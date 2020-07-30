﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

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

    private AnimationCurve animationCurve;

    private void Awake()
    {
        activeTransferFunction = TransferFunction.Max_RGB;
        lerpRatio = LerpRatio.Aesthetic;
    }


    void Start()
    {
        screenGrab = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
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
        hdriTextureTransformed = new Texture2D(inputTexture.width, inputTexture.height);
        sweepTextureTransformed = new Texture2D(sweepTexture.width, sweepTexture.height);
        ggm = new Ggm_troyedition();
        isSweepActive = false;
        enableDyeBleaching = false;
        //animationCurve = AnimationCurve.EaseInOut(0.0f, 0.0f, 1.0f, 1.0f);

        StartCoroutine("CpuGGMIterative");
    }

    //public void setCurveValues(float x0, float y0, float x1, float y1, float w, float overShootX, float overShootY) 
    //{
    //    if (x0 != m_x0) 
    //    {
    //        m_x0 = x0;
    //    }
    //    if (y0 != m_y0)
    //    {
    //        m_y0 = y0;
    //    }
    //    if (x1 != m_x1)
    //    {
    //        m_x1 = x1;
    //    }
    //    if (y1 != m_y1)
    //    {
    //        m_y1 = y1;
    //    }
    //    if (w != m_W)
    //    {
    //        m_W = w;
    //    }
    //    if (overShootX != m_overshootX) 
    //    {
    //        m_overshootX = overShootX;
    //    }
    //    if (overShootY != m_overshootY)
    //    {
    //        m_overshootY = overShootY;
    //    }
    //}
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

    public bool getShowSweep()
    {
        return isSweepActive;
    }

    public void setShowSweep(bool isActive) 
    {
        isSweepActive = isActive;
        sweepPlane.SetActive(isSweepActive);

    }

    void Update()
    {
        if (Input.GetKeyUp(KeyCode.T))
            KeyIsUp = true;

        if (Input.GetKeyUp(KeyCode.Y))
            KeyIsUp = false;

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

    private void OnPreRender()
    {
        hdriPlane.GetComponent<MeshRenderer>().material.SetTexture("_MainTex", hdriTextureTransformed);
        sweepPlane.GetComponent<MeshRenderer>().material.SetTexture("_MainTex", sweepTextureTransformed);
    }
    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        Graphics.Blit(src, screenGrab, fullScreenTextureMat);

        colorGamutMat.SetTexture("_MainTex", screenGrab);
        colorGamutMat.SetFloat("_DoGamutMap", enableGamutMap);
        colorGamutMat.SetFloat("_ExposureControl", exposure);
        colorGamutMat.SetFloat("_TanHCompression", (useTanHCompressionFunction == false ? 0.0f : 1.0f));

        Graphics.Blit(screenGrab, dest, colorGamutMat);
    }

    IEnumerator CpuGGMIterative()
    {
        while (true)
        {
            if (inputTextureIdx != hdriIndex) 
            {
                inputTextureIdx = hdriIndex;
                inputTexture = HDRIList[inputTextureIdx];
            }

            Color[] hdriPixelArray = inputTexture.GetPixels();
            Color[] sweepPixelArray = sweepTexture.GetPixels();

            int counter = 10000;
            for (int i = 0; i < hdriPixelArray.Length; i++, counter--)
            {
                if (counter <= 0)
                {
                    counter = 10000;
                    yield return new WaitForEndOfFrame();
                }

                // Full dynamic range of image
                Color hdriPixelColor = hdriPixelArray[i] * exposure;
                Color sweepPixelColor = sweepPixelArray[i] * sweepExposure;
                Color ratio;

                // Timothy Lottes Max approach
                //float hdriMaxRGBChannel = hdriPixelColor.maxColorComponent;
                //Color ratio = hdriPixelColor / hdriMaxRGBChannel;

                //float sweepMaxRGBChannel = sweepPixelColor.maxColorComponent;
                //Color sweepRatio = sweepPixelColor / sweepMaxRGBChannel;
                // Primary Grade, guardrails
                // Clip scene values outside our max range
                // Dye bleaching

                // if (any(col) > dye_bleach_x))
                //    lerp_ratio = (dye_bleach_y - eval(transfer_function(maxColor))) / dye_bleach_y; // aesthetic version
                //    // lerp_ratio = (maxColor - dye_bleach_x) / (m_W - dye_bleach_x) // radiometric version
                //    remaining_space = maxColor - col; 
                //    additive_light = lerp_ratio * remaining_space;
                //    new_bleached_color = col + additive_light
                //    ratio = new_bleached_color / maxColor

                if (enableDyeBleaching == true)
                {
                    float lerp_ratio = 0.0f;
                    if (hdriPixelColor.r > dye_bleach_x || hdriPixelColor.g > dye_bleach_x || hdriPixelColor.b > dye_bleach_x)
                    {
                        float hdriMaxRGBChannel = hdriPixelColor.maxColorComponent;

                        if (lerpRatio == LerpRatio.Aesthetic)
                        {
                            // Y ratio
                            lerp_ratio = (dstCurve.Eval(hdriMaxRGBChannel) - 1.0f) / (dye_bleach_y - 1.0f);
                        }
                        else
                        {
                            // X ratio
                            lerp_ratio = (hdriMaxRGBChannel - dye_bleach_x) / (animationCurve[3].time - dye_bleach_x);
                        }

                        Color remaining_space = new Color(hdriMaxRGBChannel - hdriPixelColor.r, hdriMaxRGBChannel - hdriPixelColor.g, hdriMaxRGBChannel - hdriPixelColor.b);
                        Color additive_light = lerp_ratio * remaining_space;
                        Color new_bleached_color = hdriPixelColor + additive_light;
                        ratio = new_bleached_color / hdriMaxRGBChannel;
                    }
                }

                //col = newGGM(col, 1.0f);
                if (animationCurve != null)
                {
                    // Secondary Nuance Grade, guardrails
                    if (hdriPixelColor.r > animationCurve[3].time || hdriPixelColor.g > animationCurve[3].time || hdriPixelColor.b > animationCurve[3].time)
                    {
                        hdriPixelColor.r = animationCurve[3].time;
                        hdriPixelColor.g = animationCurve[3].time;
                        hdriPixelColor.b = animationCurve[3].time;
                    }
                    if (sweepPixelColor.r > animationCurve[3].time || sweepPixelColor.g > animationCurve[3].time || sweepPixelColor.b > animationCurve[3].time)
                    {
                        sweepPixelColor.r = animationCurve[3].time;
                        sweepPixelColor.g = animationCurve[3].time;
                        sweepPixelColor.b = animationCurve[3].time;
                    }

                    // Timothy Lottes Max approach
                    float hdriMaxRGBChannel = hdriPixelColor.maxColorComponent;
                    ratio = hdriPixelColor / hdriMaxRGBChannel;
                    float sweepMaxRGBChannel = sweepPixelColor.maxColorComponent;
                    Color sweepRatio = sweepPixelColor / sweepMaxRGBChannel;
                    //float luma = Vector3.Dot(new Vector3(col.r, col.g, col.b), new Vector3(0.2126f, 0.7152f, 0.0722f));

                    // Transfer function
                    if (KeyIsUp || activeTransferFunction == TransferFunction.Max_RGB)
                    {
                        hdriMaxRGBChannel = animationCurve.Evaluate(hdriMaxRGBChannel);
                        //maxColor = animationCurve.Evaluate(luma);
                        hdriPixelColor = hdriMaxRGBChannel * ratio;

                        sweepMaxRGBChannel = animationCurve.Evaluate(sweepMaxRGBChannel);
                        sweepPixelColor = sweepMaxRGBChannel * sweepRatio;

                        activeTransferFunction = TransferFunction.Max_RGB;

                    }
                    else
                    {
                        activeTransferFunction = TransferFunction.Per_Channel;
                        hdriPixelColor.r = animationCurve.Evaluate(hdriPixelColor.r);
                        hdriPixelColor.g = animationCurve.Evaluate(hdriPixelColor.g);
                        hdriPixelColor.b = animationCurve.Evaluate(hdriPixelColor.b);

                        sweepPixelColor.r = animationCurve.Evaluate(sweepPixelColor.r);
                        sweepPixelColor.g = animationCurve.Evaluate(sweepPixelColor.g);
                        sweepPixelColor.b = animationCurve.Evaluate(sweepPixelColor.b);

                        //col.r = dstCurve.Eval(col.r);
                        //col.g = dstCurve.Eval(col.g);
                        //col.b = dstCurve.Eval(col.b);
                    }

                    // GGM - Here doesn't make sense 
                    //col = newGGM(col, 1.0f);

                    hdriPixelArray[i] = new Color(Mathf.Pow(hdriPixelColor.r, 1.0f / 2.2f), Mathf.Pow(hdriPixelColor.g, 1.0f / 2.2f), Mathf.Pow(hdriPixelColor.b, 1.0f / 2.2f), 1.0f);
                    sweepPixelArray[i] = new Color(Mathf.Pow(sweepPixelColor.r, 1.0f / 2.2f), Mathf.Pow(sweepPixelColor.g, 1.0f / 2.2f), Mathf.Pow(sweepPixelColor.b, 1.0f / 2.2f), 1.0f);

                }
            }

            hdriTextureTransformed.SetPixels(hdriPixelArray);
            hdriTextureTransformed.Apply();

            sweepTextureTransformed.SetPixels(sweepPixelArray);
            sweepTextureTransformed.Apply();
        }
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


    //private void cpuGGM()
    //{
    //    Color[] pixels = inputTexture.GetPixels();
    //    //Color[] pixels2 = inputTexture.GetPixels();

    //    for (int i = 0; i < pixels.Length; i++)
    //    {
    //        Color col = pixels[i] * exposureControl;
    //        //logOutputStrBuilder.AppendLine("B4 map" + col.ToString());
    //        ////logOutput += ("B4 map RGB" + col.ToString() + "\n");
    //        //col.r = dstCurve.Eval(col.r);
    //        //col.g = dstCurve.Eval(col.g);
    //        //col.b = dstCurve.Eval(col.b);
    //        //logOutputStrBuilder.AppendLine("Af map" + col.ToString());
    //        //logOutput += ("Af map RGB" + col.ToString() + "\n");

    //        //float val = System.Math.Max(col.r, System.Math.Max(col.g, col.b));
    //        //if (val > 0.85)
    //        //{
    //        //    if (val == col.r)
    //        //    {
    //        //        logOutput += ("B4 map " + col.ToString() + "\n");
    //        //        col = colorRemap(col, 0.0f);
    //        //        logOutput += ("Af map " + col.ToString() + "\n");
    //        //    }
    //        //    else if (val == col.g)
    //        //    {
    //        //        logOutput += ("B4 map " + col.ToString() + "\n");
    //        //        col = colorRemap(col, 1.0f);
    //        //        logOutput += ("Af map " + col.ToString() + "\n");
    //        //    }
    //        //    else if (val == col.b)
    //        //    {
    //        //        logOutput += ("B4 map " + col.ToString() + "\n");
    //        //        col = colorRemap(col, 2.0f);
    //        //        logOutput += ("Af map " + col.ToString() + "\n");
    //        //    }
    //        //}
    //        //logOutputStrBuilder.AppendLine("B4 map " + col.ToString());

    //        pixels[i] = newGGM(pixels[i], 1.0f);
    //        //float tmp = Mathf.Max(col.r, Mathf.Max(col.g, col.b));
    //        //tmp = dstCurve.Eval(tmp);
    //        //Scale all other values
    //        //col.r = tmp * col.r;

    //        col.r = dstCurve.Eval(col.r);
    //        col.g = dstCurve.Eval(col.g);
    //        col.b = dstCurve.Eval(col.b);


    //        pixels[i] = new Color(Mathf.Pow(col.r, 1.0f / 2.2f), Mathf.Pow(col.g, 1.0f / 2.2f), Mathf.Pow(col.b, 1.0f / 2.2f), 1.0f);
    //        //logOutputStrBuilder.AppendLine("Af map " + col.ToString());

    //        //pixels2[i] = newGGM(pixels2[i], 0.85f);
    //    }
    //    //System.IO.File.WriteAllText(@".\Log.txt", logOutputStrBuilder.ToString());
    //    //textureToSave = new Texture2D(inputTexture.width, inputTexture.height);
    //    textureToSave.SetPixels(pixels);
    //    textureToSave.Apply();
    //    //File.WriteAllBytes(@"GGM_Output.png", textureToSave.EncodeToPNG());
    //    //textureToSave = new Texture2D(inputTexture.width, inputTexture.height);
    //    //textureToSave.SetPixels(pixels2);
    //    //File.WriteAllBytes(@"GGM2_Output.png", textureToSave.EncodeToPNG());
    //    //logOutputStrBuilder.Clear();
    //    //logOutput = "";

    //}
}
