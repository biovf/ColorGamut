using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using MathNet.Numerics;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Assertions.Comparers;
using Debug = UnityEngine.Debug;

public enum TransferFunction
{
    Per_Channel,
    Max_RGB
}

[ExecuteInEditMode]
public class ColorGamut : MonoBehaviour
{
    public Material colorGamutMat;
    public Material fullScreenTextureMat;
    public GameObject sweepPlane;
    public Texture2D sweepTexture;
    public List<Texture2D> HDRIList;
    
    [Header("Dye Bleaching")]
    public bool enableDyeBleaching;
    public float dye_bleach_x = 1.0f;
    public float dye_bleach_y = 1.0f;
   
    private float exposure;

    // [Range(0.01f, 20.0f)]
    // public float sweepExposure;
    private TransferFunction activeTransferFunction;
    
    private Texture2D inputTexture;

    private bool isSweepActive;
    private bool isBleachingActive;
    private bool isMultiThreaded = false;
    private bool showPixelsOutOfGamut = false;
    private bool onGuiChanged = true;

    private int hdriIndex;
    private int inputTextureIdx = 0;
    private int lutLength;
    private int yIndexIntersect = 0;
    private const int maxIterationsPerFrame = 100000;

    private Texture2D hdriTextureTransformed;
    private Texture2D sweepTextureTransformed;
    private Texture2D textureToSave;
    private RenderTexture screenGrab;

    // private Ggm_troyedition ggm;
    private AnimationCurve animationCurve;
    private Color[] hdriPixelArray;
    private Vector2[] animationCurveLUT;
    private string logOutput = "";
    
    // Parametric curve variables
 
    private float slope;
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

    public float MaxRadiometricValue => maxRadiometricValue;
    private float maxRadiometricValue;
    private float maxDisplayValue;
    private float minDisplayValue;
    
    private Vector2 greyPoint;
    private List<float> xValues;
    private List<float> yValues;

    public int CurveValueLutDim => curveValueLutDim;
    private int curveValueLutDim;

    private enum ColorRange
    {
        InsideGamut,
        BelowGamut,
        AboveGamut
    };
    private ColorRange colorRange;

    private enum CurveDataState
    {
        NotCalculated,
        Calculated,
        MustRecalculate
    };

    private CurveDataState curveDataState = CurveDataState.NotCalculated;
    private Camera mainCamera;
    private void Awake()
    {
        activeTransferFunction = TransferFunction.Max_RGB;
    }

    void Start()
    {
        hdriIndex = 0;
        exposure = 1.0f;
        // sweepExposure = 1.0f;

        isBleachingActive = true;
        isSweepActive     = false;
        onGuiChanged      = true;
        enableDyeBleaching = false;
        
        // Parametric curve
        slope = 2.2f;
        slopeMin = 1.02f;
        slopeMax = 4.5f;
        maxDisplayValue = 1.5f;
        minDisplayValue = 0.0f;
        greyPoint = new Vector2(0.18f, 0.18f);
        minRadiometricValue = Mathf.Pow(2.0f, -6.0f) * greyPoint.x;
        maxRadiometricValue = Mathf.Pow(2.0f,  6.0f) * greyPoint.x;
        origin = new Vector2(minRadiometricValue, 0.00001f);
        curveValueLutDim = 1024;
        createParametricCurve(greyPoint, origin);
        
        /***
         * Run Tests here
         */
        // List<float> xVal = new List<float>()
        // {
        //     0.886f, 0.896f, 1.001f, // values are mapping to 0.181, 0.181, 0.181
        //     0.087f, 0.173f, 0.335f, // values are mapping to 0.000, 0.085, 0.181
        //     0.040f, 0.097f, 0.207f, // values are mapping to 0.000, 0.000, 0.181
        // };
        //
        // List<float> yValues = parametricCurve.calcYfromXQuadratic(xVal, tValues,
        //     new List<Vector2>(controlPoints), xValues);
    
        
        if (HDRIList == null)
            Debug.LogError("HDRIs list is empty");

        inputTexture  = HDRIList[hdriIndex];
        
        hdriPixelArray          = new Color[inputTexture.width * inputTexture.height];
        hdriTextureTransformed  = new Texture2D(inputTexture.width, inputTexture.height, TextureFormat.RGBAHalf, false);
        textureToSave           = new Texture2D(inputTexture.width, inputTexture.height, TextureFormat.RGBAHalf, false);
        sweepTextureTransformed = new Texture2D(sweepTexture.width, sweepTexture.height);
        screenGrab = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        screenGrab.Create();
        mainCamera = this.gameObject.GetComponent<Camera>();
        
        if (Application.isPlaying)
        {
            StartCoroutine("CpuGGMIterative");
        }
    }

