using System;
using System.Collections.Generic;
using UnityEngine;
using Debug = UnityEngine.Debug;


/// <summary>
/// Class responsible for performing all the gamut mapping transformations as well as holding
/// the aesthetic transfer function curve.
/// </summary>
public class GamutMap
{
    #region Public Properties
    public float SlopeMax => slopeMax;
    public float SlopeMin => slopeMin;
    public float MinRadiometricExposure => minRadiometricExposure;
    public float MaxRadiometricExposure => maxRadiometricExposure;
    public float MaxRadiometricValue => maxRadiometricValue;
    public float MaxDisplayValue => maxDisplayValue;
    public float MinDisplayValue => minDisplayValue;
    public float MinDisplayExposure => minDisplayExposure;
    public float MaxDisplayExposure => maxDisplayExposure;
    public int CurveLutLength => curveLutLength;
    public Vector2 MidGreySdr => midGreySDR;
    public List<float> XCameraIntrinsicValues => xCameraIntrinsicValues;
    public List<float> YDisplayIntrinsicValues => yDisplayIntrinsicValues;
    public float Exposure
    {
        get => exposure;
        set => exposure = value;
    }

    public float ChromaticityMaxLatitude
    {
        get => chromaticityMaxLatitude;
        set => chromaticityMaxLatitude = value;
    }

    public float CurveCoordMaxLatitude
    {
        get => curveCoordCoordinateMaxLatitude;
        set => curveCoordCoordinateMaxLatitude = value;
    }
    public float Slope
    {
        get => slope;
        set => slope = value;
    }
    #endregion

    private float slope;
    private float exposure;
    private float slopeMin;
    private float slopeMax;
    private float minRadiometricValue;
    private float minRadiometricExposure;
    private float maxRadiometricExposure;
    private float maxRadiometricValue;
    private float maxDisplayValue;
    private float maxDisplayExposure;
    private float minDisplayValue;
    private float minDisplayExposure;
    private float maxRadiometricLatitude;
    private float curveCoordCoordinateMaxLatitude;
    private float chromaticityMaxLatitude;
    private float maxRadiometricLatitudeExposure;
    private float totalRadiometricExposure;
    private float maxNits;
    private int curveLutLength = 1024;

    private Vector2 midGreySDR;
    private Vector2[] controlPoints;
    private Color[] hdriPixelArray;

    private Vector2 curveOrigin;
    private GamutCurve parametricGamutCurve = null;

    private List<float> tValues;
    private List<float> xCameraIntrinsicValues;
    private List<float> yDisplayIntrinsicValues;

    public GamutMap() { }

    public void Init()
    {
        exposure = 0.0f;
        curveLutLength = 1024;

        // Parametric curve
        slope = 1.7f;
        slopeMin = 1.02f;
        slopeMax = 6.5f;
        maxNits = 100.0f; // Maximum nit value we support
                          // Max and min display values are unit agnostic
        maxDisplayValue = 100.0f / maxNits; // in SDR we support a maximum of 100 nits
        minDisplayValue =
            0.05f / maxNits; // in SDR we support a minimum of 0.05f nits which is an average black value for a LED display
        midGreySDR = new Vector2(18.0f / maxNits, 18.0f / maxNits);
        minRadiometricExposure = -7.0f;
        maxRadiometricExposure = 5.7f;
        totalRadiometricExposure = maxRadiometricExposure - minRadiometricExposure;

        minDisplayExposure = Mathf.Log(minDisplayValue / midGreySDR.y, 2.0f);
        maxDisplayExposure = Mathf.Log(maxDisplayValue / midGreySDR.y, 2.0f);

        minRadiometricValue = Mathf.Pow(2.0f, minRadiometricExposure) * midGreySDR.x;
        maxRadiometricValue = Mathf.Pow(2.0f, maxRadiometricExposure) * midGreySDR.x;

        chromaticityMaxLatitude = 0.85f;
        curveCoordCoordinateMaxLatitude = 0.95f; // value in camera encoded log2/EV
        maxRadiometricLatitudeExposure = totalRadiometricExposure * curveCoordCoordinateMaxLatitude;
        maxRadiometricLatitude = Shaper.CalculateLog2ToLinear(curveCoordCoordinateMaxLatitude, midGreySDR.x,
            minRadiometricExposure, maxRadiometricExposure);

        curveOrigin = new Vector2(minRadiometricValue, minDisplayValue);
        CreateParametricCurve(midGreySDR, curveOrigin);
    }

