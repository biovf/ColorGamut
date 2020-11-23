using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Debug = UnityEngine.Debug;

public enum GamutMappingMode
{
    Per_Channel,
    Max_RGB
}

public class ColorGamut1
{
    public Material colorGamutMat;
    public Material fullScreenTextureMat;
    public Material fullScreenTextureAndSweepMat;
    public Texture2D sweepTexture;
    public List<Texture2D> HDRIList;

    private GamutMappingMode _activeGamutMappingMode;

    public GamutMappingMode ActiveGamutMappingMode => _activeGamutMappingMode;

    private float exposure;

    public float Exposure => exposure;

    private Texture2D inputTexture;

    private bool isSweepActive;
    private bool isGamutCompressionActive;
    private bool isMultiThreaded = false;
    private bool showPixelsOutOfGamut = false;

    private int hdriIndex;
    private int inputTextureIdx = 0;
    private int yIndexIntersect = 0;
    private const int maxIterationsPerFrame = 100000;

    private Texture2D hdriTextureTransformed;

    public Texture2D HdriTextureTransformed => hdriTextureTransformed;

    private RenderTexture screenGrab;

    public RenderTexture ScreenGrab => screenGrab;

    private AnimationCurve animationCurve;
    private Color[] hdriPixelArray;
    private Vector2[] animationCurveLUT;
    private string logOutput = "";

    // Parametric curve variables
    private float slope;

    public float Slope => slope;

    private Vector2 origin;
    private CurveTest parametricCurve = null;
    private Vector2[] controlPoints;
    private List<float> tValues;

    public float SlopeMax => slopeMax;
    private float slopeMax;

    public float SlopeMin => slopeMin;
    private float slopeMin;
    public float MinRadiometricValue => minRadiometricValue;
    private float minRadiometricValue;

    public float MINExposureValue => minExposureValue;
    private float minExposureValue;

    public float MAXExposureValue => maxExposureValue;
    private float maxExposureValue;

    public float MaxRadiometricValue => maxRadiometricValue;
    private float maxRadiometricValue;
    private float maxDisplayValue;

    public float MAXDisplayValue => maxDisplayValue;

    private float minDisplayValue;

    private Vector2 greyPoint;

    public Vector2 GreyPoint => greyPoint;

    private List<float> xValues;
    private List<float> yValues;

    public int CurveLutLength => curveLutLength;
    private int curveLutLength;

    private int gamutCompressionRatioPower;

    private enum ColorRange
    {
        InsideGamut,
        BelowGamut,
        AboveGamut
    };

    private ColorRange colorRange;

    public enum CurveDataState
    {
        NotCalculated,
        Dirty,
        Calculating,
        Calculated
    };
    
 

    private CurveDataState curveDataState = CurveDataState.NotCalculated;
    public CurveDataState CurveState => curveDataState;
    private Camera mainCamera;

    public ColorGamut1(Material colorGamutMat, Material fullscreenTexMat, List<Texture2D> hdriList)
    {
        this.HDRIList = hdriList;
        this.colorGamutMat = colorGamutMat;
        this.fullScreenTextureMat = fullscreenTexMat;
    }

