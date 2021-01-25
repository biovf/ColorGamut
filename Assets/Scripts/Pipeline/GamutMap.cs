using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

public enum GamutMappingMode
{
    Per_Channel,
    Max_RGB
}

public class GamutMap
{
    public Material fullScreenTextureMat;
    public Material fullScreenTextureAndSweepMat;
    public Texture2D sweepTexture;
    public List<Texture2D> HDRIList;

    private GamutMappingMode activeGamutMappingMode;

    public GamutMappingMode ActiveGamutMappingMode => activeGamutMappingMode;

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

    private Color[] hdriPixelArray;

    // Parametric curve variables
    private float slope;

    public float Slope => slope;

    private Vector2 origin;
    private GamutCurve parametricGamutCurve = null;
    private Vector2[] controlPoints;
    private List<float> tValues;

    public float SlopeMax => slopeMax;
    private float slopeMax;

    public float SlopeMin => slopeMin;
    private float slopeMin;
    public float MinRadiometricValue => minRadiometricValue;
    private float minRadiometricValue;

    public float MinRadiometricExposure => minRadiometricExposure;
    private float minRadiometricExposure;

    public float MaxRadiometricExposure => maxRadiometricExposure;
    private float maxRadiometricExposure;

    public float MaxRadiometricDynamicRange => maxRadiometricValue;
    private float maxRadiometricValue;
    private float maxDisplayValue;
    public float MaxDisplayValue => maxDisplayValue;

    private float minDisplayValue;
    private float minDisplayExposure;
    public float MinDisplayExposure => minDisplayExposure;
    private float maxDisplayExposure;
    public float MaxDisplayExposure => maxDisplayExposure;

    private float maxRadiometricLatitude;
    private float maxLatitudeLimit;
    private float maxRadiometricLatitudeExposure;

    private Vector2 midGrey;
    private float maxNits;
    public Vector2 MidGrey => midGrey;

    private List<float> xValues;
    private List<float> yValues;

    public int CurveLutLength => curveLutLength;
    private int curveLutLength = 1024;

    private int gamutCompressionRatioPower;
    private float totalRadiometricExposure;

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

    public GamutMap( Material fullscreenTexMat, List<Texture2D> hdriList)
    {
        this.HDRIList = hdriList;
       
        this.fullScreenTextureMat = fullscreenTexMat;
    }

    public void Start(HDRPipeline pipeline)
    {
        activeGamutMappingMode = GamutMappingMode.Max_RGB;

        hdriIndex = 0;
        gamutCompressionRatioPower = 2;
        exposure = 0.0f;
        curveLutLength = 1024;

        isGamutCompressionActive = true;
        isSweepActive = false;

        // Parametric curve
        slope = 2.05f;
        slopeMin = 1.02f;
        slopeMax = 6.5f;
        maxNits = 100.0f;                       // Maximum nit value we support
        // Max and min display values are unit agnostic
        maxDisplayValue = 100.0f / maxNits;     // in SDR we support a maximum of 100 nits
        minDisplayValue = 0.05f / maxNits;      // in SDR we support a minimum of 0.05f nits which is an average black value for a LED display
        midGrey = new Vector2(18.0f / maxNits, 18.0f / maxNits);
        minRadiometricExposure = -6.0f;
        maxRadiometricExposure = 6.0f;
        totalRadiometricExposure = maxRadiometricExposure - minRadiometricExposure;

        minDisplayExposure = Mathf.Log(minDisplayValue / midGrey.y, 2.0f);
        maxDisplayExposure = Mathf.Log(maxDisplayValue / midGrey.y, 2.0f);

        minRadiometricValue = Mathf.Pow(2.0f, minRadiometricExposure) * midGrey.x;
        maxRadiometricValue = Mathf.Pow(2.0f, maxRadiometricExposure) * midGrey.x;
        
        maxLatitudeLimit = 0.8f;                                // value in camera encoded log2/EV 
        maxRadiometricLatitudeExposure = totalRadiometricExposure * maxLatitudeLimit;
        maxRadiometricLatitude = Shaper.calculateLog2ToLinear(maxLatitudeLimit, midGrey.x, minRadiometricExposure, maxRadiometricExposure);
        //(maxRadiometricDynamicRange - minRadiometricDynamicRange) * maxLatitudeLimit;

        origin = new Vector2(minRadiometricValue, minDisplayValue);
        createParametricCurve(midGrey, origin);

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
        curveDataState = CurveDataState.NotCalculated;

        Debug.Log("Minimum Radiometric Value: \t " + minRadiometricValue.ToString("F6"));
        Debug.Log("Maximum Radiometric Value: \t " + maxRadiometricValue.ToString("F6"));
    }