    public void CreateParametricCurve(Vector2 greyPoint, Vector2 origin)
    {
        maxRadiometricLatitudeExposure = totalRadiometricExposure * curveCoordCoordinateMaxLatitude;
        maxRadiometricLatitude = Shaper.CalculateLog2ToLinear(curveCoordCoordinateMaxLatitude, midGreySDR.x,
            minRadiometricExposure, maxRadiometricExposure);

        if (parametricGamutCurve == null)
            parametricGamutCurve = new GamutCurve(minRadiometricExposure, maxRadiometricExposure,
                maxRadiometricValue, maxDisplayValue,
                minDisplayExposure, maxDisplayExposure, maxRadiometricLatitude, maxRadiometricLatitudeExposure,
                curveCoordCoordinateMaxLatitude);

        controlPoints = parametricGamutCurve.CreateControlPoints(origin, this.midGreySDR, slope);
        xCameraIntrinsicValues = InitialiseXCoordsInRange(curveLutLength);
        tValues = parametricGamutCurve.CalcTfromXquadratic(xCameraIntrinsicValues.ToArray(), controlPoints);
        yDisplayIntrinsicValues = parametricGamutCurve.CalcYfromXQuadratic(xCameraIntrinsicValues, tValues, new List<Vector2>(controlPoints));
    }

    public void RecalculateParametricCurve()
    {
        totalRadiometricExposure = maxRadiometricExposure - minRadiometricExposure;

        minDisplayExposure = Mathf.Log(minDisplayValue / midGreySDR.y, 2.0f);
        maxDisplayExposure = Mathf.Log(maxDisplayValue / midGreySDR.y, 2.0f);

        minRadiometricValue = Mathf.Pow(2.0f, minRadiometricExposure) * midGreySDR.x;
        maxRadiometricValue = Mathf.Pow(2.0f, maxRadiometricExposure) * midGreySDR.x;
        maxRadiometricLatitudeExposure = totalRadiometricExposure * curveCoordCoordinateMaxLatitude;
        maxRadiometricLatitude = Shaper.CalculateLog2ToLinear(curveCoordCoordinateMaxLatitude, midGreySDR.x, minRadiometricExposure, maxRadiometricExposure);
        maxRadiometricLatitudeExposure = totalRadiometricExposure * curveCoordCoordinateMaxLatitude;
        maxRadiometricLatitude = Shaper.CalculateLog2ToLinear(curveCoordCoordinateMaxLatitude, midGreySDR.x, minRadiometricExposure, maxRadiometricExposure);

        curveOrigin = new Vector2(minRadiometricValue, minDisplayValue);

        parametricGamutCurve = new GamutCurve(minRadiometricExposure, maxRadiometricExposure, maxRadiometricValue, maxDisplayValue,
            minDisplayExposure, maxDisplayExposure, maxRadiometricLatitude, maxRadiometricLatitudeExposure, curveCoordCoordinateMaxLatitude);

        controlPoints = parametricGamutCurve.CreateControlPoints(curveOrigin, this.midGreySDR, slope);
        xCameraIntrinsicValues = InitialiseXCoordsInRange(curveLutLength);
        tValues = parametricGamutCurve.CalcTfromXquadratic(xCameraIntrinsicValues.ToArray(), controlPoints);
        yDisplayIntrinsicValues = parametricGamutCurve.CalcYfromXQuadratic(xCameraIntrinsicValues, tValues, new List<Vector2>(controlPoints));

    }

