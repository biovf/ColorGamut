using System.Collections;
using System.Collections.Generic;
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

        Color[] testColor = { new Color(0.5f, 0.3f, 0.76f) };
        lichDebug(testColor, 0.8f);
        //lichCPU(0.0f);
        lichCPU(0.1f);
        //lichCPU(0.5f);
        //lichCPU(0.8f);

    }
    private void initialiseColorGamut()
    {
        colorGamut = new GamutMap(fullScreenTextureMat, HDRIList);
        colorGamut.Start(this.gameObject.GetComponent<Camera>());
    }

    void OnRenderImage(RenderTexture src, RenderTexture dest)
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


    public void lichCPU(float luminanceRelativeTarget) 
    {
        Color[] hdriPixelArray = HDRIList[0].GetPixels();
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

            float luminanceMaxChroma = Vector3.Dot(maxChroma, luminanceWeights);

            Vector3 energyLeft = Vector3.one - maxChroma;
            float luminanceEnergyLeft = Vector3.Dot(energyLeft, luminanceWeights);

            float luminanceDifferenceMaxDisplayAndMaxChroma = Mathf.Max(luminanceRelativeTarget - luminanceMaxChroma, 0.0f);
            float scaledLuminanceDifference = luminanceDifferenceMaxDisplayAndMaxChroma / Mathf.Max(luminanceEnergyLeft, 0.0001f);

            float chromaScale = (luminanceRelativeTarget - luminanceDifferenceMaxDisplayAndMaxChroma) / Mathf.Max(luminanceMaxChroma, 0.0001f);

            Vector3 outputColour = chromaScale * maxChroma + scaledLuminanceDifference * energyLeft;
            hdriPixelArray[i] = new Color(outputColour.x, outputColour.y, outputColour.z);
        }

        hdriTextureTransformed.SetPixels(hdriPixelArray);
        hdriTextureTransformed.Apply();
    }


    private void OnDestroy()
    {
        xCurveCoordsCBuffer.Release();
        yCurveCoordsCBuffer.Release();
    }
}