    public List<float> initialiseXCoordsInRange(int lutDimension, float minValue, float maxValue)
    {
        List<float> xValues = new List<float>(lutDimension);

        float xCoord = 0.0f;
        for (int i = 0; i < lutDimension; ++i)
        {
            xValues.Add(((float)i / (float)(lutDimension - 1)));
        }

        return xValues;
    }

    // TODO: return constant slope (bunch of monotonically increasing values from 1.0) for values above the maximum latitude range
    private void createParametricCurve(Vector2 greyPoint, Vector2 origin)
    {
        if (parametricGamutCurve == null)
            parametricGamutCurve = new GamutCurve(minRadiometricExposure, maxRadiometricExposure, maxRadiometricValue, maxDisplayValue, 
                minDisplayExposure, maxDisplayExposure, maxRadiometricLatitude, maxRadiometricLatitudeExposure);

        controlPoints = parametricGamutCurve.createControlPoints(origin, this.midGrey, slope);
        xValues = initialiseXCoordsInRange(curveLutLength, Shaper.calculateLog2ToLinear(0.0f, midGrey.x, minRadiometricExposure, maxRadiometricExposure),
            Shaper.calculateLog2ToLinear(1.0f, midGrey.x, minRadiometricExposure, maxRadiometricExposure));
        tValues = parametricGamutCurve.calcTfromXquadratic(xValues.ToArray(), controlPoints);
        yValues = parametricGamutCurve.calcYfromXQuadratic(xValues, tValues, new List<Vector2>(controlPoints));

        if (Application.isPlaying)
        {
            float[] yDisplayLinearEOTFValues = new float[yValues.Count];

            float[] yDisplayIntrinsic = new float[yValues.Count];

            for (int j = 0; j < xValues.Count; j++)
            {
                float yDisplayIntrinsicValue = yValues[j];// parametricGamutCurve.getYCoordinateLogXInput(((float)j)/xValues.Count, xValues.ToArray(), yValues.ToArray(), tValues.ToArray(), controlPoints);
                float yDisplayLinearValue = Shaper.calculateLog2ToLinear(yDisplayIntrinsicValue, midGrey.y, minDisplayExposure, maxDisplayExposure);
                yDisplayIntrinsic[j] = yDisplayIntrinsicValue;
                yDisplayLinearEOTFValues[j] = Mathf.Pow(yDisplayLinearValue, 1.0f / 2.2f);
                exportDualColumnDataToCSV(yDisplayIntrinsic, yDisplayLinearEOTFValues, "CurveAxisData.csv");
            }

            exportDualColumnDataToCSV(xValues.ToArray(), yValues.ToArray(), "VanillaFunction.csv");

        }

        float yDisplayIntrinsicValue2 = 0.77f;// parametricGamutCurve.getYCoordinateLogXInput(xValues[j], xValuesArray, yValuesArray, tValuesArray, controlPoints);
        float yDisplayLinearValue2 = Shaper.calculateLog2ToLinear(yDisplayIntrinsicValue2, midGrey.y, minDisplayExposure, maxDisplayExposure);
        Debug.Log("yDisplayLinearValue2 " + yDisplayLinearValue2);
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
            shapedHDRIColor.y = parametricGamutCurve.getYCoordinateLogXInput(shapedHDRIColor.x, xValues.ToArray(),
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
        float hdriYMaxValue = 0.0f;
        float rawMaxPixelValue = 0.0f;

        Color ratio = Color.black;
        Color hdriPixelColor = Color.black;

        Vector3 hdriPixelColorVec = Vector3.zero;
        Vector3 maxDynamicRangeVec = Vector3.zero;

        float[] xCoordsArray;
        float[] yCoordsArray;
        float[] tValuesArray;

        Texture2D textureToProcess = toTexture2D(inputRenderTexture);

        hdriPixelArray = textureToProcess.GetPixels();
        File.WriteAllBytes("PreTransferFunctionImage.exr", textureToProcess.EncodeToEXR());

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
                Debug.Log("Image Processing at " + (100.0f * (float) i / (float) hdriPixelArrayLen).ToString() + "%");
            }

            ratio = Color.blue;

            hdriPixelArray[i] = hdriPixelArray[i] * Mathf.Pow(2.0f, exposure);
            Color temp = hdriPixelArray[i];
            // Shape image
            Color log2HdriPixelArray = new Color();
            log2HdriPixelArray.r = Shaper.calculateLinearToLog2(Math.Max(0.0f, hdriPixelArray[i].r),
                midGrey.x, minRadiometricExposure, maxRadiometricExposure);
            log2HdriPixelArray.g = Shaper.calculateLinearToLog2(Math.Max(0.0f, hdriPixelArray[i].g),
                midGrey.x, minRadiometricExposure, maxRadiometricExposure);
            log2HdriPixelArray.b = Shaper.calculateLinearToLog2(Math.Max(0.0f, hdriPixelArray[i].b),
                midGrey.x, minRadiometricExposure, maxRadiometricExposure);


            // Calculate Pixel max color and ratio
            logHdriMaxRGBChannel = log2HdriPixelArray.maxColorComponent;
            Color linearHdriPixelColor = new Color(
                Shaper.calculateLog2ToLinear(log2HdriPixelArray.r, midGrey.x, minRadiometricExposure, maxRadiometricExposure),
                Shaper.calculateLog2ToLinear(log2HdriPixelArray.g, midGrey.x, minRadiometricExposure, maxRadiometricExposure),
                Shaper.calculateLog2ToLinear(log2HdriPixelArray.b, midGrey.x, minRadiometricExposure, maxRadiometricExposure));


            if (activeGamutMappingMode == GamutMappingMode.Max_RGB)
            {
                // Retrieve the maximum RGB value but in linear space
                float linearHdriMaxRGBChannel = Shaper.calculateLog2ToLinear(logHdriMaxRGBChannel, midGrey.x,
                    minRadiometricExposure, maxRadiometricExposure);
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

                // Get Y value from curve by retrieving the respective value from the x coordinate array
                float yValue = parametricGamutCurve.getYCoordinateLogXInput(logHdriMaxRGBChannel, xCoordsArray, yCoordsArray, tValuesArray, controlPoints);
                yValue = Shaper.calculateLog2ToLinear(yValue, midGrey.y, minDisplayExposure, maxDisplayExposure);
                hdriYMaxValue = Mathf.Min(yValue, 1.0f);
                ratio.a = 1.0f;
                hdriPixelColor = hdriYMaxValue * ratio;
            }
            else
            {
                hdriPixelColor.r = parametricGamutCurve.getYCoordinateLogXInput(log2HdriPixelArray.r,
                    xCoordsArray, yCoordsArray, tValuesArray, controlPoints);
                hdriPixelColor.g = parametricGamutCurve.getYCoordinateLogXInput(log2HdriPixelArray.g,
                    xCoordsArray, yCoordsArray, tValuesArray, controlPoints);
                hdriPixelColor.b = parametricGamutCurve.getYCoordinateLogXInput(log2HdriPixelArray.b,
                    xCoordsArray, yCoordsArray, tValuesArray, controlPoints);
            }
            
            // TODO Should this be updated/removed?
            hdriPixelColor.r = remap(hdriPixelColor.r, minDisplayValue, maxDisplayValue, 0.0f, 1.0f);
            hdriPixelColor.g = remap(hdriPixelColor.g, minDisplayValue, maxDisplayValue, 0.0f, 1.0f);
            hdriPixelColor.b = remap(hdriPixelColor.b, minDisplayValue, maxDisplayValue, 0.0f, 1.0f);

            hdriPixelArray[i].r = hdriPixelColor.r;//TransferFunction.ApplyInverseTransferFunction(hdriPixelColor.r, TransferFunction.TransferFunctionType.sRGB);
            hdriPixelArray[i].g = hdriPixelColor.g;//TransferFunction.ApplyInverseTransferFunction(hdriPixelColor.g, TransferFunction.TransferFunctionType.sRGB);
            hdriPixelArray[i].b = hdriPixelColor.b;//TransferFunction.ApplyInverseTransferFunction(hdriPixelColor.b, TransferFunction.TransferFunctionType.sRGB);
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
        File.WriteAllBytes("PostTransferFunctionImage.exr", hdriTextureTransformed.EncodeToEXR());
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
        gamutCompressionXCoordLinear = Shaper.calculateLog2ToLinear(maxLatitudeLimit, midGrey.x, minRadiometricExposure, maxRadiometricExposure);

        if (linearHdriPixelColor.r > gamutCompressionXCoordLinear ||
            linearHdriPixelColor.g > gamutCompressionXCoordLinear ||
            linearHdriPixelColor.b > gamutCompressionXCoordLinear)
        {
            gamutCompressionRange = maxRadiometricValue - gamutCompressionXCoordLinear;
            gamutCompressionRatio = (linearHdriPixelColor.maxColorComponent - gamutCompressionXCoordLinear) /
                                    gamutCompressionRange;

            Vector3 inRatioVec = new Vector3(inRatio.r, inRatio.g, inRatio.b);
            inRatioVec = Vector3.Lerp(inRatioVec, Vector3.one, gamutCompressionRatio);
            ratio = new Color(inRatioVec.x, inRatioVec.y, inRatioVec.z);

            //hdriPixelColorVec.Set(linearHdriPixelColor.r, linearHdriPixelColor.g, linearHdriPixelColor.b);
            //maxDynamicRangeVec.Set(maxRadiometricDynamicRange, maxRadiometricDynamicRange, maxRadiometricDynamicRange);

            //hdriPixelColorVec = Vector3.Lerp(hdriPixelColorVec, linearHdriMaxRGBChannel, Mathf.SmoothStep(0.0f, 1.0f, gamutCompressionRatio)
            //    /*bleachingRatio / (bleachingRatio + 1.0f));*/
            //    /*Mathf.Pow(bleachingRatio, (float)gamutCompressionRatioPower)*/);

            //linearHdriPixelColor.r = hdriPixelColorVec.x;
            //linearHdriPixelColor.g = hdriPixelColorVec.y;
            //linearHdriPixelColor.b = hdriPixelColorVec.z;

            //ratio = linearHdriPixelColor / linearHdriMaxRGBChannel;
        }

        return ratio;
    }