    /// <summary>
    ///  Initialises an array of values equidistant from each other within the [0-1] range
    /// </summary>
    /// <param name="dimension"> Array length. Normally the same as our LUT length</param>
    /// <returns> List with coordinates in a normalised [0-1] domain</returns>
    public List<float> InitialiseXCoordsInRange(int lutDimension)
    {
        List<float> xCameraIntrinsicValues = new List<float>(lutDimension);

        for (int i = 0; i < lutDimension; ++i)
        {
            xCameraIntrinsicValues.Add(((float)i / (float)(lutDimension - 1)));
        }

        return xCameraIntrinsicValues;
    }

    public Texture2D ApplyTransferFunctionTo2DSlice(RenderTexture inputRenderTexture)
    {
        float logHdriMaxRGBChannel = 0.0f;
        float hdriYMaxValue = 0.0f;

        Color ratio = Color.black;
        Color hdriPixelColor = Color.black;

        float[] xCoordsArray = xCameraIntrinsicValues.ToArray();
        float[] yCoordsArray = yDisplayIntrinsicValues.ToArray();
        float[] tValuesArray = tValues.ToArray();

        Texture2D inputTexture = ToTexture2D(inputRenderTexture);
        hdriPixelArray = inputTexture.GetPixels();
        int hdriPixelArrayLen = hdriPixelArray.Length;

        Texture2D finalImageTexture = new Texture2D(inputTexture.width, inputTexture.height, TextureFormat.RGBAHalf,
            false, true);

        for (int i = 0; i < hdriPixelArrayLen; i++)
        {
            ratio = Color.blue;

            // Assumption: texture content is already in Log2
            hdriPixelArray[i] = new Color(
                Shaper.CalculateLog2ToLinear(hdriPixelArray[i].r, midGreySDR.x, minRadiometricExposure,
                    maxRadiometricExposure),
                Shaper.CalculateLog2ToLinear(hdriPixelArray[i].g, midGreySDR.x, minRadiometricExposure,
                    maxRadiometricExposure),
                Shaper.CalculateLog2ToLinear(hdriPixelArray[i].b, midGreySDR.x, minRadiometricExposure,
                    maxRadiometricExposure));

            // Apply exposure
            hdriPixelArray[i] = hdriPixelArray[i] * Mathf.Pow(2.0f, exposure);

            // Shape image using Log2
            Color log2HdriPixelArray = new Color();
            log2HdriPixelArray.r = Shaper.CalculateLinearToLog2(hdriPixelArray[i].r, midGreySDR.x,
                minRadiometricExposure, maxRadiometricExposure);
            log2HdriPixelArray.g = Shaper.CalculateLinearToLog2(hdriPixelArray[i].g, midGreySDR.x,
                minRadiometricExposure, maxRadiometricExposure);
            log2HdriPixelArray.b = Shaper.CalculateLinearToLog2(hdriPixelArray[i].b, midGreySDR.x,
                minRadiometricExposure, maxRadiometricExposure);

            // Calculate Pixel max color and ratio
            logHdriMaxRGBChannel = log2HdriPixelArray.maxColorComponent;
            Color linearHdriPixelColor = new Color(
                Shaper.CalculateLog2ToLinear(log2HdriPixelArray.r, midGreySDR.x, minRadiometricExposure,
                    maxRadiometricExposure),
                Shaper.CalculateLog2ToLinear(log2HdriPixelArray.g, midGreySDR.x, minRadiometricExposure,
                    maxRadiometricExposure),
                Shaper.CalculateLog2ToLinear(log2HdriPixelArray.b, midGreySDR.x, minRadiometricExposure,
                    maxRadiometricExposure));

            // Retrieve the maximum RGB value but in linear space
            float linearHdriMaxRGBChannel = Shaper.CalculateLog2ToLinear(logHdriMaxRGBChannel, midGreySDR.x,
                minRadiometricExposure, maxRadiometricExposure);

            // Calculate the ratio in linear space
            ratio = linearHdriPixelColor / linearHdriMaxRGBChannel;

            // Get Y value from curve by retrieving the respective value from the x coordinate array
            float yValue = parametricGamutCurve.GetYCoordinateLogXInput(logHdriMaxRGBChannel, xCoordsArray,
                yCoordsArray, tValuesArray, controlPoints);
            yValue = Shaper.CalculateLog2ToLinear(yValue, midGreySDR.y, minDisplayExposure, maxDisplayExposure);
            hdriYMaxValue = Mathf.Min(yValue, 1.0f);
            ratio.a = 1.0f;
            hdriPixelColor = hdriYMaxValue * ratio;

            hdriPixelColor.r = RemapValueTo(hdriPixelColor.r, minDisplayValue, maxDisplayValue, 0.0f, 1.0f);
            hdriPixelColor.g = RemapValueTo(hdriPixelColor.g, minDisplayValue, maxDisplayValue, 0.0f, 1.0f);
            hdriPixelColor.b = RemapValueTo(hdriPixelColor.b, minDisplayValue, maxDisplayValue, 0.0f, 1.0f);

            hdriPixelArray[i].r = hdriPixelColor.r;
            hdriPixelArray[i].g = hdriPixelColor.g;
            hdriPixelArray[i].b = hdriPixelColor.b;
            hdriPixelArray[i].a = 1.0f;
        }

        finalImageTexture.SetPixels(hdriPixelArray);
        finalImageTexture.Apply();

        Debug.Log("Image Processing has finished");

        return finalImageTexture;
    }