    private void createParametricCurve(Vector2 greyPoint, Vector2 origin)
    {
        if(parametricCurve == null) 
            parametricCurve = new CurveTest(maxRadiometricValue, maxDisplayValue);
        controlPoints = parametricCurve.createControlPoints(origin, greyPoint, slope);
        
        xValues = initialiseXCoordsInRange(curveValueLutDim, Mathf.Round(maxRadiometricValue));
        tValues = parametricCurve.calcTfromXquadratic(xValues, new List<Vector2>(controlPoints));
        yValues = parametricCurve.calcYfromXQuadratic(xValues, tValues, new List<Vector2>(controlPoints));

    }

    // private void updateParametricCurve(Vector2 greyPoint, Vector2 origin)
    // {
    //     if(parametricCurve == null)
    //         parametricCurve = new CurveTest(maxRadiometricValue, maxDisplayValue);
    //     
    //     tValues = parametricCurve.calcTfromXquadratic(xValues, new List<Vector2>(controlPoints));
    //
    // }
    
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            int xCoord = (int)Input.mousePosition.x;
            int yCoord = (int)Input.mousePosition.y;
            Color initialHDRIColor  = inputTexture.GetPixel(xCoord, yCoord);
            Color finalHDRIColor    = hdriTextureTransformed.GetPixel(xCoord, yCoord);

