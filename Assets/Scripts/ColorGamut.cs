using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class ColorGamut : MonoBehaviour
{
    private float enableGamutMap;
    [Range(0.01f, 20.0f)]
    public float exposureControl;
    public Material colorGamut;
    private bool useTanHCompressionFunction;
    private RenderTexture screenGrab;
    private float CPUMode;
    public Texture2D inputTexture;
    public Material fullScreenTexture;
    private string logOutput = "";
    private StringBuilder logOutputStrBuilder;
    public GameObject secondPlane;



    public enum ShoulderLength
    {
        F_2_8,
        F_4_0,
        F_5_6,
        F_8_0,
        F_11_0
    }
    //public float threshold = 1.0f;
    public enum LerpRatio {
        Aesthetic,
        Radiometric
    }
    [Header("GGM")]
    public LerpRatio lerpRatio;
    public float dye_bleach_x = 1.0f;
    public float dye_bleach_y = 1.0f;
    public enum TransferFunction
    {
        Per_Channel,
        Max_RGB
    }
    [Space]
    [Header("Hable Transfer Function")]
    public TransferFunction activeTransferFunction;
    [Space]

    [Header ("Hable Curve Parameters")]
    #region DIRECT_PARAMS
    private float m_x0 = 0.18f;      //dstParams.m_x0  min 0        max 0.5
    private float m_y0 = 0.18f;      //dstParams.m_y0  min 0        max 0.5
    private float m_x1 = 0.75f;      //dstParams.m_x1  min 0        max 1.5
    private float m_y1 = 0.75f;      //dstParams.m_y1  min 0        max .99999 
    private float m_W = 1.0f;        //dstParams.m_W   min 1        max 2.5
    private float m_overshootX = 0.0f;
    private float m_overshootY = 0.0f;
    #endregion

    private bool KeyIsUp = false;
    private bool ApplyTexture = false;
    private FullCurve dstCurve;
    private CurveParamsUser userCurveParams;
    private Texture2D textureToSave;
    private Ggm_troyedition ggm;

    private void Awake()
    {
        m_x0 = 0.18f;
        m_y0 = 0.18f;
        m_x1 = 0.75f;
        m_y1 = 0.75f;
        m_W = 1.0f;
        m_overshootX = 0.0f;
        m_overshootY = 0.0f;
        activeTransferFunction = TransferFunction.Max_RGB;

        // GGM parameters 
        //threshold = 1.0f;
        lerpRatio = LerpRatio.Aesthetic;
    }

    void Start()
    {
        screenGrab = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        exposureControl = 1.0f;
        useTanHCompressionFunction = false;
        enableGamutMap = 1.0f;
        CPUMode = 0.0f;

        dstCurve = new FullCurve();
        userCurveParams = new CurveParamsUser();
        dstCurve = FilmicToneCurve.CreateCurve(FilmicToneCurve.CalcDirectParamsFromUser(userCurveParams));
        logOutputStrBuilder = new StringBuilder(10000);
        textureToSave = new Texture2D(inputTexture.width, inputTexture.height);
        ggm = new Ggm_troyedition();

        StartCoroutine("CpuGGMIterative");
    }

    public float getX0() { return m_x0; }
    public float getY0() { return m_y0; }
    public float getX1() { return m_x1; }
    public float getY1() { return m_y1; }
    public float getW() { return m_W; }

    public void setCurveValues(float x0, float y0, float x1, float y1, float w, float overShootX, float overShootY) 
    {
        if (x0 != m_x0) 
        {
            m_x0 = x0;
        }
        if (y0 != m_y0)
        {
            m_y0 = y0;
        }
        if (x1 != m_x1)
        {
            m_x1 = x1;
        }
        if (y1 != m_y1)
        {
            m_y1 = y1;
        }
        if (w != m_W)
        {
            m_W = w;
        }
        if (overShootX != m_overshootX) 
        {
            m_overshootX = overShootX;
        }
        if (overShootY != m_overshootY)
        {
            m_overshootY = overShootY;
        }
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
            blue =  (col.r != col.b) ? Mathf.Lerp(blue, col.r, newRangeMin) : col.b;

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
       
            red     = (col.b != col.r) ? Mathf.Lerp(red, col.b, newRangeMin) : col.r;
            green   = (col.b != col.g) ? Mathf.Lerp(green, col.b, newRangeMin) : col.g;
            outColor = new Color(red, green, col.b);
        }

        return new Color(Mathf.Clamp01(outColor.r), Mathf.Clamp01(outColor.g), Mathf.Clamp01(outColor.b));
    }

    void Update() 
    {
        CurveParamsDirect paramsDirect = new CurveParamsDirect();
        paramsDirect.m_x0 = m_x0;
        paramsDirect.m_y0 = m_y0;
        paramsDirect.m_x1 = m_x1;
        paramsDirect.m_y1 = m_y1;
        paramsDirect.m_W  = m_W;
        paramsDirect.m_overshootX = m_overshootX;
        paramsDirect.m_overshootY = m_overshootY;

        Debug.Log("X0: " + m_x0 + "   Y0: " + m_y0 + "  X1: " + m_x1 + "  Y1: " + m_y1 + " W: " + m_W);

        dstCurve = FilmicToneCurve.CreateCurve(paramsDirect);

        if (Input.GetKeyUp(KeyCode.T))
            KeyIsUp = true;

        if(Input.GetKeyUp(KeyCode.Y))
            KeyIsUp = false;

    }

    Color newGGM(Color RGB, float compressionThreshold) 
    {
        Vector3 result = ggm.the_ggm(new Vector3(RGB.r, RGB.g, RGB.b), new Vector3(compressionThreshold, compressionThreshold, compressionThreshold));
        return new Color(result.x, result.y, result.z);
    }
    private void OnPreRender()
    {
        secondPlane.GetComponent<MeshRenderer>().material.SetTexture("_MainTex", textureToSave);
    }
    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        Graphics.Blit(src, screenGrab, fullScreenTexture);

        //cpuGGM();
        colorGamut.SetTexture("_MainTex", screenGrab);
        colorGamut.SetFloat("_DoGamutMap", enableGamutMap);
        colorGamut.SetFloat("_ExposureControl", exposureControl);
        colorGamut.SetFloat("_TanHCompression", (useTanHCompressionFunction == false ? 0.0f : 1.0f));

        Graphics.Blit(screenGrab, dest, colorGamut);
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


    IEnumerator CpuGGMIterative() 
    {
        while (true)
        {

            Color[] pixels = inputTexture.GetPixels();
            int counter = 10000;
            for (int i = 0; i < pixels.Length; i++, counter--)
            {
                if (counter <= 0)
                {
                    counter = 10000;
                    yield return new WaitForEndOfFrame();
                }

                // Full dynamic range of image
                Color col = pixels[i] * exposureControl;

                // Timothy Lottes Max approach
                float maxColor = col.maxColorComponent;
                Color ratio = col / maxColor;

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
              
                //float lerp_ratio = 0.0f;
                //if (col.r > dye_bleach_x || col.g > dye_bleach_x || col.b > dye_bleach_x)
                //{
                //    if (lerpRatio == LerpRatio.Aesthetic)
                //    {
                //        // Y ratio
                //        lerp_ratio = (dstCurve.Eval(maxColor) - 1.0f) / (dye_bleach_y - 1.0f);
                //    }
                //    else
                //    {
                //        // X ratio
                //        lerp_ratio = (maxColor - dye_bleach_x) / (m_W - dye_bleach_x);
                //    }

                //    Color remaining_space = new Color(maxColor - col.r, maxColor - col.g, maxColor - col.b);
                //    Color additive_light = lerp_ratio * remaining_space;
                //    Color new_bleached_color = col + additive_light;
                //    ratio = new_bleached_color / maxColor;
                //}

                // Secondary Nuance Grade, guardrails
                //    if (col.r > m_W || col.g > m_W || col.b > m_W) 
                //{
                //    //col.r = m_W;
                //    //col.g = m_W;
                //    //col.b = m_W;
                //}
                //col = newGGM(col, 1.0f);

                // Hable's transfer function
                if (KeyIsUp || activeTransferFunction == TransferFunction.Max_RGB)
                {
                    maxColor = dstCurve.Eval(maxColor);
                    activeTransferFunction = TransferFunction.Max_RGB;
                    col = maxColor * ratio;
                }
                else
                {
                    activeTransferFunction = TransferFunction.Per_Channel;
                    col.r = dstCurve.Eval(col.r);
                    col.g = dstCurve.Eval(col.g);
                    col.b = dstCurve.Eval(col.b);
                }
                // GGM - Here doesn't make sense 
                //col = newGGM(col, 1.0f);

                pixels[i] = new Color(Mathf.Pow(col.r, 1.0f / 2.2f), Mathf.Pow(col.g, 1.0f / 2.2f), Mathf.Pow(col.b, 1.0f / 2.2f), 1.0f);
            }

            textureToSave.SetPixels(pixels);
            textureToSave.Apply();
        }
    }

    private void cpuGGM()
    {
        Color[] pixels = inputTexture.GetPixels();
        //Color[] pixels2 = inputTexture.GetPixels();

        for (int i = 0; i < pixels.Length; i++)
        {
            Color col = pixels[i] * exposureControl;
            //logOutputStrBuilder.AppendLine("B4 map" + col.ToString());
            ////logOutput += ("B4 map RGB" + col.ToString() + "\n");
            //col.r = dstCurve.Eval(col.r);
            //col.g = dstCurve.Eval(col.g);
            //col.b = dstCurve.Eval(col.b);
            //logOutputStrBuilder.AppendLine("Af map" + col.ToString());
            //logOutput += ("Af map RGB" + col.ToString() + "\n");

            //float val = System.Math.Max(col.r, System.Math.Max(col.g, col.b));
            //if (val > 0.85)
            //{
            //    if (val == col.r)
            //    {
            //        logOutput += ("B4 map " + col.ToString() + "\n");
            //        col = colorRemap(col, 0.0f);
            //        logOutput += ("Af map " + col.ToString() + "\n");
            //    }
            //    else if (val == col.g)
            //    {
            //        logOutput += ("B4 map " + col.ToString() + "\n");
            //        col = colorRemap(col, 1.0f);
            //        logOutput += ("Af map " + col.ToString() + "\n");
            //    }
            //    else if (val == col.b)
            //    {
            //        logOutput += ("B4 map " + col.ToString() + "\n");
            //        col = colorRemap(col, 2.0f);
            //        logOutput += ("Af map " + col.ToString() + "\n");
            //    }
            //}
            //logOutputStrBuilder.AppendLine("B4 map " + col.ToString());

            pixels[i] = newGGM(pixels[i], 1.0f);
            //float tmp = Mathf.Max(col.r, Mathf.Max(col.g, col.b));
            //tmp = dstCurve.Eval(tmp);
            //Scale all other values
            //col.r = tmp * col.r;

            col.r = dstCurve.Eval(col.r);
            col.g = dstCurve.Eval(col.g);
            col.b = dstCurve.Eval(col.b);


            pixels[i] = new Color(Mathf.Pow(col.r, 1.0f / 2.2f), Mathf.Pow(col.g, 1.0f / 2.2f), Mathf.Pow(col.b, 1.0f / 2.2f), 1.0f);
            //logOutputStrBuilder.AppendLine("Af map " + col.ToString());

            //pixels2[i] = newGGM(pixels2[i], 0.85f);
        }
        //System.IO.File.WriteAllText(@".\Log.txt", logOutputStrBuilder.ToString());
        //textureToSave = new Texture2D(inputTexture.width, inputTexture.height);
        textureToSave.SetPixels(pixels);
        textureToSave.Apply();
        //File.WriteAllBytes(@"GGM_Output.png", textureToSave.EncodeToPNG());
        //textureToSave = new Texture2D(inputTexture.width, inputTexture.height);
        //textureToSave.SetPixels(pixels2);
        //File.WriteAllBytes(@"GGM2_Output.png", textureToSave.EncodeToPNG());
        //logOutputStrBuilder.Clear();
        //logOutput = "";

    }
}