    public void exportTransferFunction(string fileName)
    {
        // Set the DOMAIN_MIN and DOMAIN_MAX ranges
        Vector3 minCameraNativeVec = /*Vector3.zero;*/new Vector3(xValues[0], xValues[0], xValues[0]);
        Vector3 maxCameraNativeVec = /*Vector3.one;*/ new Vector3(xValues[xValues.Count - 1], xValues[xValues.Count - 1], xValues[xValues.Count - 1]);

        // List<float> yValuesTmp = yValues;
        // float[] yValuesEOTF = new float[yValuesTmp.Count];
        // int i = 0;
        // for (i = 0; i < yValuesTmp.Count; i++)
        // {
        //
        //     //if (yValuesTmp[i] >= 1.0f)
        //     //    break;
        //
        //     //yValuesEOTF[i] = yValuesTmp[i];
        // }

        // Pass data to be converted and written to disk as a .cube file
        // CubeLutExporter.saveLutAsCube(yValuesEOTF, fileName, i /*yValues.Count*/, minDisplayValueVec,
        //     maxDisplayValueVec, false);



        float[] xCameraIntrinsicArray = xValues.ToArray();
        float[] yDisplayIntrinsicArray = yValues.ToArray();
        float[] tValuesArray = tValues.ToArray();
        float[] yDisplayLinearEOTFValues = new float[yValues.Count];

        // X -> camera intrinsic encoding (camera negative)
        // Y -> display intrinsic (display negative)
        // get x values and y values arrays
        // get aesthetic curve in display intrinsic from camera intrisinc encoding (x camera axis)
        // go from display intrinsic to display linear
        //                  Shaper.calculateLog2toLinear(yVal, midGrey.y, minDisplayExposure, maxDisplayExposure);
        // go from display linear to display inverse EOTF encoded
        for (int cameraIntrinsicIndex = 0; cameraIntrinsicIndex < yDisplayIntrinsicArray.Length; cameraIntrinsicIndex++)
        {
            // Take camera intrinsic index and turn it into a float cameraIntrinsicIndex/num entries
            // Take result and run it through the aesthetic compression function and store into curved
            // Take curved and convert it to display linear

            float t = 0.5f;

            // get Y coordinate from X            
            float yDisplayIntrinsicValue = yDisplayIntrinsicArray[cameraIntrinsicIndex];// ((float)cameraIntrinsicIndex)/ ((float)xValues.Count); //parametricGamutCurve.calcYfromXQuadratic((, tValues, new List<Vector2>(controlPoints));   //parametricGamutCurve.getYCoordinateLogXInput((, xCameraIntrinsicArray, yDisplayIntrinsicArray, tValuesArray , controlPoints);
            float yDisplayLinearValue = Shaper.calculateLog2ToLinear(yDisplayIntrinsicValue, midGrey.y, minDisplayExposure, maxDisplayExposure);
            // encode pow(y, 1.0/2.2)
            yDisplayLinearEOTFValues[cameraIntrinsicIndex] = Mathf.Pow(yDisplayLinearValue, 1.0f / 2.2f);// TransferFunction.ApplyInverseTransferFunction(yDisplayLinearValue, TransferFunction.TransferFunctionType.sRGB);
        }


        exportDualColumnDataToCSV(xValues.ToArray(), yDisplayLinearEOTFValues, "ResolveExportTF.csv");

        CubeLutExporter.saveLutAsCube(yDisplayLinearEOTFValues, fileName, xValues.Count, minCameraNativeVec, maxCameraNativeVec, false);
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

    public void getParametricCurveValues(out float inSlope, out float originPointX, out float originPointY,
        out float greyPointX, out float greyPointY)
    {
        inSlope = slope;
        originPointX = origin.x;
        originPointY = origin.y;
        greyPointX = midGrey.x;
        greyPointY = midGrey.y;
    }

    public void setParametricCurveValues(float inSlope, float originPointX, float originPointY,
        float greyPointX, float greyPointY)
    {
        this.slope = inSlope;
        this.origin.x = originPointX;
        this.origin.y = originPointY;
        this.midGrey.x = greyPointX;
        this.midGrey.y = greyPointY;

        SetCurveDataState(CurveDataState.Dirty);
        createParametricCurve(midGrey, origin);
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
            midGrey = new Vector2(0.18f, 0.18f);
            slope = 2.2f;
            minRadiometricValue = Mathf.Pow(2.0f, -6.0f) * midGrey.x;
            maxRadiometricValue = Mathf.Pow(2.0f, 6.0f) * midGrey.x;
            origin = new Vector2(minRadiometricValue, 0.00001f);

            createParametricCurve(midGrey, origin);
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

    public GamutCurve getParametricCurve()
    {
        if (parametricGamutCurve == null)
            createParametricCurve(midGrey, origin);

        return parametricGamutCurve;
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

    float remap(float value, float min0, float max0, float min1, float max1)
    {
        return min1 + (value - min0) * ((max1 - min1) / (max0 - min0));
    }

    public void setExposure(float exposure)
    {
        this.exposure = exposure;
        // SetCurveDataState(CurveDataState.Dirty);
    }

    public void setActiveTransferFunction(GamutMappingMode gamutMappingMode)
    {
        this.activeGamutMappingMode = gamutMappingMode;
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
        activeGamutMappingMode = curveParams.ActiveGamutMappingMode;
        createParametricCurve(midGrey, origin);
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