            Debug.Log("Inital \tEXR color: " + initialHDRIColor.ToString());
            Debug.Log("Exposed\tEXR color: " + (initialHDRIColor * exposure).ToString());
            Debug.Log("Final  \tEXR color: " + finalHDRIColor.ToString());
            Debug.Log("--------------------------------------------------------------------------------");
        }
    }

    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        // if (curveDataState == CurveDataState.NotCalculated || curveDataState == CurveDataState.MustRecalculate)
        {
            Graphics.Blit(hdriTextureTransformed, screenGrab, fullScreenTextureMat);
            colorGamutMat.SetTexture("_MainTex", screenGrab);
            Graphics.Blit(screenGrab, dest, fullScreenTextureMat);
        }
    }


    private IEnumerator CpuGGMIterative()
    {
        int counter = maxIterationsPerFrame;
        int hdriPixelArrayLen = 0;

        float hdriMaxRGBChannel = 0.0f;
        // float maxDynamicRange = 0.0f;
        float bleachingXCoord = 0.0f;
        float bleachingRange = 0.0f;
        float bleachingRatio = 0.0f;
        float hdriYMaxValue = 0.0f;
        float inverseSrgbEOTF = (1.0f / 2.2f);
        
        Color ratio = Color.black;
        Color hdriPixelColor = Color.black;

        Vector3 hdriPixelColorVec = Vector3.zero;
        Vector3 maxDynamicRangeVec = Vector3.zero;
        bool aboveGamut = false;
        bool belowGamut = false;

        while (true)
        {
            if (inputTextureIdx != hdriIndex)
            {
                inputTextureIdx = hdriIndex;
                inputTexture = HDRIList[inputTextureIdx];
            }

            hdriPixelArray = inputTexture.GetPixels();
            hdriPixelArrayLen = hdriPixelArray.Length;

            if (curveDataState != CurveDataState.Calculated)
            {
                if (isMultiThreaded && tValues != null && tValues.Count > 0)
                {
                    GamutMapJob job = new GamutMapJob();

                    NativeArray<Color> pixels = new NativeArray<Color>(hdriPixelArrayLen, Allocator.TempJob);
                    pixels.CopyFrom(hdriPixelArray);
                    job.hdriPixelArray = pixels;
                    NativeArray<float> xVals = new NativeArray<float>(xValues.Count, Allocator.TempJob);
                    xVals.CopyFrom(xValues.ToArray());
                    job.xValues = xVals;
                    // parametric curve
                    NativeArray<float> tVals = new NativeArray<float>(tValues.Count, Allocator.TempJob);
                    tVals.CopyFrom(tValues.ToArray());
                    job.tValues = tVals;
                    NativeArray<Vector2> controlPts = new NativeArray<Vector2>(controlPoints.Length, Allocator.TempJob);
                    controlPts.CopyFrom(controlPoints);
                    job.controlPoints = controlPts;
                    
                    job.exposure = exposure;
                    job.isBleachingActive = isBleachingActive;
                    job.lutLength = lutLength;
                    job.yIndexIntersect = yIndexIntersect;
                    job.minRadiometricValue = minRadiometricValue;
                    
                    JobHandle handle = job.Schedule(hdriPixelArrayLen, 1);
                    handle.Complete();
                    
                    // Finished processing image, write values back
                    for (int i = 0; i < hdriPixelArrayLen; i++)
                    {
                        hdriPixelArray[i] = pixels[i];
                    }
                    hdriTextureTransformed.SetPixels(hdriPixelArray);
                    hdriTextureTransformed.Apply();
                    // Cleanup arrays
                    pixels.Dispose();
                    xVals.Dispose();
                    tVals.Dispose();
                    controlPts.Dispose();
                    
                    // ChangeCurveDataState(CurveDataState.Calculated);
                    
                    yield return new WaitForEndOfFrame();
                }
                else
                {
                    counter = maxIterationsPerFrame;

                    for (int i = 0; i < hdriPixelArrayLen; i++, counter--)
                    {
                        if (counter <= 0)
                        {
                            counter = maxIterationsPerFrame;
                            yield return new WaitForEndOfFrame();
                        }

                        // Full dynamic range of image
                        hdriPixelColor = hdriPixelArray[i] * exposure;
                        //Color sweepPixelColor = sweepPixelArray[i] * sweepExposure;
                        ratio = Color.blue;
                        // Secondary Nuance Grade, guardrails
                        if (hdriPixelColor.r > maxRadiometricValue || hdriPixelColor.g > maxRadiometricValue ||
                            hdriPixelColor.b > maxRadiometricValue)
                        {
                            hdriPixelColor.r = maxRadiometricValue;
                            hdriPixelColor.g = maxRadiometricValue;
                            hdriPixelColor.b = maxRadiometricValue;
                        }
                        //if (sweepPixelColor.r > animationCurve[3].time || sweepPixelColor.g > animationCurve[3].time || sweepPixelColor.b > animationCurve[3].time)
                        //{
                        //    sweepPixelColor.r = animationCurve[3].time;
                        //    sweepPixelColor.g = animationCurve[3].time;
                        //    sweepPixelColor.b = animationCurve[3].time;
                        //}

                        // Calculate Pixel max color and ratio
                        hdriMaxRGBChannel = hdriPixelColor.maxColorComponent;
                        ratio = hdriPixelColor / hdriMaxRGBChannel;

                        // Calculate Sweep max color and ratio
                        //float sweepMaxRGBChannel = sweepPixelColor.maxColorComponent;
                        //Color sweepRatio = sweepPixelColor / sweepMaxRGBChannel;

                        // Transfer function
                        if (activeTransferFunction == TransferFunction.Max_RGB)
                        {
                            bleachingXCoord = 0.0f; // Intersect of x on Y = 1
                            
                            if (isBleachingActive)
                            {
                                // Calculate bleaching  values by iterating through the Y values array and returning the closest x coord
                                bleachingXCoord = parametricCurve.getXCoordinate(1.0f, yValues, tValues, new List<Vector2>(controlPoints));
                                
                                if (hdriPixelColor.r > bleachingXCoord || hdriPixelColor.g > bleachingXCoord ||
                                    hdriPixelColor.b > bleachingXCoord)
                                {
                                    bleachingRange = maxRadiometricValue - bleachingXCoord;
                                    bleachingRatio = (hdriPixelColor.maxColorComponent - bleachingXCoord) /
                                                     bleachingRange;

                                    hdriPixelColorVec.Set(hdriPixelColor.r, hdriPixelColor.g, hdriPixelColor.b);
                                    maxDynamicRangeVec.Set(maxRadiometricValue, maxRadiometricValue, maxRadiometricValue);
                                    hdriPixelColorVec = Vector3.Lerp(hdriPixelColorVec, maxDynamicRangeVec,
                                        bleachingRatio);

                                    hdriPixelColor.r = hdriPixelColorVec.x;
                                    hdriPixelColor.g = hdriPixelColorVec.y;
                                    hdriPixelColor.b = hdriPixelColorVec.z;

                                    ratio = hdriPixelColor / hdriMaxRGBChannel;
                                }
                            }

                            // Get Y curve value
                            float yValue = parametricCurve.getYCoordinate(hdriMaxRGBChannel, xValues, tValues,
                                new List<Vector2>(controlPoints));
                            hdriYMaxValue = Mathf.Min(yValue, 1.0f);
                            hdriPixelColor = hdriYMaxValue * ratio;
                            
                            if (showPixelsOutOfGamut)
                            {
                                if (yValue < 0.0f || hdriMaxRGBChannel < minRadiometricValue) // Below Gamut
                                {
                                    hdriPixelColor = Color.red;
                                }
                                else if (yValue > 1.0f)                                       // Above gamut
                                {
                                    hdriPixelColor = Color.green;
                                }
                            }

                            // Sweep texture
                            //sweepMaxRGBChannel = animationCurve.Evaluate(sweepMaxRGBChannel);
                            //sweepPixelColor = sweepMaxRGBChannel * sweepRatio;

                            activeTransferFunction = TransferFunction.Max_RGB;
                        }
                        else
                        {
                            activeTransferFunction = TransferFunction.Per_Channel;
                            
                            hdriPixelColor.r = evaluateSingleColorChannel(hdriPixelColor.r);
                            hdriPixelColor.g = evaluateSingleColorChannel(hdriPixelColor.g);
                            hdriPixelColor.b = evaluateSingleColorChannel(hdriPixelColor.b);

                            if (showPixelsOutOfGamut)
                            {
                                if (hdriPixelColor.r < minRadiometricValue || hdriPixelColor.g < minRadiometricValue ||
                                    hdriPixelColor.b < minRadiometricValue)
                                {
                                    hdriPixelColor = Color.red;
                                }
                                else if (hdriPixelColor.r > 1.0f || hdriPixelColor.g > 1.0f ||
                                         hdriPixelColor.b > 1.0f)
                                {
                                    hdriPixelColor = Color.green;
                                }
                            }

                            //sweepPixelColor.r = animationCurve.Evaluate(sweepPixelColor.r);
                            //sweepPixelColor.g = animationCurve.Evaluate(sweepPixelColor.g);
                            //sweepPixelColor.b = animationCurve.Evaluate(sweepPixelColor.b);
                        }

                        hdriPixelArray[i].r = Mathf.Pow(hdriPixelColor.r, inverseSrgbEOTF);
                        hdriPixelArray[i].g = Mathf.Pow(hdriPixelColor.g, inverseSrgbEOTF);
                        hdriPixelArray[i].b = Mathf.Pow(hdriPixelColor.b, inverseSrgbEOTF);
                        hdriPixelArray[i].a = 1.0f;
                        //sweepPixelArray[i] = new Color(Mathf.Pow(sweepPixelColor.r, 1.0f / 2.2f), Mathf.Pow(sweepPixelColor.g, 1.0f / 2.2f), Mathf.Pow(sweepPixelColor.b, 1.0f / 2.2f), 1.0f);

                    }

                    hdriTextureTransformed.SetPixels(hdriPixelArray);
                    hdriTextureTransformed.Apply();
                }

                ChangeCurveDataState(CurveDataState.Calculated);
                //sweepTextureTransformed.SetPixels(sweepPixelArray);
                //sweepTextureTransformed.Apply();
            }
            else
            { 
                yield return new WaitForEndOfFrame();
            }
        }
    }


    private void ChangeCurveDataState(CurveDataState newState)
    {
        switch (newState)
        {
            case CurveDataState.NotCalculated:
            {
                mainCamera.clearFlags = CameraClearFlags.Skybox;
                curveDataState = CurveDataState.NotCalculated;
                break;
            }
            case CurveDataState.Calculated:
            {
                mainCamera.clearFlags = CameraClearFlags.Nothing;
                curveDataState = CurveDataState.Calculated;
                break;
            }
            case CurveDataState.MustRecalculate:
            {
                mainCamera.clearFlags = CameraClearFlags.Skybox;
                curveDataState = CurveDataState.MustRecalculate;
                break;
            }
        }
    }

    private float evaluateSingleColorChannel(float colorChannel)
    {
        return parametricCurve.getYCoordinate(colorChannel, xValues, tValues,
            new List<Vector2>(controlPoints));
    }

    // Utility methods
    public List<float> initialiseXCoordsInRange(int dimension, float maxRange)
    {
        List<float> xValues = new List<float>(dimension);
        float step = maxRange / (float) dimension;
        for (int i = 1; i <= dimension; i++)
        {
            xValues.Add(i * step);
        }

        return xValues;
    }

    public void getParametricCurveValues(out float inSlope, out float originPointX, out float originPointY,
        out float greyPointX, out float greyPointY)
    {
        inSlope = slope;
        originPointX = origin.x;
        originPointY = origin.y;
        greyPointX = greyPoint.x;
        greyPointY = greyPoint.y;
    }

    public void setParametricCurveValues( float inSlope, float originPointX, float originPointY, 
        float greyPointX, float greyPointY)
    {
        this.slope = inSlope;
        this.origin.x = originPointX;
        this.origin.y = originPointY;
        this.greyPoint.x = greyPointX;
        this.greyPoint.y = greyPointY;

        ChangeCurveDataState(CurveDataState.MustRecalculate);
        createParametricCurve(greyPoint, origin);
        // updateParametricCurve(greyPoint, origin);
    }
    
    public bool getShowSweep()
    {
        return isSweepActive;
    }

    public void setShowSweep(bool isActive) 
    {
        isSweepActive = isActive;
        sweepPlane.SetActive(isSweepActive);
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
        ChangeCurveDataState(CurveDataState.MustRecalculate);
    }

    Texture2D toTexture2D(RenderTexture rTex)
    {
        Texture2D tex = new Texture2D(rTex.width, rTex.height, TextureFormat.RGBAHalf, false);
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

    public List<float> getTValues()
    {
        return tValues;
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

    public void setHDRIIndex(int index)
    {
        hdriIndex = index;
        ChangeCurveDataState(CurveDataState.MustRecalculate);
    }
    public void setBleaching(bool inIsBleachingActive) 
    {
        isBleachingActive = inIsBleachingActive;
        ChangeCurveDataState(CurveDataState.MustRecalculate);
    }

    public void setExposure(float exposure)
    {
        this.exposure = exposure;
        ChangeCurveDataState(CurveDataState.MustRecalculate);
    }

    public void setActiveTransferFunction(TransferFunction transferFunction)
    {
        this.activeTransferFunction = transferFunction;
        ChangeCurveDataState(CurveDataState.MustRecalculate);
    }

    static public Texture2D GetRTPixels(RenderTexture rt)
    {
        // Remember currently active render texture
        RenderTexture currentActiveRT = RenderTexture.active;

        // Set the supplied RenderTexture as the active one
        RenderTexture.active = rt;

        // Create a new Texture2D and read the RenderTexture image into it
        Texture2D tex = new Texture2D(rt.width, rt.height);
        tex.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);

        // Restorie previously active render texture
        RenderTexture.active = currentActiveRT;
        return tex;
    }
    
    float remap(float value, float min0, float max0, float min1, float max1)
    {
        return min1 + (value - min0) * ((max1 - min1) / (max0 - min0));
    }

    Color colorRemap(Color col, float channel)
    {
        float red = col.r;
        float green = col.g;
        float blue = col.b;
        Color outColor = new Color();
        if (channel == 0.0) // 0.0 corresponds to red
        {
            float newRangeMin = remap(Mathf.Clamp01(col.r), 1.0f, 0.85f, 1.0f, 0.0f); // how far between 0.85 and 1 are we? Remap it to 1.0 to 0.0

            green = (col.r != col.g) ? Mathf.Lerp(green, col.r, newRangeMin) : col.g;
            blue = (col.r != col.b) ? Mathf.Lerp(blue, col.r, newRangeMin) : col.b;

            outColor = new Color(col.r, green, blue);
        }
        else if (channel == 1.0) // 1.0 corresponds to green
        {
            float newRangeMin = remap(Mathf.Clamp01(col.g), 1.0f, 0.85f, 1.0f, 0.0f); // how far between 0.85 and 1 are we? Remap it to 1.0 to 0.0

            red = (col.g != col.r) ? Mathf.Lerp(red, col.g, newRangeMin) : col.r;
            blue = (col.g != col.b) ? Mathf.Lerp(blue, col.g, newRangeMin) : col.b;

            outColor = new Color(red, col.g, blue);
        }
        else if (channel == 2.0) // 2.0 corresponds to blue
        {
            float newRangeMin = remap(Mathf.Clamp01(col.b), 1.0f, 0.85f, 1.0f, 0.0f); // how far between 0.85 and 1 are we? Remap it to 1.0 to 0.0

            red = (col.b != col.r) ? Mathf.Lerp(red, col.b, newRangeMin) : col.r;
            green = (col.b != col.g) ? Mathf.Lerp(green, col.b, newRangeMin) : col.g;
            outColor = new Color(red, green, col.b);
        }

        return new Color(Mathf.Clamp01(outColor.r), Mathf.Clamp01(outColor.g), Mathf.Clamp01(outColor.b));
    }
    
 

    bool all(bool[] x)       // bvec can be bvec2, bvec3 or bvec4
    {
        bool result = true;
        int i;
        for (i = 0; i < x.Length; ++i)
        {
            result &= x[i];
        }
        return result;
    }

    bool any(bool[] x)
    {     // bvec can be bvec2, bvec3 or bvec4
        bool result = false;
        int i;
        for (i = 0; i < x.Length; ++i)
        {
            result |= x[i];
        }
        return result;
    }

    Vector3 sum_vec3(Vector3 input_vector)
    {
        float sum = input_vector.x + input_vector.y + input_vector.z;
        return new Vector3(sum, sum, sum);
    }

    Vector3 max_vec3(Vector3 input_vector)
    {
        float max = Mathf.Max(Mathf.Max(input_vector.x, input_vector.y), input_vector.z);
        return new Vector3(max, max, max);
    }

    bool[] greaterThan(Vector3 vecA, Vector3 vecB)
    {
        return new bool[3] { (vecA.x > vecB.x), (vecA.x > vecB.x), (vecA.x > vecB.x) };
    }

    bool[] lessThanEqual(Vector3 vecA, Vector3 vecB)
    {
        return new bool[3] { (vecA.x <= vecB.x), (vecA.x <= vecB.x), (vecA.x <= vecB.x) };
    }
    
}

