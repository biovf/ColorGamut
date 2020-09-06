using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using MathNet.Numerics;

public class ColorGamut : MonoBehaviour
{

    public Material colorGamutMat;
    public Texture2D inputTexture;
    public Material fullScreenTextureMat;
    public GameObject hdriPlane;
    public GameObject sweepPlane;
    public Texture2D sweepTexture;
    public List<Texture2D> HDRIList;

    public enum ShoulderLength
    {
        F_2_8,
        F_4_0,
        F_5_6,
        F_8_0,
        F_11_0
    }
    public enum LerpRatio
    {
        Aesthetic,
        Radiometric
    }
    [Header("Dye Bleaching")]
    public bool enableDyeBleaching;
    public LerpRatio lerpRatio;
    public float dye_bleach_x = 1.0f;
    public float dye_bleach_y = 1.0f;
    public enum TransferFunction
    {
        Per_Channel,
        Max_RGB
    }
    [Space]
    [Range(0.01f, 20.0f)]
    public float exposure;
    [Range(0.01f, 20.0f)]
    public float sweepExposure;
    [Space]
    [Header("Aesthetic Function")]
    public TransferFunction activeTransferFunction;
    [Space]


    private bool KeyIsUp = false;
    private bool ApplyTexture = false;
    private FullCurve dstCurve;
    private CurveParamsUser userCurveParams;
    private Texture2D hdriTextureTransformed;
    private Texture2D sweepTextureTransformed;
    private Ggm_troyedition ggm;
    private int hdriIndex;
    private int inputTextureIdx = 0;
    private string logOutput = "";
    private StringBuilder logOutputStrBuilder;
    private float CPUMode;
    private bool useTanHCompressionFunction;
    private RenderTexture screenGrab;
    private float enableGamutMap;
    private bool isSweepActive;
    private bool isBleachingActive;
    private Texture2D textureToSave;

    private AnimationCurve animationCurve;
    Color[] hdriPixelArray;

    private void Awake()
    {
        activeTransferFunction = TransferFunction.Max_RGB;
        lerpRatio = LerpRatio.Aesthetic;
    }


    void Start()
    {
        isBleachingActive = true;
        screenGrab = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        screenGrab.Create();
        exposure = 1.0f;
        sweepExposure = 1.0f;
        useTanHCompressionFunction = false;
        enableGamutMap = 1.0f;
        CPUMode = 0.0f;

        dstCurve = new FullCurve();
        userCurveParams = new CurveParamsUser();
        dstCurve = FilmicToneCurve.CreateCurve(FilmicToneCurve.CalcDirectParamsFromUser(userCurveParams));
        logOutputStrBuilder = new StringBuilder(10000);
        hdriIndex = 0;

        if (HDRIList == null)
            Debug.LogError("HDRIs list is empty");

        inputTexture = HDRIList[hdriIndex];
        hdriTextureTransformed  = new Texture2D(inputTexture.width, inputTexture.height, TextureFormat.RGBAHalf, false);
        textureToSave           = new Texture2D(inputTexture.width, inputTexture.height, TextureFormat.RGBAHalf, false);
        sweepTextureTransformed = new Texture2D(sweepTexture.width, sweepTexture.height);
        ggm = new Ggm_troyedition();
        isSweepActive = false;
        enableDyeBleaching = false;
        //animationCurve = AnimationCurve.EaseInOut(0.0f, 0.0f, 1.0f, 1.0f);
        hdriPixelArray = new Color[inputTexture.width * inputTexture.height];
        StartCoroutine("CpuGGMIterative");
    }
    