    public void Start(HDRPipeline pipeline)
    {
        _activeGamutMappingMode = GamutMappingMode.Max_RGB;

        hdriIndex = 0;
        gamutCompressionRatioPower = 2;
        exposure = 0.0f;

        isGamutCompressionActive = true;
        isSweepActive = false;

        // Parametric curve
        slope = 2.0f;
        slopeMin = 1.02f;
        slopeMax = 4.5f;
        maxDisplayValue = 1.5f;
        minDisplayValue = 0.00001f;
        greyPoint = new Vector2(0.18f, 0.18f);
        minExposureValue = -6.0f;
        maxExposureValue = 6.0f;

        minRadiometricValue = Mathf.Pow(2.0f, minExposureValue) * greyPoint.x;
        maxRadiometricValue = Mathf.Pow(2.0f, maxExposureValue) * greyPoint.x;

        Debug.Log("Minimum Radiometric Value: \t " + minRadiometricValue.ToString("F6"));
        Debug.Log("Maximum Radiometric Value: \t " + maxRadiometricValue.ToString("F6"));
        origin = new Vector2(minRadiometricValue, minDisplayValue);
        curveLutLength = 1023;
        createParametricCurve(greyPoint, origin);

        if (HDRIList == null)
            Debug.LogError("HDRIs list is empty");

        inputTexture = HDRIList[hdriIndex];
        mainCamera = pipeline.gameObject.GetComponent<Camera>();

        hdriPixelArray = new Color[inputTexture.width * inputTexture.height];
        hdriTextureTransformed =
            new Texture2D(inputTexture.width, inputTexture.height, TextureFormat.RGBAHalf, false, true);
        screenGrab = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBHalf,
            RenderTextureReadWrite.Linear);
        screenGrab.Create();
        mainCamera = pipeline.gameObject.GetComponent<Camera>();
        curveDataState = CurveDataState.NotCalculated;
    }

    public List<float> initialiseXCoordsInRange(int dimension, float maxRadiometricValue)
    {
        List<float> xValues = new List<float>(dimension);

        float xCoord = 0.0f;
        for (int i = 0; i < dimension; ++i)
        {
            xCoord = minRadiometricValue + (Mathf.Pow((float) i / (float) dimension, 2.0f) * maxRadiometricValue);
            xValues.Add(Mathf.Clamp01(xCoord));
        }

        return xValues;
    }

    private void createParametricCurve(Vector2 greyPoint, Vector2 origin)
    {
        if (parametricCurve == null)
            parametricCurve = new CurveTest(minExposureValue, maxExposureValue, maxRadiometricValue, maxDisplayValue);

        controlPoints = parametricCurve.createControlPoints(origin, this.greyPoint, slope);
        xValues = initialiseXCoordsInRange(curveLutLength, controlPoints[6].x);
        tValues = parametricCurve.calcTfromXquadratic(xValues.ToArray(), controlPoints);
        yValues =
            parametricCurve.calcYfromXQuadratic(xValues, tValues, new List<Vector2>(controlPoints));

        // exportDualColumnDataToCSV(xValues.ToArray(), yValues.ToArray(), "CurveAxisData.csv");
    }

    public void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            int xCoord = (int) Input.mousePosition.x;
            int yCoord = (int) Input.mousePosition.y;
            // Mouse position gives us the coordinates based on any resolution we have
            // On the other hand our textures have a fixed resolution so we're going to have to remap the mouse coordinates
            // into the texture width/height range
            float normalisedXCoord = (float) xCoord / (float) Screen.width;
            float normalisedYCoord = (float) yCoord / (float) Screen.height;
            xCoord = (int) (normalisedXCoord * (float) inputTexture.width);
            yCoord = (int) (normalisedYCoord * (float) inputTexture.height);

            Color initialHDRIColor = inputTexture.GetPixel(xCoord, yCoord);
            Color finalHDRIColor = hdriTextureTransformed.GetPixel(xCoord, yCoord);

            Vector2 shapedHDRIColor = new Vector2(initialHDRIColor.maxColorComponent, 0.0f);
            shapedHDRIColor.y = parametricCurve.getYCoordinateLogXInput(shapedHDRIColor.x, xValues.ToArray(),
                yValues.ToArray(),
                tValues.ToArray(), controlPoints);

            Debug.Log("Coordinates \t \t " + "x: " + xCoord + " y: " + yCoord);
            Debug.Log("HDRI pixel color: \t \t" + initialHDRIColor.ToString("F6"));
            Debug.Log("HDRI shaper pixel color: \t \t" + shapedHDRIColor.ToString("F6"));
            Debug.Log("HDRI pixel color * exposure: \t" + (initialHDRIColor * exposure).ToString("F6"));
            Debug.Log("Gamut mapped Color:  \t \t" + finalHDRIColor.ToString("F6"));
            Debug.Log("--------------------------------------------------------------------------------");
        }
    }

    public IEnumerator ApplyTransferFunction(RenderTexture inputRenderTexture)
    {
        int counter = maxIterationsPerFrame;
        int hdriPixelArrayLen = 0;

        float logHdriMaxRGBChannel = 0.0f;
        float gamutCompressionXCoordLinear = 0.0f;
        float gamutCompressionRange = 0.0f;

        float gamutCompressionRatio = 0.0f;
        float hdriYMaxValue = 0.0f;
        float rawMaxPixelValue = 0.0f;

        Color ratio = Color.black;
        Color hdriPixelColor = Color.black;

        Vector3 hdriPixelColorVec = Vector3.zero;
        Vector3 maxDynamicRangeVec = Vector3.zero;

        float[] xCoordsArray;
        float[] yCoordsArray;
        float[] tValuesArray;

        int completionStatus = 0;

        Texture2D textureToProcess = toTexture2D(inputRenderTexture);

        hdriPixelArray = textureToProcess.GetPixels();
        // File.WriteAllBytes("PreTransferFunctionImage.exr", textureToProcess.EncodeToEXR());

        hdriPixelArrayLen = hdriPixelArray.Length;
        int quarterSize = hdriPixelArrayLen / 4;
        int halfSize = hdriPixelArrayLen / 2;
        int threeQuartersSize = hdriPixelArrayLen - quarterSize;

        if (tValues == null)
            yield return new WaitForEndOfFrame();

        xCoordsArray = xValues.ToArray();
        yCoordsArray = yValues.ToArray();
        tValuesArray = tValues.ToArray();

        counter = maxIterationsPerFrame;
        mainCamera.clearFlags = CameraClearFlags.Skybox;
        curveDataState = CurveDataState.Calculating;
        for (int i = 0; i < hdriPixelArrayLen; i++, counter--)
        {
            if (curveDataState == CurveDataState.Dirty)
            {
                yield break;
            }

            if (i == quarterSize || i == halfSize || i == threeQuartersSize)
            {
                Debug.Log("Image Processing at " + (100.0f * (float) i / (float) hdriPixelArrayLen).ToString() +
                          "%");
            }

            ratio = Color.blue;

            hdriPixelArray[i] = hdriPixelArray[i] * Mathf.Pow(2.0f, exposure);
            // Shape image
            Color log2HdriPixelArray = new Color();
            log2HdriPixelArray.r = Shaper.calculateLinearToLog2(Math.Max(0.0f, hdriPixelArray[i].r),
                greyPoint.x, minExposureValue, maxExposureValue);
            log2HdriPixelArray.g = Shaper.calculateLinearToLog2(Math.Max(0.0f, hdriPixelArray[i].g),
                greyPoint.x, minExposureValue, maxExposureValue);
            log2HdriPixelArray.b = Shaper.calculateLinearToLog2(Math.Max(0.0f, hdriPixelArray[i].b),
                greyPoint.x, minExposureValue, maxExposureValue);

            // Calculate Pixel max color and ratio
            logHdriMaxRGBChannel = log2HdriPixelArray.maxColorComponent;
            Color linearHdriPixelColor = new Color(
                Shaper.calculateLog2ToLinear(log2HdriPixelArray.r, greyPoint.x, minExposureValue,
                    maxExposureValue),
                Shaper.calculateLog2ToLinear(log2HdriPixelArray.g, greyPoint.x, minExposureValue,
                    maxExposureValue),
                Shaper.calculateLog2ToLinear(log2HdriPixelArray.b, greyPoint.x, minExposureValue,
                    maxExposureValue));


            if (_activeGamutMappingMode == GamutMappingMode.Max_RGB)
            {
                // Retrieve the maximum RGB value but in linear space
                float linearHdriMaxRGBChannel = Shaper.calculateLog2ToLinear(logHdriMaxRGBChannel, greyPoint.x,
                    minExposureValue, maxExposureValue);
                // Calculate the ratio in linear space
                ratio = linearHdriPixelColor / linearHdriMaxRGBChannel;
                rawMaxPixelValue = linearHdriMaxRGBChannel;

                // Secondary Nuance Grade, guardrails
                if (linearHdriPixelColor.r > maxRadiometricValue ||
                    linearHdriPixelColor.g > maxRadiometricValue ||
                    linearHdriPixelColor.b > maxRadiometricValue)
                {
                    linearHdriPixelColor.r = maxRadiometricValue;
                    linearHdriPixelColor.g = maxRadiometricValue;
                    linearHdriPixelColor.b = maxRadiometricValue;
                }

                if (isGamutCompressionActive)
                {
                    ratio = calculateGamutCompression(xCoordsArray, yCoordsArray, tValuesArray, linearHdriPixelColor, ref hdriPixelColorVec, maxDynamicRangeVec, linearHdriMaxRGBChannel, ratio);
                }

                // Get Y value from curve using the array version 
                float yValue = parametricCurve.getYCoordinateLogXInput(logHdriMaxRGBChannel,
                    xCoordsArray, yCoordsArray, tValuesArray, controlPoints);
                yValue = TransferFunction.ApplyTransferFunction(yValue, TransferFunction.TransferFunctionType.sRGB);

                hdriYMaxValue = Mathf.Min(yValue, 1.0f);
                ratio.a = 1.0f;
                hdriPixelColor = hdriYMaxValue * ratio;
            }
            else
            {
                hdriPixelColor.r = parametricCurve.getYCoordinateLogXInput(log2HdriPixelArray.r,
                    xCoordsArray, yCoordsArray, tValuesArray, controlPoints);
                hdriPixelColor.g = parametricCurve.getYCoordinateLogXInput(log2HdriPixelArray.g,
                    xCoordsArray, yCoordsArray, tValuesArray, controlPoints);
                hdriPixelColor.b = parametricCurve.getYCoordinateLogXInput(log2HdriPixelArray.b,
                    xCoordsArray, yCoordsArray, tValuesArray, controlPoints);
            }

            hdriPixelArray[i].r = TransferFunction.ApplyInverseTransferFunction(hdriPixelColor.r, TransferFunction.TransferFunctionType.sRGB);
            hdriPixelArray[i].g = TransferFunction.ApplyInverseTransferFunction(hdriPixelColor.g, TransferFunction.TransferFunctionType.sRGB);
            hdriPixelArray[i].b = TransferFunction.ApplyInverseTransferFunction(hdriPixelColor.b, TransferFunction.TransferFunctionType.sRGB);
            hdriPixelArray[i].a = 1.0f;
        }

        // Make sure the result should be written out to the texture
        if (curveDataState == CurveDataState.Dirty)
        {
            yield break;
        }

        hdriTextureTransformed.SetPixels(hdriPixelArray);
        hdriTextureTransformed.Apply();

        // Write texture to disk
        // File.WriteAllBytes("PostTransferFunctionImage.exr", hdriTextureTransformed.EncodeToEXR());
        curveDataState = CurveDataState.Calculated;
        mainCamera.clearFlags = CameraClearFlags.Nothing;
        Debug.Log("Image Processing has finished");
    }

    private Color calculateGamutCompression(float[] xCoordsArray, float[] yCoordsArray, float[] tValuesArray,
        Color linearHdriPixelColor, ref Vector3 hdriPixelColorVec, Vector3 maxDynamicRangeVec,
        float linearHdriMaxRGBChannel, Color inRatio)
    {
        float gamutCompressionXCoordLinear;
        float gamutCompressionRange;
        float gamutCompressionRatio;
        Color ratio = inRatio;
        gamutCompressionXCoordLinear = 0.0f; // Intersect of x on Y = 1

        // Calculate gamut compression values by iterating through the Y values array and returning the closest x coord
        gamutCompressionXCoordLinear = Shaper.calculateLog2ToLinear(
            parametricCurve.getXCoordinate(1.0f, xCoordsArray, yCoordsArray, tValuesArray, controlPoints),
            greyPoint.x, minExposureValue, maxExposureValue);

        if (linearHdriPixelColor.r > gamutCompressionXCoordLinear ||
            linearHdriPixelColor.g > gamutCompressionXCoordLinear ||
            linearHdriPixelColor.b > gamutCompressionXCoordLinear)
        {
            gamutCompressionRange = maxRadiometricValue - gamutCompressionXCoordLinear;
            gamutCompressionRatio = (linearHdriPixelColor.maxColorComponent - gamutCompressionXCoordLinear) /
                                    gamutCompressionRange;

            hdriPixelColorVec.Set(linearHdriPixelColor.r, linearHdriPixelColor.g,
                linearHdriPixelColor.b);
            maxDynamicRangeVec.Set(maxRadiometricValue, maxRadiometricValue,
                maxRadiometricValue);
            hdriPixelColorVec = Vector3.Lerp(hdriPixelColorVec, maxDynamicRangeVec,
                Mathf.SmoothStep(0.0f, 1.0f, gamutCompressionRatio)
                /*bleachingRatio / (bleachingRatio + 1.0f));*/
                /*Mathf.Pow(bleachingRatio, (float)gamutCompressionRatioPower)*/);

            linearHdriPixelColor.r = hdriPixelColorVec.x;
            linearHdriPixelColor.g = hdriPixelColorVec.y;
            linearHdriPixelColor.b = hdriPixelColorVec.z;

            ratio = linearHdriPixelColor / linearHdriMaxRGBChannel;
        }

        return ratio;
    }

    public void exportTransferFunction(string fileName)
    {
        // Set the DOMAIN_MIN and DOMAIN_MAX ranges
        Vector3 minDisplayValueVec = Vector3.zero; //new Vector3(minDisplayValue, minDisplayValue, minDisplayValue);
        Vector3 maxDisplayValueVec = Vector3.one; //new Vector3(maxDisplayValue, maxDisplayValue, maxDisplayValue);

        List<float> yValuesTmp = yValues; 
        float[] yValuesEOTF = new float[yValuesTmp.Count];
        int i = 0;
        for (i = 0; i < yValuesTmp.Count; i++)
        {
            if (yValuesTmp[i] >= 1.0f)
                break;
            
            yValuesEOTF[i] = yValuesTmp[i];
        }

        // Pass data to be converted and written to disk as a .cube file
        CubeLutExporter.saveLutAsCube(yValuesEOTF, fileName, i /*yValues.Count*/, minDisplayValueVec,
            maxDisplayValueVec, false);
    }

    public void SetCurveDataState(CurveDataState newState)
    {
        if (curveDataState == newState)
        {
            Debug.LogWarning("Current state is being change to itself again");
            return;
        }

        switch (newState)
        {
            case CurveDataState.NotCalculated:
            {
                mainCamera.clearFlags = CameraClearFlags.Skybox;
                curveDataState = CurveDataState.NotCalculated;
                break;
            }
            case CurveDataState.Dirty:
            {
                Debug.Log("Image Processing has started");
                mainCamera.clearFlags = CameraClearFlags.Skybox;
                curveDataState = CurveDataState.Dirty;
                break;
            }
            case CurveDataState.Calculated:
            {
                mainCamera.clearFlags = CameraClearFlags.Nothing;
                curveDataState = CurveDataState.Calculated;
                break;
            }
            case CurveDataState.Calculating:
            {
                mainCamera.clearFlags = CameraClearFlags.Skybox;
                curveDataState = CurveDataState.Calculating;
                break;
            }
        }
    }

    public Texture2D getHDRITexture()
    {
        return hdriTextureTransformed;
    }

    // Dimension - size of the look up table being created
    // maxRadiometricValue - maximum radiometric value we are using
    // public List<float> initialiseXCoordsInRange(int dimension, float maxRadiometricValue)
    // {
    //     List<float> xValues = new List<float>(dimension);
    //
    //     float halfDimensionFlt = (((float) dimension) / 2.0f);
    //     int halfDimensionInt = dimension / 2;
    //     // calculate the step used from our minimum radiometric until our mid grey point
    //     float stepPreMidGrey = (greyPoint.x - minRadiometricValue) / halfDimensionFlt;
    //     // calculate the step necessary for the second half of the values, from mid grey point until maxRadiometricValue
    //     float stepPostMidGrey = (maxRadiometricValue - greyPoint.x) / (halfDimensionFlt - 1.0f);
    //     float xCoord = 0.0f;
    //     
    //     for (int i = 0; i <= halfDimensionInt; ++i)
    //     {
    //         xCoord = MinRadiometricValue + (i * stepPreMidGrey);
    //         
    //         if (xCoord < MinRadiometricValue)
    //             continue;
    //
    //         if (Mathf.Approximately(xCoord, maxRadiometricValue))
    //             break;
    //
    //         xValues.Add(Shaper.calculateLinearToLog2(xCoord));
    //         // Debug.Log("1st half - Index: " + i + " xCoord: " + xCoord + " \t Shaped Value " + xValues[i] + " \t ");
    //     }
    //
    //     int len = (dimension % 2) == 0 ? halfDimensionInt : halfDimensionInt + 1;
    //     for (int i = 1; i < len; ++i)
    //     {
    //         xCoord = 0.18f + (i * stepPostMidGrey);
    //         
    //         if (xCoord < MinRadiometricValue)
    //             continue;
    //         
    //
    //         xValues.Add(Shaper.calculateLinearToLog2(xCoord));
    //         // Debug.Log("2nd half -Index: " + (xValues.Count - 1) + " xCoord: " + xCoord + " \t Shaped Value " + xValues[xValues.Count - 1] + " \t ");
    //     }
    //
    //     return xValues;
    // }

    public void getParametricCurveValues(out float inSlope, out float originPointX, out float originPointY,
        out float greyPointX, out float greyPointY)
    {
        inSlope = slope;
        originPointX = origin.x;
        originPointY = origin.y;
        greyPointX = greyPoint.x;
        greyPointY = greyPoint.y;
    }

    public void setParametricCurveValues(float inSlope, float originPointX, float originPointY,
        float greyPointX, float greyPointY)
    {
        this.slope = inSlope;
        this.origin.x = originPointX;
        this.origin.y = originPointY;
        this.greyPoint.x = greyPointX;
        this.greyPoint.y = greyPointY;

        SetCurveDataState(CurveDataState.Dirty);
        createParametricCurve(greyPoint, origin);
    }

    public bool getShowSweep()
    {
        return isSweepActive;
    }

    public void setShowSweep(bool isActive, Texture2D inputTexture)
    {
        isSweepActive = isActive;
        this.inputTexture = inputTexture;
        SetCurveDataState(CurveDataState.Dirty);
    }

    public bool getIsMultiThreaded()
    {
        return isMultiThreaded;
    }

    public void setIsMultiThreaded(bool isMultiThreaded)
    {
        this.isMultiThreaded = isMultiThreaded;
    }

    public void setShowOutOfGamutPixels(bool isPixelOutOfGamut)
    {
        this.showPixelsOutOfGamut = isPixelOutOfGamut;
        SetCurveDataState(CurveDataState.Dirty);
    }

    Texture2D toTexture2D(RenderTexture rTex)
    {
        Texture2D tex = new Texture2D(rTex.width, rTex.height, TextureFormat.RGBAHalf, false, true);
        RenderTexture.active = rTex;
        tex.ReadPixels(new Rect(0, 0, rTex.width, rTex.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        return tex;
    }

    public Vector2[] getControlPoints()
    {
        // TODO: Replace with curveStateData state
        if (controlPoints == null || controlPoints.Length == 0)
        {
            greyPoint = new Vector2(0.18f, 0.18f);
            slope = 2.2f;
            minRadiometricValue = Mathf.Pow(2.0f, -6.0f) * greyPoint.x;
            maxRadiometricValue = Mathf.Pow(2.0f, 6.0f) * greyPoint.x;
            origin = new Vector2(minRadiometricValue, 0.00001f);

            createParametricCurve(greyPoint, origin);
        }

        return controlPoints;
    }

    private void exportDualColumnDataToCSV(float[] data1ToExport, float[] data2ToExport, string fileName)
    {
        if (data1ToExport.Length != data2ToExport.Length)
        {
            Debug.LogError("Input arrays must have the same size");
            return;
        }

        StringBuilder strBuilder = new StringBuilder(data1ToExport.Length);
        for (int i = 0; i < data1ToExport.Length; i++)
        {
            strBuilder.AppendLine(data1ToExport[i].ToString() + " , " + data2ToExport[i].ToString());
        }

        File.WriteAllText(fileName, strBuilder.ToString());
    }

    private void exportSingleColumnDataToCSV(float[] dataToExport, string fileName)
    {
        StringBuilder strBuilder = new StringBuilder(dataToExport.Length);
        foreach (var value in dataToExport)
        {
            strBuilder.AppendLine(dataToExport.ToString() + ",");
        }

        File.WriteAllText(fileName, strBuilder.ToString());
    }

    public List<float> getTValues()
    {
        return tValues;
    }

    public List<float> getXValues()
    {
        return xValues;
    }

    public List<float> getYValues()
    {
        return yValues;
    }

    public CurveTest getParametricCurve()
    {
        if (parametricCurve == null)
            createParametricCurve(greyPoint, origin);

        return parametricCurve;
    }

    public void setInputTexture(Texture2D inputTexture)
    {
        this.inputTexture = inputTexture;
        SetCurveDataState(CurveDataState.Dirty);
    }

    // public void setHDRIIndex(int index)
    // {
    //     hdriIndex = index;
    //     SetCurveDataState(CurveDataState.Dirty);
    // }

    public void setGamutCompression(bool inIsGamutCompressionActive)
    {
        isGamutCompressionActive = inIsGamutCompressionActive;
        SetCurveDataState(CurveDataState.Dirty);
    }

    public void setExposure(float exposure)
    {
        this.exposure = exposure;
        // SetCurveDataState(CurveDataState.Dirty);
    }

    public void setActiveTransferFunction(GamutMappingMode gamutMappingMode)
    {
        this._activeGamutMappingMode = gamutMappingMode;
        // SetCurveDataState(CurveDataState.Dirty);
    }

    public void setGamutCompressionRatioPower(int ratioPower)
    {
        this.gamutCompressionRatioPower = ratioPower;
        SetCurveDataState(CurveDataState.Dirty);
    }

    public void setCurveParams(CurveParams curveParams)
    {
        isGamutCompressionActive = curveParams.isGamutCompressionActive;
        exposure = curveParams.exposure;
        slope = curveParams.slope;
        origin.x = curveParams.originX;
        origin.y = curveParams.originY;
        _activeGamutMappingMode = curveParams.ActiveGamutMappingMode;
        createParametricCurve(greyPoint, origin);
    }

    static public Texture2D GetRTPixels(RenderTexture rt)
    {
        // Remember currently active render texture
        RenderTexture currentActiveRT = RenderTexture.active;

        // Set the supplied RenderTexture as the active one
        RenderTexture.active = rt;

        // Create a new Texture2D and read the RenderTexture image into it
        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBAHalf, false);
        tex.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);

        // Restorie previously active render texture
        RenderTexture.active = currentActiveRT;
        return tex;
    }
}