struct GamutMapJob : IJobParallelFor
{
    public NativeArray<Color> hdriPixelArray;
    [ReadOnly] public NativeArray<float> tValues;
    [ReadOnly] public NativeArray<float> xValues;
    [ReadOnly] public NativeArray<Vector2> controlPoints;
    
    [ReadOnly] public float exposure;
    [ReadOnly] public Vector2 finalKeyframe;
    [ReadOnly] public bool isBleachingActive;
    [ReadOnly] public int lutLength;
    [ReadOnly] public int yIndexIntersect;
    [ReadOnly] public float minRadiometricValue;
    
    private float hdriMaxRGBChannel;
    private float maxDynamicRange;
    private float bleachStartPoint;
    private float bleachingRange;
    private float bleachingRatio;
    private float hdriYMaxValue;
    private float inverseSrgbEOTF;

    private Color ratio;
    private Color hdriPixelColor;
    private Color tempResult;

    private Vector3 hdriPixelColorVec;
    private Vector3 maxDynamicRangeVec;
    private enum ColorRange
    {
        InGamut,
        BelowGamut,
        AboveGamut
    };
    private ColorRange colorRange;
    
    public float calcYfromXQuadratic(float xValue, NativeArray<float> tValues, NativeArray<Vector2> controlPoints)
    {
        float yValues = 0.0f;
        Vector2[] controlPointsArray = new Vector2[]{ 
            controlPoints[0], controlPoints[1], controlPoints[2],
            controlPoints[2], controlPoints[3], controlPoints[4],
            controlPoints[4], controlPoints[5], controlPoints[6]};

        for (int index = 0; index < tValues.Length; index++)
        {
            for (int i = 0; i < controlPointsArray.Length - 1; i += 3)
            {
                Vector2 p0 = controlPointsArray[0 + i];
                Vector2 p1 = controlPointsArray[1 + i];
                Vector2 p2 = controlPointsArray[2 + i];

                if (p0.x <= xValue && xValue <= p2.x)
                {
                    float tValue = tValues[index];
                    yValues = (Mathf.Pow(1.0f - tValue, 2.0f) * p0.y) +
                              (2.0f * (1.0f - tValue) * tValue * p1.y) +
                              (Mathf.Pow(tValue, 2.0f) * p2.y);
                
                    return yValues;
                }
            }
        }
        return yValues;
    }
      