    private Color CalculateGamutCompression(Color linearHdriPixelColor, Color inRatio)
    {
        float gamutCompressionXCoordLinear;
        float gamutCompressionRange;
        float gamutCompressionRatio;
        Color ratio = inRatio;
        gamutCompressionXCoordLinear = 0.0f; // Intersect of x on Y = 1

        // Calculate gamut compression values by iterating through the Y values array and returning the closest x coord
        gamutCompressionXCoordLinear = Shaper.CalculateLog2ToLinear(chromaticityMaxLatitude, midGreySDR.x,
            minRadiometricExposure, maxRadiometricExposure);

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
        }

        return ratio;
    }

    public Color[] ApplyChromaticityCompressionCPU(Color[] linearRadiometricInputPixels)
    {
        Vector3 colorVec = Vector3.zero;
        Color[] outputColorBuffer = new Color[linearRadiometricInputPixels.Length];

        Color linearPixelColor = new Color();
        Color ratio = Color.white;
        for (int index = 0; index < linearRadiometricInputPixels.Length; index++)
        {
            // Secondary bottom nuance grade, lower end guardrails
            linearPixelColor.r = Math.Max(0.0f, linearRadiometricInputPixels[index].r);
            linearPixelColor.g = Math.Max(0.0f, linearRadiometricInputPixels[index].g);
            linearPixelColor.b = Math.Max(0.0f, linearRadiometricInputPixels[index].b);

            linearPixelColor.r = Math.Min(maxRadiometricValue, linearPixelColor.r);
            linearPixelColor.g = Math.Min(maxRadiometricValue, linearPixelColor.g);
            linearPixelColor.b = Math.Min(maxRadiometricValue, linearPixelColor.b);

            float maxLinearPixelColor = linearPixelColor.maxColorComponent;
            ratio = linearPixelColor / maxLinearPixelColor;

            if (maxLinearPixelColor > 0.0f)
            {
                ratio = CalculateGamutCompression(linearPixelColor, ratio);
                linearPixelColor = maxLinearPixelColor * ratio;
            }

            outputColorBuffer[index].r = Shaper.CalculateLinearToLog2(linearPixelColor.r, MidGreySdr.x,
                MinRadiometricExposure, MaxRadiometricExposure);
            outputColorBuffer[index].g = Shaper.CalculateLinearToLog2(linearPixelColor.g, MidGreySdr.x,
                MinRadiometricExposure, MaxRadiometricExposure);
            outputColorBuffer[index].b = Shaper.CalculateLinearToLog2(linearPixelColor.b, MidGreySdr.x,
                MinRadiometricExposure, MaxRadiometricExposure);

            outputColorBuffer[index].a = 1.0f;
        }

        return outputColorBuffer;
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

            linearPixelColor.r = Shaper.CalculateLog2ToLinear(logPixelColor.r, midGreySDR.x, minRadiometricExposure,
                maxRadiometricExposure);
            linearPixelColor.g = Shaper.CalculateLog2ToLinear(logPixelColor.g, midGreySDR.x, minRadiometricExposure,
                maxRadiometricExposure);
            linearPixelColor.b = Shaper.CalculateLog2ToLinear(logPixelColor.b, midGreySDR.x, minRadiometricExposure,
                maxRadiometricExposure);

            float maxLinearPixelColor = linearPixelColor.maxColorComponent;
            Color ratio = linearPixelColor / maxLinearPixelColor;

            float displayIntrinsicValue = parametricGamutCurve.GetYCoordinateLogXInput(maxLogPixelColor,
                xCameraIntrinsicArray, yDisplayIntrinsicArray, tValuesArray, controlPoints);
            float displayLinearValue = Shaper.CalculateLog2ToLinear(displayIntrinsicValue, midGreySDR.y,
                minDisplayExposure, maxDisplayExposure);
            displayLinearValue = Mathf.Min(displayLinearValue, 1.0f);
            ratio.a = 1.0f;
            linearPixelColor = displayLinearValue * ratio;

            linearPixelColor.r = TransferFunction.ApplyInverseTransferFunction(linearPixelColor.r,
                TransferFunction.TransferFunctionType.sRGB_2PartFunction);
            linearPixelColor.g = TransferFunction.ApplyInverseTransferFunction(linearPixelColor.g,
                TransferFunction.TransferFunctionType.sRGB_2PartFunction);
            linearPixelColor.b = TransferFunction.ApplyInverseTransferFunction(linearPixelColor.b,
                TransferFunction.TransferFunctionType.sRGB_2PartFunction);

            input3DLut[index] = linearPixelColor;
        }
    }

    public void GetParametricCurveValues(out float inSlope, out float originPointX, out float originPointY,
        out float greyPointX, out float greyPointY)
    {
        inSlope = slope;
        originPointX = curveOrigin.x;
        originPointY = curveOrigin.y;
        greyPointX = midGreySDR.x;
        greyPointY = midGreySDR.y;
    }

    private Texture2D ToTexture2D(RenderTexture rTex)
    {
        Texture2D tex = new Texture2D(rTex.width, rTex.height, TextureFormat.RGBAHalf, false, true);
        RenderTexture.active = rTex;
        tex.ReadPixels(new Rect(0, 0, rTex.width, rTex.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        return tex;
    }

    public Vector2[] GetControlPoints()
    {
        // TODO: Replace with curveStateData state
        if (controlPoints == null || controlPoints.Length == 0)
        {
            midGreySDR = new Vector2(0.18f, 0.18f);
            slope = 2.2f;
            minRadiometricValue = Mathf.Pow(2.0f, -6.0f) * midGreySDR.x;
            maxRadiometricValue = Mathf.Pow(2.0f, 6.0f) * midGreySDR.x;
            curveOrigin = new Vector2(minRadiometricValue, 0.00001f);

            CreateParametricCurve(midGreySDR, curveOrigin);
        }

        return controlPoints;
    }

    float RemapValueTo(float value, float min0, float max0, float min1, float max1)
    {
        return min1 + (value - min0) * ((max1 - min1) / (max0 - min0));
    }

#if UNITY_EDITOR
    public void SetCurveParams(CurveParams curveParams)
    {
        exposure = curveParams.exposure;
        slope = curveParams.slope;
        curveOrigin.x = curveParams.originX;
        curveOrigin.y = curveParams.originY;
        curveCoordCoordinateMaxLatitude = curveParams.curveCoordMaxLatitude;
        chromaticityMaxLatitude = curveParams.chromaticitydMaxLatitude;
        CreateParametricCurve(midGreySDR, curveOrigin);
    }
#endif

}
