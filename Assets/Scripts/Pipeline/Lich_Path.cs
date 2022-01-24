using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class Lich_Path : MonoBehaviour
{
    public Material fullScreenTextureMat;
    public Material gamutMapMat;
    public Material lichMat;

    public List<Texture2D> HDRIList;

    private GamutMap colorGamut;
    private RenderTexture renderBuffer;
    private RenderTexture hdriRenderTexture;
    private RenderTexture gamutMapRT;

    public ComputeBuffer XCurveCoordsCBuffer => xCurveCoordsCBuffer;
    private ComputeBuffer xCurveCoordsCBuffer;
    public ComputeBuffer YCurveCoordsCBuffer => yCurveCoordsCBuffer;
    private ComputeBuffer yCurveCoordsCBuffer;
    public Vector4[] ControlPointsUniform => controlPointsUniform;
    private Vector4[] controlPointsUniform;

    private Texture2D hdriTextureTransformed;
    static float maxNits = 100.0f;                       // Maximum nit value we support

    static float maxDisplayValue = 100.0f / maxNits;     // in SDR we support a maximum of 100 nits
    static float minDisplayValue = 0.05f / maxNits;      // in SDR we support a minimum of 0.05f nits which is an average black value for a LED display
    static Vector2 midGreySDR = new Vector2(18.0f / maxNits, 18.0f / maxNits);
    static float midRadiometricGrey = 0.18f;
    static float minRadiometricExposure = -7.0f;
    static float maxRadiometricExposure = 6.0f;

    float totalRadiometricExposure = maxRadiometricExposure - minRadiometricExposure;

    float minDisplayExposure = Mathf.Log(minDisplayValue / midGreySDR.y, 2.0f);
    float maxDisplayExposure = Mathf.Log(maxDisplayValue / midGreySDR.y, 2.0f);
    static float minRadiometricValue = Mathf.Pow(2.0f, minRadiometricExposure) * midGreySDR.x;
    static float maxRadiometricValue = Mathf.Pow(2.0f, maxRadiometricExposure) * midGreySDR.x;
    private bool enableLICH = true;

    void Start()
    {
        renderBuffer = new RenderTexture(HDRIList[0].width, HDRIList[0].height, 0, RenderTextureFormat.ARGBHalf,
       RenderTextureReadWrite.Linear);
        hdriRenderTexture = new RenderTexture(HDRIList[0].width, HDRIList[0].height, 0, RenderTextureFormat.ARGBHalf,
            RenderTextureReadWrite.Linear);
        gamutMapRT = new RenderTexture(HDRIList[0].width, HDRIList[0].height, 0, RenderTextureFormat.ARGBHalf,
            RenderTextureReadWrite.Linear);
        initialiseColorGamut();
        controlPointsUniform = new Vector4[7];
        hdriTextureTransformed = new Texture2D(HDRIList[0].width, HDRIList[0].height, TextureFormat.RGBAHalf, false, true);

        xCurveCoordsCBuffer = new ComputeBuffer(1024, sizeof(float));
        yCurveCoordsCBuffer = new ComputeBuffer(1024, sizeof(float));

        //Color[] testColor = { new Color(0.25f, 0.5f, 0.75f) };
        //lichDebug(testColor, 1.0f);


        // Test
        Debug.Log("Linear Result: " + 0.5f / 0.324f);
        //float numerator = Shaper.calculateLinearToLog2(0.5f, colorGamut.MidGreySdr.x, colorGamut.MinRadiometricExposure, colorGamut.MaxRadiometricExposure); 
        //float denominator = Shaper.calculateLinearToLog2(0.324f, colorGamut.MidGreySdr.x, colorGamut.MinRadiometricExposure, colorGamut.MaxRadiometricExposure);
        //float logResult = numerator / denominator;
        //float linResult = Shaper.calculateLog2ToLinear(logResult, colorGamut.MidGreySdr.x, colorGamut.MinRadiometricExposure, colorGamut.MaxRadiometricExposure);
        //Debug.Log("Linear-Log-Linear Result: " + linResult + " " + logResult);
        float luminanceTarget = 0.3f;
        if (enableLICH)
        {
            //lichCPU(0.0f);
            //lichLinear  (luminanceTarget);
            lichLog     (luminanceTarget);
            //lichCPU     (luminanceTarget);
            //lichCPU(0.5f);
            //lichCPU(0.8f);
        }
        else 
        {
            Graphics.CopyTexture(HDRIList[0], hdriTextureTransformed);
            //Graphics.Blit(HDRIList[0], hdriTextureTransformed, fullScreenTextureMat);
        }

    }
    private void initialiseColorGamut()
    {
        colorGamut = new GamutMap(fullScreenTextureMat, HDRIList);
        colorGamut.Start(this.gameObject.GetComponent<Camera>());
    }

    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (true)
        {
            // Highlight compression via LICH
            //lichMat.SetTexture("_MainTex", HDRIList[0]);
            //lichMat.SetVector("greyPoint", new Vector4(colorGamut.MidGreySdr.x, colorGamut.MidGreySdr.y, 0.0f));
            //lichMat.SetFloat("minRadiometricExposure", colorGamut.MinRadiometricExposure);
            //lichMat.SetFloat("maxRadiometricExposure", colorGamut.MaxRadiometricExposure);
            //Graphics.Blit(HDRIList[0], hdriRenderTexture, lichMat);
            fullScreenTextureMat.SetTexture("_MainTex", hdriTextureTransformed);
            Graphics.Blit(hdriTextureTransformed, hdriRenderTexture, fullScreenTextureMat);
            // Final gamut mapping
            gamutMapMat.SetTexture("_MainTex", hdriRenderTexture);
            gamutMapMat.SetVector("greyPoint", new Vector4(colorGamut.MidGreySdr.x, colorGamut.MidGreySdr.y, 0.0f));
            gamutMapMat.SetFloat("minRadiometricExposure", colorGamut.MinRadiometricExposure);
            gamutMapMat.SetFloat("maxRadiometricExposure", colorGamut.MaxRadiometricExposure);
            gamutMapMat.SetFloat("maxRadiometricValue", colorGamut.MaxRadiometricDynamicRange);
            gamutMapMat.SetFloat("minDisplayExposure", colorGamut.MinDisplayExposure);
            gamutMapMat.SetFloat("maxDisplayExposure", colorGamut.MaxDisplayExposure);
            gamutMapMat.SetFloat("minDisplayValue", colorGamut.MinDisplayValue);
            gamutMapMat.SetFloat("maxDisplayValue", colorGamut.MaxDisplayValue);
            gamutMapMat.SetInt("inputArraySize", colorGamut.getXValues().Count - 1);
            gamutMapMat.SetInt("heatmap", 0);

            xCurveCoordsCBuffer.SetData(colorGamut.getXValues().ToArray());
            yCurveCoordsCBuffer.SetData(colorGamut.getYValues().ToArray());
            gamutMapMat.SetBuffer(Shader.PropertyToID("xCurveCoordsCBuffer"), xCurveCoordsCBuffer);
            gamutMapMat.SetBuffer(Shader.PropertyToID("yCurveCoordsCBuffer"), yCurveCoordsCBuffer);
            gamutMapMat.SetVectorArray("controlPoints", controlPointsUniform);

            Graphics.Blit(hdriRenderTexture, gamutMapRT, gamutMapMat);
            fullScreenTextureMat.SetTexture("_MainTex", gamutMapRT);
            Graphics.Blit(gamutMapRT, dest, fullScreenTextureMat);
        }
        else 
        {
            Graphics.Blit(hdriTextureTransformed, dest, fullScreenTextureMat);

        }
    }

    public void lichDebug(Color[] testColor, float luminanceRelativeTarget)
    {
        Color[] hdriPixelArray = testColor;
        int hdriPixelArrayLen = hdriPixelArray.Length;
        Color logRGBInput;
        Vector3 logRGBInputVec = Vector3.zero;
        for (int i = 0; i < hdriPixelArrayLen; i++)
        {
            logRGBInput.r = Shaper.calculateLinearToLog2(hdriPixelArray[i].r, colorGamut.MidGreySdr.x, colorGamut.MinRadiometricExposure, colorGamut.MaxRadiometricExposure);
            logRGBInput.g = Shaper.calculateLinearToLog2(hdriPixelArray[i].g, colorGamut.MidGreySdr.x, colorGamut.MinRadiometricExposure, colorGamut.MaxRadiometricExposure);
            logRGBInput.b = Shaper.calculateLinearToLog2(hdriPixelArray[i].b, colorGamut.MidGreySdr.x, colorGamut.MinRadiometricExposure, colorGamut.MaxRadiometricExposure);
            logRGBInputVec = new Vector3(logRGBInput.r, logRGBInput.g, logRGBInput.b);

            Vector3 luminanceWeights = new Vector3(0.2126f, 0.7152f, 0.0722f);
            // Troy Sobotka, 2021, "EVILS - Exposure Value Invariant Luminance Scaling"
            // https://colab.research.google.com/drive/1iPJzNNKR7PynFmsqSnQm3bCZmQ3CvAJ-#scrollTo=psU43hb-BLzB

            float maxRGBInput = Mathf.Max(logRGBInput.r, Mathf.Max(logRGBInput.g, logRGBInput.b));
            Vector3 maxChroma = Vector3.Max(logRGBInputVec / maxRGBInput, Vector3.zero);
            Debug.Log("maxChroma: " + maxChroma.ToString("G8"));

            float luminanceMaxChroma = Vector3.Dot(maxChroma, luminanceWeights);
            Debug.Log("luminanceMaxChroma: " + luminanceMaxChroma.ToString("G8"));

            Vector3 energyLeft = Vector3.one - maxChroma;
            Debug.Log("energyLeft: " + energyLeft.ToString("G8"));

            float luminanceEnergyLeft = Vector3.Dot(energyLeft, luminanceWeights);
            Debug.Log("luminanceEnergyLeft: " + luminanceEnergyLeft.ToString("G8"));

            float luminanceDifferenceMaxDisplayAndMaxChroma = Mathf.Max(luminanceRelativeTarget - luminanceMaxChroma, 0.0f);
            Debug.Log("luminanceDifferenceMaxDisplayAndMaxChroma: " + luminanceDifferenceMaxDisplayAndMaxChroma.ToString("G8"));

            float scaledLuminanceDifference = luminanceDifferenceMaxDisplayAndMaxChroma / Mathf.Max(luminanceEnergyLeft, 0.0001f);
            Debug.Log("scaledLuminanceDifference: " + scaledLuminanceDifference.ToString("G8"));

            float chromaScale = (luminanceRelativeTarget - luminanceDifferenceMaxDisplayAndMaxChroma) / Mathf.Max(luminanceMaxChroma, 0.0001f);
            Debug.Log("chromaScale: " + chromaScale.ToString("G8"));

            Vector3 reserves_compliment = scaledLuminanceDifference * energyLeft;
            Debug.Log("reserves_compliment: " + reserves_compliment.ToString("G8"));

            Vector3 chroma_scaled = chromaScale * maxChroma;
            Debug.Log("chroma_scaled: " + chroma_scaled.ToString("G8"));

            Vector3 outputColour = chroma_scaled + reserves_compliment;
            hdriPixelArray[i] = new Color(outputColour.x, outputColour.y, outputColour.z);
        }

        foreach (Color c in hdriPixelArray) 
        {
            Debug.Log(hdriPixelArray.ToString());
        }
    }

    public void lichLinear(float luminanceRelativeTarget) 
    {
        Color[] hdriPixelArray = HDRIList[0].GetPixels();
        int hdriPixelArrayLen = hdriPixelArray.Length;
        Color rgbInput;
        Vector3 rgbInputVec = Vector3.zero;

        // BT.709 weights
        Vector3 luminanceWeights = new Vector3(0.2126f, 0.7152f, 0.0722f);

        for (int i = 0; i < hdriPixelArrayLen; i++)
        {
            rgbInput.r = hdriPixelArray[i].r;
            rgbInput.g = hdriPixelArray[i].g;
            rgbInput.b = hdriPixelArray[i].b;
            rgbInputVec = new Vector3(rgbInput.r, rgbInput.g, rgbInput.b);

            // Troy Sobotka, 2021, "EVILS - Exposure Value Invariant Luminance Scaling"
            // https://colab.research.google.com/drive/1iPJzNNKR7PynFmsqSnQm3bCZmQ3CvAJ-#scrollTo=psU43hb-BLzB

            float maxRGBInput = Mathf.Max(rgbInput.r, Mathf.Max(rgbInput.g, rgbInput.b));
            Vector3 maxChroma = Vector3.Max(rgbInputVec / maxRGBInput, Vector3.zero);
            //Debug.Log("maxChroma: " + maxChroma.ToString("G8"));

            float luminanceMaxChroma = Vector3.Dot(maxChroma, luminanceWeights);
            //Debug.Log("luminanceMaxChroma: " + luminanceMaxChroma.ToString("G8"));

            Vector3 energyLeft = Vector3.one - maxChroma;
            //Debug.Log("energyLeft: " + energyLeft.ToString("G8"));

            float luminanceEnergyLeft = Vector3.Dot(energyLeft, luminanceWeights);
            //Debug.Log("luminanceEnergyLeft: " + luminanceEnergyLeft.ToString("G8"));

            float luminanceDifferenceMaxDisplayAndMaxChroma = Mathf.Max(luminanceRelativeTarget - luminanceMaxChroma, 0.0f);
            //Debug.Log("luminanceDifferenceMaxDisplayAndMaxChroma: " + luminanceDifferenceMaxDisplayAndMaxChroma.ToString("G8"));

            float scaledLuminanceDifference = luminanceDifferenceMaxDisplayAndMaxChroma / Mathf.Max(luminanceEnergyLeft, 0.0001f);
            //Debug.Log("scaledLuminanceDifference: " + scaledLuminanceDifference.ToString("G8"));

            float chromaScale = (luminanceRelativeTarget - luminanceDifferenceMaxDisplayAndMaxChroma) / Mathf.Max(luminanceMaxChroma, 0.0001f);
            //Debug.Log("chromaScale: " + chromaScale.ToString("G8"));

            Vector3 reserves_compliment = scaledLuminanceDifference * energyLeft;
            //Debug.Log("reserves_compliment: " + reserves_compliment.ToString("G8"));

            Vector3 chroma_scaled = chromaScale * maxChroma;
            //Debug.Log("chroma_scaled: " + chroma_scaled.ToString("G8"));

            Vector3 outputColour = chroma_scaled + reserves_compliment;

            hdriPixelArray[i] = new Color(outputColour.x, outputColour.y, outputColour.z);
        }
        Color[] hdriPixelArray2 = new Color[hdriPixelArray.Length];
        rgbInput = Color.black;

        for (int i = 0; i < hdriPixelArray.Length; i++)
        {
            rgbInput.r = hdriPixelArray[i].r;
            rgbInput.g = hdriPixelArray[i].g;
            rgbInput.b = hdriPixelArray[i].b;
            hdriPixelArray2[i] = new Color(rgbInput.r, rgbInput.g, rgbInput.b);
        }
        SaveToDisk(hdriPixelArray, "lichLinear.exr", hdriTextureTransformed.width, hdriTextureTransformed.height);


    }

    public void lichLog(float luminanceRelativeTarget)
    {
        Color[] hdriPixelArray = HDRIList[0].GetPixels();
        int hdriPixelArrayLen = hdriPixelArray.Length;
        Color rgbInput;
        Vector3 rgbInputVec = Vector3.zero;

        float maxLogValue = Shaper.calculateLinearToLog2(maxRadiometricValue, colorGamut.MidGreySdr.x, colorGamut.MinRadiometricExposure, colorGamut.MaxRadiometricExposure);
        float minLogValue = Shaper.calculateLinearToLog2(minRadiometricValue, colorGamut.MidGreySdr.x, colorGamut.MinRadiometricExposure, colorGamut.MaxRadiometricExposure);
        Vector3 maxLog = new Vector3(maxLogValue, maxLogValue, maxLogValue);
        Vector3 minLog = new Vector3(minLogValue, minLogValue, minLogValue);

        float dynamicRange = maxRadiometricValue - minRadiometricValue;

        // BT.709 weights
        Vector3 luminanceWeights = new Vector3(0.2126f, 0.7152f, 0.0722f);

        for (int i = 0; i < hdriPixelArrayLen; i++)
        {
            //logRGBInput.r = Shaper.calculateLinearToLog2(hdriPixelArray[i].r, colorGamut.MidGreySdr.x, colorGamut.MinRadiometricExposure, colorGamut.MaxRadiometricExposure);
            //logRGBInput.g = Shaper.calculateLinearToLog2(hdriPixelArray[i].g, colorGamut.MidGreySdr.x, colorGamut.MinRadiometricExposure, colorGamut.MaxRadiometricExposure);
            //logRGBInput.b = Shaper.calculateLinearToLog2(hdriPixelArray[i].b, colorGamut.MidGreySdr.x, colorGamut.MinRadiometricExposure, colorGamut.MaxRadiometricExposure);


            rgbInput.r = hdriPixelArray[i].r;
            rgbInput.g = hdriPixelArray[i].g;
            rgbInput.b = hdriPixelArray[i].b;


            // Log converted values for the defined range
            rgbInputVec = new Vector3(rgbInput.r, rgbInput.g, rgbInput.b);

            // Troy Sobotka, 2021, "EVILS - Exposure Value Invariant Luminance Scaling"
            // https://colab.research.google.com/drive/1iPJzNNKR7PynFmsqSnQm3bCZmQ3CvAJ-#scrollTo=psU43hb-BLzB

            float maxRGBInput = Mathf.Max(rgbInput.r, Mathf.Max(rgbInput.g, rgbInput.b));
       

            // Max Chroma from a division of two log values.
            Vector3 normalizedChroma = Vector3.Max(rgbInputVec / maxRGBInput, Vector3.zero);
            //Debug.Log("maxChroma: " + maxChroma.ToString("G8"));

            // Luminance calculation from Log Max Chroma with luminance weights.
            // luminanceMaxChroma is in log
            float luminanceNormalizedChroma = Vector3.Dot(normalizedChroma, luminanceWeights);
            //Debug.Log("luminanceMaxChroma: " + luminanceMaxChroma.ToString("G8"));

            float luminanceRGBInput = Vector3.Dot(rgbInputVec, luminanceWeights);

            float luminanceRelativeCameraEncoding = Mathf.Clamp01(luminanceRGBInput / dynamicRange);

            //if bright > normluminance
            //       Dechroma


            // Remaining energy left: maxLog is in log and matches the maximum value in our range
            //                        maxChroma is also in log
            Vector3 energyLeft = maxLog - normalizedChroma;
            //Debug.Log("energyLeft: " + energyLeft.ToString("G8"));

            // energy left is in log
            // luminanceWeights corresponds to BT.709 weights
            // luminanceEnergyLeft is in log
            float luminanceEnergyLeft = Vector3.Dot(energyLeft, luminanceWeights);
            //Debug.Log("luminanceEnergyLeft: " + luminanceEnergyLeft.ToString("G8"));

            // luminanceDifferenceMaxDisplayAndMaxChroma = [luminanceRelativeTarget - luminanceMaxChroma]
            // luminanceMaxChroma is in log
            // Max(...) operation should be fine between Log values
            float luminanceDifferenceMaxDisplayAndMaxChroma = Mathf.Max(luminanceRelativeTarget - luminanceNormalizedChroma, 0.0f);
            //Debug.Log("luminanceDifferenceMaxDisplayAndMaxChroma: " + luminanceDifferenceMaxDisplayAndMaxChroma.ToString("G8"));
            
            // scaleLogLuminanceDifference here is in log
            float scaledLuminanceDifference = luminanceDifferenceMaxDisplayAndMaxChroma / Mathf.Max(luminanceEnergyLeft, 0.0001f);
            //Debug.Log("scaledLuminanceDifference: " + scaledLuminanceDifference.ToString("G8"));

            // chromaScale here is in log
            float chromaScale = (luminanceRelativeTarget - luminanceDifferenceMaxDisplayAndMaxChroma) / Mathf.Max(luminanceNormalizedChroma, 0.0001f);
            //Debug.Log("chromaScale: " + chromaScale.ToString("G8"));

            Vector3 reserves_compliment = scaledLuminanceDifference * energyLeft;
            //Debug.Log("reserves_compliment: " + reserves_compliment.ToString("G8"));

            Vector3 chroma_scaled = chromaScale * normalizedChroma;
            //Debug.Log("chroma_scaled: " + chroma_scaled.ToString("G8"));

            Vector3 outputColour = chroma_scaled + reserves_compliment;
            // Logify here
            hdriPixelArray[i] = new Color(outputColour.x, outputColour.y, outputColour.z);
        }
        Color[] hdriPixelArray2 = new Color[hdriPixelArray.Length];
        //Color rgbInput = Color.black;

        for (int i = 0; i < hdriPixelArray.Length; i++)
        {
            rgbInput.r = Shaper.calculateLog2ToLinear(hdriPixelArray[i].r, colorGamut.MidGreySdr.x, colorGamut.MinRadiometricExposure, colorGamut.MaxRadiometricExposure);
            rgbInput.g = Shaper.calculateLog2ToLinear(hdriPixelArray[i].g, colorGamut.MidGreySdr.x, colorGamut.MinRadiometricExposure, colorGamut.MaxRadiometricExposure);
            rgbInput.b = Shaper.calculateLog2ToLinear(hdriPixelArray[i].b, colorGamut.MidGreySdr.x, colorGamut.MinRadiometricExposure, colorGamut.MaxRadiometricExposure);
            hdriPixelArray2[i] = new Color(rgbInput.r, rgbInput.g, rgbInput.b);
        }
        SaveToDisk(hdriPixelArray, "lichLog.exr", hdriTextureTransformed.width, hdriTextureTransformed.height);

        hdriTextureTransformed.SetPixels(hdriPixelArray);
        hdriTextureTransformed.Apply();
    }



    public void lichCPU(float luminanceRelativeTarget) 
    {
        Color[] hdriPixelArray = HDRIList[0].GetPixels();
        int hdriPixelArrayLen = hdriPixelArray.Length;
        Color logRGBInput;
        Vector3 logRGBInputVec = Vector3.zero;

        float maxLogValue = Shaper.calculateLinearToLog2(maxRadiometricValue, colorGamut.MidGreySdr.x, colorGamut.MinRadiometricExposure, colorGamut.MaxRadiometricExposure);
        float minLogValue = Shaper.calculateLinearToLog2(minRadiometricValue, colorGamut.MidGreySdr.x, colorGamut.MinRadiometricExposure, colorGamut.MaxRadiometricExposure);
        Vector3 maxLog = new Vector3(maxLogValue, maxLogValue, maxLogValue);
        Vector3 minLog = new Vector3(minLogValue, minLogValue, minLogValue);
        Vector3 dynamicRange = maxLog - minLog;
        // BT.709 weights
        Vector3 luminanceWeights = new Vector3(0.2126f, 0.7152f, 0.0722f);

        for (int i = 0; i < hdriPixelArrayLen; i++)
        {
            logRGBInput.r = Shaper.calculateLinearToLog2(hdriPixelArray[i].r, colorGamut.MidGreySdr.x, colorGamut.MinRadiometricExposure, colorGamut.MaxRadiometricExposure);
            logRGBInput.g = Shaper.calculateLinearToLog2(hdriPixelArray[i].g, colorGamut.MidGreySdr.x, colorGamut.MinRadiometricExposure, colorGamut.MaxRadiometricExposure);
            logRGBInput.b = Shaper.calculateLinearToLog2(hdriPixelArray[i].b, colorGamut.MidGreySdr.x, colorGamut.MinRadiometricExposure, colorGamut.MaxRadiometricExposure);
            // Log converted values for the defined range
            logRGBInputVec = new Vector3(logRGBInput.r, logRGBInput.g, logRGBInput.b);

            // Troy Sobotka, 2021, "EVILS - Exposure Value Invariant Luminance Scaling"
            // https://colab.research.google.com/drive/1iPJzNNKR7PynFmsqSnQm3bCZmQ3CvAJ-#scrollTo=psU43hb-BLzB

            // Max color input in Log. Max operation should be equivalent to Linear
            //float maxRGBInput = Mathf.Max(hdriPixelArray[i].r, Mathf.Max(hdriPixelArray[i].g, hdriPixelArray[i].b));
            float maxRGBLogInput = Mathf.Max(logRGBInput.r, Mathf.Max(logRGBInput.g, logRGBInput.b));
            
            // Max color input in Linear
            float maxRGBLinearInput = Mathf.Max(hdriPixelArray[i].r, Mathf.Max(hdriPixelArray[i].g, hdriPixelArray[i].b));

            // Max Chroma from a division of two log values. What is the meaning of division in log?
            Vector3 maxChroma2 = Vector3.Max(logRGBInputVec / maxRGBLogInput, Vector3.zero);
            // maxChroma here is in Linear, need to convert back to log
            Vector3 maxChroma = Vector3.Max(new Vector3(
                                            Shaper.calculateLog2ToLinear(logRGBInputVec.x, colorGamut.MidGreySdr.x, colorGamut.MinRadiometricExposure, colorGamut.MaxRadiometricExposure),
                                            Shaper.calculateLog2ToLinear(logRGBInputVec.y, colorGamut.MidGreySdr.x, colorGamut.MinRadiometricExposure, colorGamut.MaxRadiometricExposure),
                                            Shaper.calculateLog2ToLinear(logRGBInputVec.z, colorGamut.MidGreySdr.x, colorGamut.MinRadiometricExposure, colorGamut.MaxRadiometricExposure))
                                            / maxRGBLinearInput, Vector3.zero);
            maxChroma.x = Shaper.calculateLinearToLog2(maxChroma.x, colorGamut.MidGreySdr.x, colorGamut.MinRadiometricExposure, colorGamut.MaxRadiometricExposure);
            maxChroma.y = Shaper.calculateLinearToLog2(maxChroma.y, colorGamut.MidGreySdr.x, colorGamut.MinRadiometricExposure, colorGamut.MaxRadiometricExposure);
            maxChroma.z = Shaper.calculateLinearToLog2(maxChroma.z, colorGamut.MidGreySdr.x, colorGamut.MinRadiometricExposure, colorGamut.MaxRadiometricExposure);


            // Luminance calculation from Log Max Chroma with luminance weights.
            // luminanceMaxChroma is in log
            float luminanceMaxChroma = Vector3.Dot(maxChroma, luminanceWeights);

            // TODO: When changing calculations to log, Vector3.one is no longer one. 
            //       It needs to be calculated based on the log range being used
            //       Range = Max Nits - Min Nits
            //       Need to take care about the maximum energy being used.

            //Vector3 energyLeft = Vector3.one - maxChroma;
            // Remaining energy left: maxLog is in log and matches the maximum value in our range
            //                        maxChroma is also in log
            Vector3 energyLeft = maxLog - maxChroma;
            
            // energy left is in log
            // luminanceWeights corresponds to BT.709 weights
            // luminanceEnergyLeft is in log
            float luminanceEnergyLeft = Vector3.Dot(energyLeft, luminanceWeights);

            // luminanceRelativeTarget is in the open domain?
            // luminanceMaxChroma is in log
            // Max(...) operation should be fine in Log
            float luminanceDifferenceMaxDisplayAndMaxChroma = Mathf.Max(luminanceRelativeTarget - luminanceMaxChroma, 0.0f);

            // scaleLogLuminanceDifference here is in Linear
            float scaleLogLuminanceDifference = Shaper.calculateLog2ToLinear(luminanceDifferenceMaxDisplayAndMaxChroma, colorGamut.MidGreySdr.x, colorGamut.MinRadiometricExposure, colorGamut.MaxRadiometricExposure)
                                                /
                                                Shaper.calculateLog2ToLinear(Mathf.Max(luminanceEnergyLeft, 0.0001f), colorGamut.MidGreySdr.x, colorGamut.MinRadiometricExposure, colorGamut.MaxRadiometricExposure);
            // scaleLogLuminanceDifference here is in log
            scaleLogLuminanceDifference = Shaper.calculateLinearToLog2(scaleLogLuminanceDifference, colorGamut.MidGreySdr.x, colorGamut.MinRadiometricExposure, colorGamut.MaxRadiometricExposure);
            //float scaledLinearLuminanceDifference = luminanceDifferenceMaxDisplayAndMaxChroma / Mathf.Max(luminanceEnergyLeft, 0.0001f);

            // chromaScale here is in linear
            float chromaScale = Shaper.calculateLog2ToLinear((luminanceRelativeTarget - luminanceDifferenceMaxDisplayAndMaxChroma), colorGamut.MidGreySdr.x, colorGamut.MinRadiometricExposure, colorGamut.MaxRadiometricExposure)
                                /
                                Shaper.calculateLog2ToLinear(Mathf.Max(luminanceMaxChroma, 0.0001f), colorGamut.MidGreySdr.x, colorGamut.MinRadiometricExposure, colorGamut.MaxRadiometricExposure);
            // chromaScale here is in log
            chromaScale = Shaper.calculateLinearToLog2(scaleLogLuminanceDifference, colorGamut.MidGreySdr.x, colorGamut.MinRadiometricExposure, colorGamut.MaxRadiometricExposure);

            Vector3 outputColour = chromaScale * maxChroma + scaleLogLuminanceDifference * energyLeft;
            hdriPixelArray[i] = new Color(outputColour.x, outputColour.y, outputColour.z);
        }

        Color[] hdriPixelArray2 = new Color[hdriPixelArray.Length];
        Color rgbInput = Color.black;

        for (int i = 0; i < hdriPixelArray.Length; i++)
        {
            rgbInput.r = Shaper.calculateLog2ToLinear(hdriPixelArray[i].r, colorGamut.MidGreySdr.x, colorGamut.MinRadiometricExposure, colorGamut.MaxRadiometricExposure);
            rgbInput.g = Shaper.calculateLog2ToLinear(hdriPixelArray[i].g, colorGamut.MidGreySdr.x, colorGamut.MinRadiometricExposure, colorGamut.MaxRadiometricExposure);
            rgbInput.b = Shaper.calculateLog2ToLinear(hdriPixelArray[i].b, colorGamut.MidGreySdr.x, colorGamut.MinRadiometricExposure, colorGamut.MaxRadiometricExposure);
            hdriPixelArray2[i] = new Color(rgbInput.r, rgbInput.g, rgbInput.b);
        }
        SaveToDisk(hdriPixelArray, "NewCodeLogLinearLichedImage.exr", hdriTextureTransformed.width, hdriTextureTransformed.height);

        //hdriTextureTransformed.SetPixels(hdriPixelArray);
        //hdriTextureTransformed.Apply();

      
    }

    private void SaveToDisk(Color[] pixels, string fileName, int width, int height, bool useExr = true)
    {
        Debug.Log("Preparing to save texture to disk");

        if (useExr)
        {
            Texture2D textureToSave = new Texture2D(width, height, TextureFormat.RGBAHalf, false, true);
            textureToSave.SetPixels(pixels);
            textureToSave.Apply();
            File.WriteAllBytes(@fileName, textureToSave.EncodeToEXR());
        }
        else
        {
            Texture2D textureToSave = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            textureToSave.SetPixels(pixels);
            textureToSave.Apply();
            File.WriteAllBytes(@fileName, textureToSave.EncodeToPNG());
        }

        Debug.Log("Texture " + fileName + " successfully saved to disk");
    }


    private Texture2D toTexture2D(RenderTexture rTex)
    {
        Texture2D tex = new Texture2D(rTex.width, rTex.height, TextureFormat.RGBAHalf, false, true);

        RenderTexture.active = rTex;
        tex.ReadPixels(new Rect(0, 0, rTex.width, rTex.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        return tex;
    }

    private void OnDestroy()
    {
        xCurveCoordsCBuffer.Release();
        yCurveCoordsCBuffer.Release();
    }
}