    void Update()
    {
        if (Input.GetKeyUp(KeyCode.T))
            KeyIsUp = true;

        if (Input.GetKeyUp(KeyCode.Y))
            KeyIsUp = false;

        if (Input.GetMouseButtonDown(0))
        {
            //Color[] pixels = toTexture2D(screenGrab).GetPixels();
            //textureToSave.SetPixels(pixels);
            //textureToSave.Apply();
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
    
    // private void OnPreRender()
    // {
    //     hdriPlane.GetComponent<MeshRenderer>().material.SetTexture("_MainTex", hdriTextureTransformed);
    // }
    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        Graphics.Blit(hdriTextureTransformed, screenGrab, fullScreenTextureMat);
        colorGamutMat.SetTexture("_MainTex", screenGrab);
        Graphics.Blit(screenGrab, dest, fullScreenTextureMat);
    }

    private const int maxIterationsPerFrame = 100000;
    IEnumerator CpuGGMIterative()
    {
        int counter = maxIterationsPerFrame;
        int hdriPixelArrayLen = 0;

        float hdriMaxRGBChannel = 0.0f;
        float maxDynamicRange = 0.0f;
        float bleachStartPoint = 0.0f;
        float bleachingRange = 0.0f;
        float bleachingRatio = 0.0f;
        float hdriYMaxValue = 0.0f;
        float inverseSrgbEOTF = (1.0f / 2.2f);
        
        Color ratio = Color.black;
        Color hdriPixelColor = Color.black;

        Vector3 hdriPixelColorVec = Vector3.zero;
        Vector3 maxDynamicRangeVec = Vector3.zero;
        
        while (true)
        {
            if (inputTextureIdx != hdriIndex) 
            {
                inputTextureIdx = hdriIndex;
                inputTexture = HDRIList[inputTextureIdx];
            }

            hdriPixelArray = inputTexture.GetPixels();
            //Color[] sweepPixelArray = sweepTexture.GetPixels();

            counter = maxIterationsPerFrame;
            hdriPixelArrayLen = hdriPixelArray.Length;

 
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
                ratio = Color.black;

                if (animationCurve != null)
                {
                    // Secondary Nuance Grade, guardrails
                    if (hdriPixelColor.r > animationCurve[3].time || hdriPixelColor.g > animationCurve[3].time || hdriPixelColor.b > animationCurve[3].time)
                    {
                        hdriPixelColor.r = animationCurve[3].time;
                        hdriPixelColor.g = animationCurve[3].time;
                        hdriPixelColor.b = animationCurve[3].time;
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
                        // New approach
                        maxDynamicRange = animationCurve[3].time; // The x axis max value on the curve
                        bleachStartPoint = TimeFromValue(animationCurve, 1.0f);   // Intersect of x on Y = 1

                        if (isBleachingActive)
                        {
                            if (hdriPixelColor.r > bleachStartPoint || hdriPixelColor.g > bleachStartPoint || hdriPixelColor.b > bleachStartPoint)
                            {
                                bleachingRange = maxDynamicRange - bleachStartPoint;
                                bleachingRatio = (hdriPixelColor.maxColorComponent - bleachStartPoint) / bleachingRange;
                                
                                hdriPixelColorVec.Set(hdriPixelColor.r, hdriPixelColor.g, hdriPixelColor.b);
                                maxDynamicRangeVec.Set(maxDynamicRange, maxDynamicRange, maxDynamicRange);
                                Vector3 outputColor = Vector3.Lerp( hdriPixelColorVec,maxDynamicRangeVec , bleachingRatio);
                                
                                hdriPixelColor.r = outputColor.x;
                                hdriPixelColor.g = outputColor.y;
                                hdriPixelColor.b = outputColor.z;

                                ratio = hdriPixelColor / hdriMaxRGBChannel;
                            }
                        }
                        // Get Y curve value
                        hdriYMaxValue = Mathf.Min(animationCurve.Evaluate(hdriMaxRGBChannel), 1.0f);
                        hdriPixelColor = hdriYMaxValue * ratio;

                        // Sweep texture
                        //sweepMaxRGBChannel = animationCurve.Evaluate(sweepMaxRGBChannel);
                        //sweepPixelColor = sweepMaxRGBChannel * sweepRatio;

                        activeTransferFunction = TransferFunction.Max_RGB;
                    }
                    else
                    {
                        activeTransferFunction = TransferFunction.Per_Channel;
                        hdriPixelColor.r = animationCurve.Evaluate(hdriPixelColor.r);
                        hdriPixelColor.g = animationCurve.Evaluate(hdriPixelColor.g);
                        hdriPixelColor.b = animationCurve.Evaluate(hdriPixelColor.b);

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
            }

            hdriTextureTransformed.SetPixels(hdriPixelArray);
            hdriTextureTransformed.Apply();

            //sweepTextureTransformed.SetPixels(sweepPixelArray);
            //sweepTextureTransformed.Apply();
        }
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

    Texture2D toTexture2D(RenderTexture rTex)
    {
        Texture2D tex = new Texture2D(rTex.width, rTex.height, TextureFormat.RGBAHalf, false);
        RenderTexture.active = rTex;
        tex.ReadPixels(new Rect(0, 0, rTex.width, rTex.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        return tex;
    }
    public AnimationCurve getAnimationCurve() 
    {
        return animationCurve;
    }
    public void setAnimationCurve(AnimationCurve curve)
    {
        animationCurve = curve;
    }

    public void setHDRIIndex(int index)
    {
        hdriIndex = index;
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
    
    public void setBleaching(bool inIsBleachingActive) 
    {
        isBleachingActive = inIsBleachingActive;
    }
    
    static float TimeFromValue(AnimationCurve c, float value, float precision = 1e-6f)
    {
        float minTime = c.keys[0].time;
        float maxTime = c.keys[c.keys.Length - 1].time;
        float best = (maxTime + minTime) / 2;
        float bestVal = c.Evaluate(best);
        int it = 0;
        const int maxIt = 1000;
        float sign = Mathf.Sign(c.keys[c.keys.Length - 1].value - c.keys[0].value);
        while (it < maxIt && Mathf.Abs(minTime - maxTime) > precision)
        {
            if ((bestVal - value) * sign > 0)
            {
                maxTime = best;
            }
            else
            {
                minTime = best;
            }
            best = (maxTime + minTime) / 2;
            bestVal = c.Evaluate(best);
            it++;
        }
        return best;
    }

    Color newGGM(Color RGB, float compressionThreshold)
    {
        Vector3 result = ggm.the_ggm(new Vector3(RGB.r, RGB.g, RGB.b), new Vector3(compressionThreshold, compressionThreshold, compressionThreshold));
        return new Color(result.x, result.y, result.z);
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
