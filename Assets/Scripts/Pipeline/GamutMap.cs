#define DEBUG_IMAGES
//#define DEBUG_CURVE_DATA
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
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

    private Texture2D inputRadiometricLinearTexture;

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

    public Vector2 Origin => origin;
    private Vector2 origin;
    private GamutCurve parametricGamutCurve = null;

    public GamutCurve ParametricGamutCurve => parametricGamutCurve;

    private Vector2[] controlPoints;

    public Vector2[] ControlPoints => controlPoints;

    private List<float> tValues;

    public List<float> TValues => tValues;

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
    public float MinDisplayValue => minDisplayValue;
    private float minDisplayValue;
    private float minDisplayExposure;
    public float MinDisplayExposure => minDisplayExposure;
    private float maxDisplayExposure;
    public float MaxDisplayExposure => maxDisplayExposure;

    private float maxRadiometricLatitude;
    private float curveCoordCoordinateMaxLatitude;

    public float CurveCoordMaxLatitude
    {
        get => curveCoordCoordinateMaxLatitude;
        set => curveCoordCoordinateMaxLatitude = value;
    }

    private float chromaticityMaxLatitude;
    public float ChromaticityMaxLatitude => chromaticityMaxLatitude;

    private float maxRadiometricLatitudeExposure;

    private Vector2 midGreySDR;
    private float maxNits;
    public Vector2 MidGreySdr => midGreySDR;

    private List<float> xCameraIntrinsicValues;
    public List<float> XCameraIntrinsicValues => xCameraIntrinsicValues;
    private List<float> yDisplayIntrinsicValues;

    public List<float> YDisplayIntrinsicValues => yDisplayIntrinsicValues;

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

    public CurveDataState CurveState
    {
        get => curveDataState;
        set => curveDataState = value;
    }

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
        slope = 1.7f;
        slopeMin = 1.02f;
        slopeMax = 6.5f;
        maxNits = 100.0f;                       // Maximum nit value we support
        // Max and min display values are unit agnostic
        maxDisplayValue = 100.0f / maxNits;     // in SDR we support a maximum of 100 nits
        minDisplayValue = 0.05f / maxNits;      // in SDR we support a minimum of 0.05f nits which is an average black value for a LED display
        midGreySDR = new Vector2(18.0f / maxNits, 18.0f / maxNits);
        minRadiometricExposure = -6.0f;
        maxRadiometricExposure = 6.0f;
        totalRadiometricExposure = maxRadiometricExposure - minRadiometricExposure;

        minDisplayExposure = Mathf.Log(minDisplayValue / midGreySDR.y, 2.0f);
        maxDisplayExposure = Mathf.Log(maxDisplayValue / midGreySDR.y, 2.0f);

        minRadiometricValue = Mathf.Pow(2.0f, minRadiometricExposure) * midGreySDR.x;
        maxRadiometricValue = Mathf.Pow(2.0f, maxRadiometricExposure) * midGreySDR.x;

        chromaticityMaxLatitude = 0.85f;
        curveCoordCoordinateMaxLatitude = 0.85f;                                // value in camera encoded log2/EV
        maxRadiometricLatitudeExposure = totalRadiometricExposure * curveCoordCoordinateMaxLatitude;
        maxRadiometricLatitude = Shaper.calculateLog2ToLinear(curveCoordCoordinateMaxLatitude, midGreySDR.x, minRadiometricExposure, maxRadiometricExposure);

        origin = new Vector2(minRadiometricValue, minDisplayValue);
        createParametricCurve(midGreySDR, origin);

        if (HDRIList == null)
            Debug.LogError("HDRIs list is empty");

        inputRadiometricLinearTexture = HDRIList[hdriIndex];
        mainCamera = pipeline.gameObject.GetComponent<Camera>();

        hdriPixelArray = new Color[inputRadiometricLinearTexture.width * inputRadiometricLinearTexture.height];
        hdriTextureTransformed =
            new Texture2D(inputRadiometricLinearTexture.width, inputRadiometricLinearTexture.height, TextureFormat.RGBAHalf, false, true);
        screenGrab = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBHalf,
            RenderTextureReadWrite.Linear);
        screenGrab.Create();
        curveDataState = CurveDataState.NotCalculated;

        Debug.Log("Minimum Radiometric Value: \t " + minRadiometricValue.ToString("F6"));
        Debug.Log("Maximum Radiometric Value: \t " + maxRadiometricValue.ToString("F6"));

    }

    public List<float> initialiseXCoordsInRange(int lutDimension)
    {
        List<float> xCameraIntrinsicValues = new List<float>(lutDimension);

        for (int i = 0; i < lutDimension; ++i)
        {
            xCameraIntrinsicValues.Add(((float)i / (float)(lutDimension - 1)));
        }

        return xCameraIntrinsicValues;
    }

    public void createParametricCurve(Vector2 greyPoint, Vector2 origin)
    {
        maxRadiometricLatitudeExposure = totalRadiometricExposure * curveCoordCoordinateMaxLatitude;
        maxRadiometricLatitude = Shaper.calculateLog2ToLinear(curveCoordCoordinateMaxLatitude, midGreySDR.x, minRadiometricExposure, maxRadiometricExposure);

        if (parametricGamutCurve == null)
            parametricGamutCurve = new GamutCurve(minRadiometricExposure, maxRadiometricExposure, maxRadiometricValue, maxDisplayValue, 
                minDisplayExposure, maxDisplayExposure, maxRadiometricLatitude, maxRadiometricLatitudeExposure, curveCoordCoordinateMaxLatitude);

        controlPoints = parametricGamutCurve.createControlPoints(origin, this.midGreySDR, slope);
        xCameraIntrinsicValues = initialiseXCoordsInRange(curveLutLength);
        tValues = parametricGamutCurve.calcTfromXquadratic(xCameraIntrinsicValues.ToArray(), controlPoints);
        yDisplayIntrinsicValues = parametricGamutCurve.calcYfromXQuadratic(xCameraIntrinsicValues, tValues, new List<Vector2>(controlPoints));
        exportDualColumnDataToCSV(xCameraIntrinsicValues.ToArray(), yDisplayIntrinsicValues.ToArray(), "DebugData/CurveData.csv");
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
            xCoord = (int) (normalisedXCoord * (float) inputRadiometricLinearTexture.width);
            yCoord = (int) (normalisedYCoord * (float) inputRadiometricLinearTexture.height);

            Color initialHDRIColor = inputRadiometricLinearTexture.GetPixel(xCoord, yCoord);
            Color finalHDRIColor = hdriTextureTransformed.GetPixel(xCoord, yCoord);

            Vector2 shapedHDRIColor = new Vector2(initialHDRIColor.maxColorComponent, 0.0f);
            shapedHDRIColor.y = parametricGamutCurve.getYCoordinateLogXInput(shapedHDRIColor.x, xCameraIntrinsicValues.ToArray(),
                yDisplayIntrinsicValues.ToArray(),
                tValues.ToArray(), controlPoints);

            Debug.Log("Coordinates \t \t " + "x: " + xCoord + " y: " + yCoord);
            Debug.Log("HDRI pixel color: \t \t" + initialHDRIColor.ToString("F6"));
            Debug.Log("HDRI shaper pixel color: \t \t" + shapedHDRIColor.ToString("F6"));
            Debug.Log("HDRI pixel color * exposure: \t" + (initialHDRIColor * exposure).ToString("F6"));
            Debug.Log("Gamut mapped Color:  \t \t" + finalHDRIColor.ToString("F6"));
            Debug.Log("--------------------------------------------------------------------------------");
        }
    }

    private List<float> logInputColorPixelValues;
    private List<float> logOutputColorPixelValues;
    private List<Color> logPixelData;
    private List<Color> finalImage;
    public IEnumerator ApplyTransferFunction(RenderTexture inputRenderTexture)
    {
        int counter = maxIterationsPerFrame;
        int hdriPixelArrayLen = 0;

        float logHdriMaxRGBChannel = 0.0f;
        float hdriYMaxValue = 0.0f;

        Color ratio = Color.black;
        Color hdriPixelColor = Color.black;

        Vector3 hdriPixelColorVec = Vector3.zero;
        Vector3 maxDynamicRangeVec = Vector3.zero;

        float[] xCoordsArray;
        float[] yCoordsArray;
        float[] tValuesArray;


        Texture2D inputTexture = toTexture2D(inputRenderTexture);
        hdriPixelArray = inputTexture.GetPixels();

        saveDebugImageToDisk(inputTexture, "PreGamutMap_texture.exr");

        hdriPixelArrayLen = hdriPixelArray.Length;
        int quarterSize = hdriPixelArrayLen / 4;
        int halfSize = hdriPixelArrayLen / 2;
        int threeQuartersSize = hdriPixelArrayLen - quarterSize;

        logInputColorPixelValues = new List<float>(hdriPixelArrayLen);
        logOutputColorPixelValues= new List<float>(hdriPixelArrayLen);
        logPixelData = new List<Color>(hdriPixelArrayLen);
        finalImage = new List<Color>(hdriPixelArrayLen);
        if (tValues == null)
            yield return new WaitForEndOfFrame();

        Texture2D finalImageTexture = new Texture2D(inputRadiometricLinearTexture.width, inputRadiometricLinearTexture.height, TextureFormat.RGBAHalf, false, true);
        xCoordsArray = xCameraIntrinsicValues.ToArray();
        yCoordsArray = yDisplayIntrinsicValues.ToArray();
        tValuesArray = tValues.ToArray();

        counter = maxIterationsPerFrame;
        mainCamera.clearFlags = CameraClearFlags.Skybox;
        curveDataState = CurveDataState.Calculating;
        Color temp;
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

            // Sanitise values to make sure no negative numbers are used
            hdriPixelArray[i].r = Math.Max(0.0f, hdriPixelArray[i].r);
            hdriPixelArray[i].g = Math.Max(0.0f, hdriPixelArray[i].g);
            hdriPixelArray[i].b = Math.Max(0.0f, hdriPixelArray[i].b);

            // Debug
            temp = hdriPixelArray[i];
            temp.a = 1.0f;
            logPixelData.Add(temp);

            hdriPixelArray[i] = new Color(
                Shaper.calculateLog2ToLinear(hdriPixelArray[i].r, midGreySDR.x, minRadiometricExposure, maxRadiometricExposure),
                Shaper.calculateLog2ToLinear(hdriPixelArray[i].g, midGreySDR.x, minRadiometricExposure, maxRadiometricExposure),
                Shaper.calculateLog2ToLinear(hdriPixelArray[i].b, midGreySDR.x, minRadiometricExposure, maxRadiometricExposure));

            // Apply exposure
            hdriPixelArray[i] = hdriPixelArray[i] * Mathf.Pow(2.0f, exposure);

            // Shape image using Log2
            Color log2HdriPixelArray = new Color();
            log2HdriPixelArray.r = Shaper.calculateLinearToLog2(hdriPixelArray[i].r, midGreySDR.x, minRadiometricExposure, maxRadiometricExposure);
            log2HdriPixelArray.g = Shaper.calculateLinearToLog2(hdriPixelArray[i].g, midGreySDR.x, minRadiometricExposure, maxRadiometricExposure);
            log2HdriPixelArray.b = Shaper.calculateLinearToLog2(hdriPixelArray[i].b, midGreySDR.x, minRadiometricExposure, maxRadiometricExposure);

            // Calculate Pixel max color and ratio
            logHdriMaxRGBChannel = log2HdriPixelArray.maxColorComponent;
            Color linearHdriPixelColor = new Color(
                Shaper.calculateLog2ToLinear(log2HdriPixelArray.r, midGreySDR.x, minRadiometricExposure, maxRadiometricExposure),
                Shaper.calculateLog2ToLinear(log2HdriPixelArray.g, midGreySDR.x, minRadiometricExposure, maxRadiometricExposure),
                Shaper.calculateLog2ToLinear(log2HdriPixelArray.b, midGreySDR.x, minRadiometricExposure, maxRadiometricExposure));

            if (activeGamutMappingMode == GamutMappingMode.Max_RGB)
            {
                // Retrieve the maximum RGB value but in linear space
                float linearHdriMaxRGBChannel = Shaper.calculateLog2ToLinear(logHdriMaxRGBChannel, midGreySDR.x, minRadiometricExposure, maxRadiometricExposure);

                //Debug data
                logInputColorPixelValues.Add(logHdriMaxRGBChannel);

                // Calculate the ratio in linear space
                ratio = linearHdriPixelColor / linearHdriMaxRGBChannel;

                // Secondary Nuance Grade, guardrails
                if (linearHdriPixelColor.r > maxRadiometricValue ||
                    linearHdriPixelColor.g > maxRadiometricValue ||
                    linearHdriPixelColor.b > maxRadiometricValue)
                {
                    linearHdriPixelColor.r = maxRadiometricValue;
                    linearHdriPixelColor.g = maxRadiometricValue;
                    linearHdriPixelColor.b = maxRadiometricValue;
                }

                // Get Y value from curve by retrieving the respective value from the x coordinate array
                float yValue = parametricGamutCurve.getYCoordinateLogXInput(logHdriMaxRGBChannel, xCoordsArray, yCoordsArray, tValuesArray, controlPoints);
                yValue = Shaper.calculateLog2ToLinear(yValue, midGreySDR.y, minDisplayExposure, maxDisplayExposure);
                logOutputColorPixelValues.Add(yValue);
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

            hdriPixelArray[i].r = hdriPixelColor.r;
            hdriPixelArray[i].g = hdriPixelColor.g;
            hdriPixelArray[i].b = hdriPixelColor.b;
            hdriPixelArray[i].a = 1.0f;

            Color finalImageColour = new Color();
            finalImageColour.r = TransferFunction.ApplyInverseTransferFunction(hdriPixelArray[i].r, TransferFunction.TransferFunctionType.sRGB_2PartFunction);
            finalImageColour.g = TransferFunction.ApplyInverseTransferFunction(hdriPixelArray[i].g, TransferFunction.TransferFunctionType.sRGB_2PartFunction);
            finalImageColour.b = TransferFunction.ApplyInverseTransferFunction(hdriPixelArray[i].b, TransferFunction.TransferFunctionType.sRGB_2PartFunction);
            finalImageColour.a = 1.0f;
            finalImage.Add(finalImageColour);
        }

        // Make sure the result should be written out to the texture
        if (curveDataState == CurveDataState.Dirty)
        {
            yield break;
        }

        hdriTextureTransformed.SetPixels(hdriPixelArray);
        hdriTextureTransformed.Apply();

        // Write texture to disk
        saveDebugImageToDisk(hdriTextureTransformed, "PostGamutMap_DisplayLinear.exr");

        finalImageTexture.SetPixels(finalImage.ToArray());
        finalImageTexture.Apply();

        saveDebugImageToDisk(finalImageTexture, "PostGamutMap_DisplayLinearWithInverseEOTF.exr");

        // Save Log encoded data
        Texture2D logImageToSave = new Texture2D(inputRadiometricLinearTexture.width, inputRadiometricLinearTexture.height, TextureFormat.RGBAHalf, false, true);
        logImageToSave.SetPixels(logPixelData.ToArray());
        logImageToSave.Apply();
        saveDebugImageToDisk(logImageToSave, "ColorGradeResult_CameraIntrinsicResult.exr");

        curveDataState = CurveDataState.Calculated;
        mainCamera.clearFlags = CameraClearFlags.Nothing;
        Debug.Log("Image Processing has finished");
    }

    private Color calculateGamutCompression(Color linearHdriPixelColor, Color inRatio ,
        float[] xCoordsArray, float[] yCoordsArray, float[] tValuesArray)
    {
        float gamutCompressionXCoordLinear;
        float gamutCompressionRange;
        float gamutCompressionRatio;
        Color ratio = inRatio;
        gamutCompressionXCoordLinear = 0.0f; // Intersect of x on Y = 1

        // Calculate gamut compression values by iterating through the Y values array and returning the closest x coord
        gamutCompressionXCoordLinear = Shaper.calculateLog2ToLinear(chromaticityMaxLatitude, midGreySDR.x, minRadiometricExposure, maxRadiometricExposure);

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

    public Color[] ApplyChromaticityCompression(Color[] linearRadiometricInputPixels, bool outputInCameraIntrinsic)
    {
        Vector3 colorVec = Vector3.zero;
        Color[] outputColorBuffer = new Color[linearRadiometricInputPixels.Length];

        float[] xCameraIntrinsicArray = xCameraIntrinsicValues.ToArray();
        float[] yDisplayIntrinsicArray = yDisplayIntrinsicValues.ToArray();
        float[] tValuesArray = tValues.ToArray();

        Color linearPixelColor = new Color();
        Color ratio = Color.white;
        for (int index = 0; index < linearRadiometricInputPixels.Length; index++)
        {
            linearPixelColor.r = Math.Max(0.0f, linearRadiometricInputPixels[index].r);
            linearPixelColor.g = Math.Max(0.0f, linearRadiometricInputPixels[index].g);
            linearPixelColor.b = Math.Max(0.0f, linearRadiometricInputPixels[index].b);
            float maxLinearPixelColor = linearPixelColor.maxColorComponent;
            ratio = linearPixelColor / maxLinearPixelColor;

            if (maxLinearPixelColor > 0.0f)
            {
                ratio = calculateGamutCompression(linearPixelColor, ratio, xCameraIntrinsicArray, yDisplayIntrinsicArray, tValuesArray);
                linearPixelColor = maxLinearPixelColor * ratio;
            }

            if (outputInCameraIntrinsic == true)
            {
                outputColorBuffer[index].r = Shaper.calculateLinearToLog2(linearPixelColor.r, MidGreySdr.x,
                    MinRadiometricExposure, MaxRadiometricExposure);
                outputColorBuffer[index].g = Shaper.calculateLinearToLog2(linearPixelColor.g, MidGreySdr.x,
                    MinRadiometricExposure, MaxRadiometricExposure);
                outputColorBuffer[index].b = Shaper.calculateLinearToLog2(linearPixelColor.b, MidGreySdr.x,
                    MinRadiometricExposure, MaxRadiometricExposure);
            } else 
            {
                outputColorBuffer[index].r = linearPixelColor.r;
                outputColorBuffer[index].g = linearPixelColor.g;
                outputColorBuffer[index].b = linearPixelColor.b;
            }
            outputColorBuffer[index].a = 1.0f;
        }

        return outputColorBuffer;
    }

    public void saveInGameCapture(string saveFilePath)
    {
        Vector3 colorVec = Vector3.zero;
        Color[] outputColorBuffer = ApplyChromaticityCompression(inputRadiometricLinearTexture.GetPixels(), true);
        SaveToDisk(outputColorBuffer, saveFilePath, inputRadiometricLinearTexture.width, inputRadiometricLinearTexture.height);
    }

    public void CalculateTransferTransform(ref Color[] input3DLut)
    {
        // X -> camera intrinsic encoding (camera negative)
        // Y -> display intrinsic (display negative)
        // get x values and y values arrays
        // get aesthetic curve in display intrinsic from camera intrisinc encoding (x camera axis)
        // go from display intrinsic to display linear
        //                  Shaper.calculateLog2toLinear(yVal, midGreySDR.y, minDisplayExposure, maxDisplayExposure);
        // go from display linear to display inverse EOTF encoded
        Color logPixelColor = new Color();
        Color linearPixelColor = new Color();

        float[] xCameraIntrinsicArray = xCameraIntrinsicValues.ToArray();
        float[] yDisplayIntrinsicArray = yDisplayIntrinsicValues.ToArray();
        float[] tValuesArray = tValues.ToArray();

        for (int index = 0; index < input3DLut.Length; index++)
        {
            logPixelColor = input3DLut[index];
            float maxLogPixelColor = logPixelColor.maxColorComponent;

            linearPixelColor.r = Shaper.calculateLog2ToLinear(logPixelColor.r, midGreySDR.x, minRadiometricExposure, maxRadiometricExposure);
            linearPixelColor.g = Shaper.calculateLog2ToLinear(logPixelColor.g, midGreySDR.x, minRadiometricExposure, maxRadiometricExposure);
            linearPixelColor.b = Shaper.calculateLog2ToLinear(logPixelColor.b, midGreySDR.x, minRadiometricExposure, maxRadiometricExposure);

            float maxLinearPixelColor = linearPixelColor.maxColorComponent;
            Color ratio = linearPixelColor / maxLinearPixelColor;

            float displayIntrinsicValue = parametricGamutCurve.getYCoordinateLogXInput(maxLogPixelColor, xCameraIntrinsicArray, yDisplayIntrinsicArray, tValuesArray, controlPoints);
            float displayLinearValue = Shaper.calculateLog2ToLinear(displayIntrinsicValue, midGreySDR.y, minDisplayExposure, maxDisplayExposure);
            displayLinearValue = Mathf.Min(displayLinearValue, 1.0f);
            ratio.a = 1.0f;
            linearPixelColor = displayLinearValue * ratio;

            linearPixelColor.r = TransferFunction.ApplyInverseTransferFunction(linearPixelColor.r,TransferFunction.TransferFunctionType.sRGB_2PartFunction);
            linearPixelColor.g = TransferFunction.ApplyInverseTransferFunction(linearPixelColor.g,TransferFunction.TransferFunctionType.sRGB_2PartFunction);
            linearPixelColor.b = TransferFunction.ApplyInverseTransferFunction(linearPixelColor.b,TransferFunction.TransferFunctionType.sRGB_2PartFunction);

            input3DLut[index] = linearPixelColor;
        }
    }


    public void exportTransferFunction(string filePath)
    {
        // Set the DOMAIN_MIN and DOMAIN_MAX ranges
        Vector3 minCameraNativeVec = /*Vector3.zero;*/new Vector3(xCameraIntrinsicValues[0], xCameraIntrinsicValues[0], xCameraIntrinsicValues[0]);
        Vector3 maxCameraNativeVec = /*Vector3.one;*/ new Vector3(xCameraIntrinsicValues[xCameraIntrinsicValues.Count - 1], xCameraIntrinsicValues[xCameraIntrinsicValues.Count - 1], xCameraIntrinsicValues[xCameraIntrinsicValues.Count - 1]);

        const int lutDimension = 33;
        Color[] identity3DLut = LutGenerator.generateIdentityCubeLUT(lutDimension);
        CalculateTransferTransform(ref identity3DLut);

        CubeLutExporter.saveLutAsCube(identity3DLut, filePath, lutDimension, minCameraNativeVec, maxCameraNativeVec, true);
    }

    public void SaveToDisk(Color[] pixels, string fileName, int width, int height)
    {
        Debug.Log("Preparing to save image to disk");

        Texture2D textureToSave = new Texture2D(width, height, TextureFormat.RGBAHalf, false, true);
        textureToSave.SetPixels(pixels);
        textureToSave.Apply();
        File.WriteAllBytes(@fileName, textureToSave.EncodeToEXR());

        Debug.Log("Successfully saved " + fileName + " to disk");
    }

    [Conditional("DEBUG_IMAGES")]
    private void saveDebugImageToDisk(Texture2D imageToSave, string fileName)
    {
        string debugDataPath = "DebugData/";
        File.WriteAllBytes(debugDataPath + fileName, imageToSave.EncodeToEXR());
    }
    [Conditional("DEBUG_CURVE_DATA")]
    private void exportDataToCSV(float[][] dataToExport, string fileName)
    {
        if (dataToExport.Length % 2 != 0)
        {
            Debug.LogError("Odd number of arrays being loaded in exportDataToCSV");
            return;
        }
        StringBuilder strBuilder = new StringBuilder(dataToExport[0].Length);

        int arrayLen = dataToExport[0].Length;
        for (int j = 0; j < arrayLen; j++)
        {
            for (int i = 0; i < dataToExport.Length; i++)
            {
                strBuilder.Append(dataToExport[i][j].ToString() + " , ");
            }

            strBuilder.AppendLine("");
        }

        File.WriteAllText(fileName, strBuilder.ToString());
        Debug.Log("Successfully saved the file " + fileName + " to disk");
    }

    [Conditional("DEBUG_CURVE_DATA")]
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
    [Conditional("DEBUG_CURVE_DATA")]

    private void exportSingleColumnDataToCSV(float[] dataToExport, string fileName)
    {
        StringBuilder strBuilder = new StringBuilder(dataToExport.Length);
        foreach (var value in dataToExport)
        {
            strBuilder.AppendLine(dataToExport.ToString() + ",");
        }

        File.WriteAllText(fileName, strBuilder.ToString());
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
        greyPointX = midGreySDR.x;
        greyPointY = midGreySDR.y;
    }

    public void setParametricCurveValues(float inSlope, float originPointX, float originPointY,
        float greyPointX, float greyPointY)
    {
        this.slope = inSlope;
        this.origin.x = originPointX;
        this.origin.y = originPointY;
        this.midGreySDR.x = greyPointX;
        this.midGreySDR.y = greyPointY;

        SetCurveDataState(CurveDataState.Dirty);
        createParametricCurve(midGreySDR, origin);
    }

    public bool getShowSweep()
    {
        return isSweepActive;
    }

    public void setShowSweep(bool isActive, Texture2D inputTexture)
    {
        isSweepActive = isActive;
        this.inputRadiometricLinearTexture = inputTexture;
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

    public Texture2D toTexture2D(RenderTexture rTex)
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
            midGreySDR = new Vector2(0.18f, 0.18f);
            slope = 2.2f;
            minRadiometricValue = Mathf.Pow(2.0f, -6.0f) * midGreySDR.x;
            maxRadiometricValue = Mathf.Pow(2.0f, 6.0f) * midGreySDR.x;
            origin = new Vector2(minRadiometricValue, 0.00001f);

            createParametricCurve(midGreySDR, origin);
        }

        return controlPoints;
    }



    public List<float> getTValues()
    {
        return tValues;
    }

    public List<float> getXValues()
    {
        return xCameraIntrinsicValues;
    }

    public List<float> getYValues()
    {
        return yDisplayIntrinsicValues;
    }

    public GamutCurve getParametricCurve()
    {
        if (parametricGamutCurve == null)
            createParametricCurve(midGreySDR, origin);

        return parametricGamutCurve;
    }

    public void setInputTexture(Texture2D inputTexture)
    {
        this.inputRadiometricLinearTexture = inputTexture;
        SetCurveDataState(CurveDataState.Dirty);
    }

    public void setChromaticityMaxLatitude(float latitudeLimit)
    {
        chromaticityMaxLatitude = latitudeLimit;
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
        curveCoordCoordinateMaxLatitude = curveParams.curveCoordMaxLatitude;
        chromaticityMaxLatitude = curveParams.chromaticitydMaxLatitude;
        curveDataState = GamutMap.CurveDataState.Dirty;
        createParametricCurve(midGreySDR, origin);
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

    public static bool FastApproximately(float a, float b, float threshold)
    {
        return ((a - b) < 0 ? ((a - b) * -1) : (a - b)) <= threshold;
    }
}