    public static float ClosestTo(NativeArray<float> list, float target, out int index)
    {
        var closest = float.MaxValue;
        var minDifference = float.MaxValue;
        var outIndex = 0;
        for (int i = 0; i < list.Length; i++)
        {
            var difference = Math.Abs((float)list[i] - target);
            if (minDifference > difference)
            {
                minDifference = (float)difference;
                closest = list[i];
                outIndex = i;
            }
        }

        index = outIndex;
        return closest;
    }

    public float getYCoordinate(float inputXCoord, NativeArray<float> xCoords, 
        NativeArray<float> tValues, NativeArray<Vector2> controlPoints)
    {
        if (xCoords.Length <= 0 || tValues.Length <= 0)
        {
            Debug.Log("Input array of x values or t values have mismatched lengths ");
            return -1.0f;
        }

        Vector2[] controlPointsArray = new Vector2[]{ 
            controlPoints[0], controlPoints[1], controlPoints[2],
            controlPoints[2], controlPoints[3], controlPoints[4],
            controlPoints[4], controlPoints[5], controlPoints[6]};

        for (int i = 0; i < controlPointsArray.Length - 1 ; i += 3)
        {
            Vector2 p0 = controlPointsArray[0 + i];
            Vector2 p1 = controlPointsArray[1 + i];
            Vector2 p2 = controlPointsArray[2 + i];

            if (p0.x <= inputXCoord && inputXCoord <= p2.x)
            {
// Search closest x value to xValue and grab its index in the array too
// The array index is used to lookup the tValue
                int idx = 0;
                ClosestTo(xCoords, inputXCoord, out idx);
                float tValue = tValues[idx];

                return (Mathf.Pow(1.0f - tValue, 2.0f) * p0.y) +
                       (2.0f * (1.0f - tValue) * tValue * p1.y) +
                       (Mathf.Pow(tValue, 2.0f) * p2.y);
            }
        }

        return -1.0f;
    }
    public void Execute(int index)
    {
        hdriPixelColor = hdriPixelArray[index] * exposure;
        ratio = Color.black;
        inverseSrgbEOTF = 1.0f / 2.2f;
        colorRange = ColorRange.InGamut;
   
            
        // Calculate Pixel max color and ratio
        hdriMaxRGBChannel = hdriPixelColor.maxColorComponent; 
        ratio = hdriPixelColor / hdriMaxRGBChannel;
        
        maxDynamicRange = maxDynamicRange; // The x axis max value on the curve
        // TODO: FIX ME - needs to calculate where the curve intersects Y = 1
        bleachStartPoint = 1.0f;

        if (isBleachingActive)
        {
            if (hdriPixelColor.r > bleachStartPoint || hdriPixelColor.g > bleachStartPoint ||
                hdriPixelColor.b > bleachStartPoint)
            {
                bleachingRange = maxDynamicRange - bleachStartPoint;
                bleachingRatio = (hdriPixelColor.maxColorComponent - bleachStartPoint) /
                                 bleachingRange;

                hdriPixelColorVec.Set(hdriPixelColor.r, hdriPixelColor.g, hdriPixelColor.b);
                maxDynamicRangeVec.Set(maxDynamicRange, maxDynamicRange, maxDynamicRange);
                Vector3 outputColor = Vector3.Lerp(hdriPixelColorVec, maxDynamicRangeVec,
                    bleachingRatio);

                hdriPixelColor.r = outputColor.x;
                hdriPixelColor.g = outputColor.y;
                hdriPixelColor.b = outputColor.z;

                ratio = hdriPixelColor / hdriMaxRGBChannel;
            }
        }

        // Get Y curve value
           
        float yValue = getYCoordinate(hdriMaxRGBChannel, xValues, tValues,
            controlPoints);
        if (yValue < 0.0f)
        {
            colorRange = ColorRange.BelowGamut;
        } else if (yValue > 1.0f)
        {
            colorRange = ColorRange.AboveGamut;
        } else if (hdriMaxRGBChannel < minRadiometricValue)
        {
            colorRange = ColorRange.BelowGamut;
        }

        if (colorRange == ColorRange.InGamut)
        {
            hdriYMaxValue = Mathf.Min(yValue, 1.0f);
        }
            

        if (colorRange == ColorRange.InGamut)
        {
            hdriPixelColor = hdriYMaxValue * ratio;
        } else if (colorRange == ColorRange.BelowGamut)
        {
            hdriPixelColor = Color.red;
        } else if (colorRange == ColorRange.AboveGamut)
        {
            hdriPixelColor = Color.green;                      
        }

        colorRange = ColorRange.InGamut;
        
        tempResult.r = Mathf.Pow(hdriPixelColor.r, inverseSrgbEOTF);
        tempResult.g = Mathf.Pow(hdriPixelColor.g, inverseSrgbEOTF);
        tempResult.b = Mathf.Pow(hdriPixelColor.b, inverseSrgbEOTF);
        tempResult.a = 1.0f;
            
        hdriPixelArray[index] = tempResult;
        
    }
}