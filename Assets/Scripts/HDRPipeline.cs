//#define DEBUG_CHECKS

using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using Debug = UnityEngine.Debug;

public struct CurveParams
{
    public bool isGamutCompressionActive;
    public float exposure;
    public float slope;
    public float originX;
    public float originY;
    public float curveCoordMaxLatitude;
    public float chromaticitydMaxLatitude;

    public GamutMappingMode ActiveGamutMappingMode;

    public CurveParams(float inExposure, float inSlope, float inOriginX,
        float inOriginY, GamutMappingMode inActiveGamutMappingMode, bool inIsGamutCompressionActive,
        float inCurveCoordMaxLatitude, float inChromaticitydMaxLatitude)
    {
        exposure = inExposure;
        slope = inSlope;
        originX = inOriginX;
        originY = inOriginY;
        ActiveGamutMappingMode = inActiveGamutMappingMode;
        isGamutCompressionActive = inIsGamutCompressionActive;
        curveCoordMaxLatitude = inCurveCoordMaxLatitude;
        chromaticitydMaxLatitude = inChromaticitydMaxLatitude;
    }
}

public class HDRPipeline : MonoBehaviour
{
    // Gamut Mapping public member variables
    public Material fullScreenTextureMat;
    [FormerlySerializedAs("gamutMap")]
    public Material gamutMapMat;

    public Material chromaticityCompressionMat;
    public Texture2D sweepTexture;
    public List<Texture2D> HDRIList;

    // Color Grading public member variables
    public Material colorGradingMat;
    public Material colorGradingBakerMat;
    public Material lutBakerMat;
    public Texture3D colorGradeLUT;
    public Texture3D bakedLUT;

    private GamutMap colorGamut;
    private RenderTexture renderBuffer;
    private RenderTexture hdriRenderTexture;
    private RenderTexture gamutMapRT;

    private int activeTransferFunction = 0;

    public bool CPUMode
    {
        get => useCpuMode;
        set => useCpuMode = value;
    }

    // Curve widget member variables
    private Material curveDrawMaterial;
    private RenderTexture curveRT;
    public RenderTexture CurveRT => curveRT;
    private float scaleFactor = 1.0f;

    public float ScaleFactor
    {
        get => scaleFactor;
        set => scaleFactor = value;
    }

    public Vector4[] ControlPointsUniform => controlPointsUniform;
    private Vector4[] controlPointsUniform;

    public ComputeBuffer XCurveCoordsCBuffer => xCurveCoordsCBuffer;
    private ComputeBuffer xCurveCoordsCBuffer;

    public ComputeBuffer YCurveCoordsCBuffer => yCurveCoordsCBuffer;
    private ComputeBuffer yCurveCoordsCBuffer;

    private Texture2D hdriTexture2D;

    private bool useCpuMode = false;
    private bool useBakedLUT = true;
    private bool debug = false;

    void Start()
    {
        isObjectNull(colorGradeLUT, "colorGradeLUT");
        isObjectNull(colorGradingBakerMat, "colorGradingBakerMat");

        renderBuffer = new RenderTexture(HDRIList[0].width, HDRIList[0].height, 0, RenderTextureFormat.ARGBHalf,
            RenderTextureReadWrite.Linear);
        hdriRenderTexture = new RenderTexture(HDRIList[0].width, HDRIList[0].height, 0, RenderTextureFormat.ARGBHalf,
            RenderTextureReadWrite.Linear);
        gamutMapRT = new RenderTexture(HDRIList[0].width, HDRIList[0].height, 0, RenderTextureFormat.ARGBHalf,
            RenderTextureReadWrite.Linear);
        initialiseColorGamut();

        curveRT = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGB32);
        curveDrawMaterial = new Material(Shader.Find("Custom/DrawCurve"));

        var lutBakerShader = Shader.Find("Custom/LutBaker");
        lutBakerMat = new Material(lutBakerShader);
        controlPointsUniform = new Vector4[7];

        xCurveCoordsCBuffer = new ComputeBuffer(1024, sizeof(float));
        yCurveCoordsCBuffer = new ComputeBuffer(1024, sizeof(float));

        // LutBaker test

        float curTime = Time.realtimeSinceStartup;
        // Texture3D runtimeBakedLUT = lutbaker.BakeLUT(33);
        // Debug.Log("Took " + (Time.realtimeSinceStartup - curTime).ToString() + "s");
        // bakedLUT = runtimeBakedLUT;

    }

    [Conditional("DEBUG_CHECKS")]
    private void isObjectNull(Object obj, string objName)
    {
        if (obj == null)
        {
            Debug.LogError(objName + " is null");
        }

    }

    private void initialiseColorGamut()
    {
        colorGamut = new GamutMap(fullScreenTextureMat, HDRIList);
        colorGamut.Start(this);
    }

    void Update()
    {
        colorGamut.Update();
    }

    public void drawGamutCurveWidget()
    {
        var oldRt = RenderTexture.active;
        curveDrawMaterial.SetVectorArray("controlPoints", controlPointsUniform);
        Graphics.Blit(null, curveRT, curveDrawMaterial);
        RenderTexture.active = oldRt;
    }

    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        Vector2[] controlPoints = colorGamut.getControlPoints();
        for (int i = 0; i < 7; i++)
        {
            controlPointsUniform[i] = new Vector4(controlPoints[i].x, controlPoints[i].y);
        }
        drawGamutCurveWidget();

        Graphics.Blit(HDRIList[0], hdriRenderTexture, fullScreenTextureMat);

        if (useBakedLUT)
        {
            //Graphics.Blit(HDRIList[0], hdriRenderTexture, fullScreenTextureMat);
            //hdriTexture2D = colorGamut.toTexture2D(hdriRenderTexture);
            //Color[] hdriTexturePixels = hdriTexture2D.GetPixels();
            //hdriTexture2D.SetPixels(hdriTexturePixels);
            //hdriTexture2D.Apply();

            //chromaticityCompressionMat.SetTexture("_MainTex", hdriRenderTexture);
            //chromaticityCompressionMat.SetFloat("exposure", colorGamut.Exposure);
            //chromaticityCompressionMat.SetVector("greyPoint", new Vector4(colorGamut.MidGreySdr.x, colorGamut.MidGreySdr.y, 0.0f));
            //chromaticityCompressionMat.SetFloat("minRadiometricExposure", colorGamut.MinRadiometricExposure);
            //chromaticityCompressionMat.SetFloat("maxRadiometricExposure", colorGamut.MaxRadiometricExposure);
            //chromaticityCompressionMat.SetFloat("maxRadiometricValue", colorGamut.MaxRadiometricDynamicRange);
            //chromaticityCompressionMat.SetFloat("chromaticityMaxLatitude", colorGamut.ChromaticityMaxLatitude);
            //chromaticityCompressionMat.SetFloat("gamutCompressionRatioPower", colorGamut.GamutCompressionRatioPower);
            lutBakerMat.SetTexture("_MainTex", hdriRenderTexture);
            lutBakerMat.SetTexture("_LUT", bakedLUT);
            lutBakerMat.SetFloat("_MinExposureValue", colorGamut.MinRadiometricExposure);
            lutBakerMat.SetFloat("_MaxExposureValue", colorGamut.MaxRadiometricExposure);
            lutBakerMat.SetFloat("_MidGreyX", colorGamut.MidGreySdr.x);
            Graphics.Blit(hdriRenderTexture, dest, lutBakerMat);
            //Graphics.Blit(renderBuffer, hdriRenderTexture, fullScreenTextureMat);
            //RenderColorGrade(hdriRenderTexture, renderBuffer, bakedLUT);
        }
        else
        {
            if (colorGamut.CurveState == GamutMap.CurveDataState.NotCalculated ||
            colorGamut.CurveState == GamutMap.CurveDataState.Dirty)
            {
                // Chromaticity compression
                if (colorGamut.getIsGamutCompressionActive())
                {
                    if (useCpuMode)
                    {
                        hdriTexture2D = colorGamut.toTexture2D(hdriRenderTexture);
                        Color[] hdriTexturePixels = hdriTexture2D.GetPixels();
                        hdriTexturePixels = colorGamut.ApplyChromaticityCompressionCPU(hdriTexture2D.GetPixels());
                        hdriTexture2D.SetPixels(hdriTexturePixels);
                        hdriTexture2D.Apply();
                    }
                    else
                    {
                        chromaticityCompressionMat.SetTexture("_MainTex", hdriRenderTexture);
                        chromaticityCompressionMat.SetFloat("exposure", colorGamut.Exposure);
                        chromaticityCompressionMat.SetVector("greyPoint", new Vector4(colorGamut.MidGreySdr.x, colorGamut.MidGreySdr.y, 0.0f));
                        chromaticityCompressionMat.SetFloat("minRadiometricExposure", colorGamut.MinRadiometricExposure);
                        chromaticityCompressionMat.SetFloat("maxRadiometricExposure", colorGamut.MaxRadiometricExposure);
                        chromaticityCompressionMat.SetFloat("maxRadiometricValue", colorGamut.MaxRadiometricDynamicRange);
                        chromaticityCompressionMat.SetFloat("chromaticityMaxLatitude", colorGamut.ChromaticityMaxLatitude);
                        chromaticityCompressionMat.SetFloat("gamutCompressionRatioPower", colorGamut.GamutCompressionRatioPower);

                        Graphics.Blit(hdriRenderTexture, renderBuffer, chromaticityCompressionMat);
                        Graphics.Blit(renderBuffer, hdriRenderTexture, fullScreenTextureMat);

                    }
                }

                // colorGamut.SaveToDisk(hdriTexture2D.GetPixels(), "DebugData/Image_ChromaticityCompression.exr", hdriTexture2D.width, hdriTexture2D.height);
                // Color grade
                if (useCpuMode)
                {
                    RenderColorGrade(hdriTexture2D, renderBuffer, colorGradeLUT);
                }
                else
                {
                    RenderColorGrade(hdriRenderTexture, renderBuffer, colorGradeLUT);
                }

                // Aesthetic curve
                if (useCpuMode)
                {
                    ApplyGamutMap(renderBuffer);
                }
                else
                {
                    activeTransferFunction = (colorGamut.ActiveGamutMappingMode == GamutMappingMode.Max_RGB) ? 0 : 1;
                    gamutMapMat.SetTexture("_MainTex", renderBuffer);
                    gamutMapMat.SetVector("greyPoint", new Vector4(colorGamut.MidGreySdr.x, colorGamut.MidGreySdr.y, 0.0f));
                    gamutMapMat.SetFloat("minRadiometricExposure", colorGamut.MinRadiometricExposure);
                    gamutMapMat.SetFloat("maxRadiometricExposure", colorGamut.MaxRadiometricExposure);
                    gamutMapMat.SetFloat("maxRadiometricValue", colorGamut.MaxRadiometricDynamicRange);
                    gamutMapMat.SetFloat("minDisplayExposure", colorGamut.MinDisplayExposure);
                    gamutMapMat.SetFloat("maxDisplayExposure", colorGamut.MaxDisplayExposure);
                    gamutMapMat.SetFloat("minDisplayValue", colorGamut.MinDisplayValue);
                    gamutMapMat.SetFloat("maxDisplayValue", colorGamut.MaxDisplayValue);
                    gamutMapMat.SetInt("inputArraySize", colorGamut.getXValues().Count - 1);
                    gamutMapMat.SetInt("usePerChannel", activeTransferFunction);

                    xCurveCoordsCBuffer.SetData(colorGamut.getXValues().ToArray());
                    yCurveCoordsCBuffer.SetData(colorGamut.getYValues().ToArray());
                    gamutMapMat.SetBuffer(Shader.PropertyToID("xCurveCoordsCBuffer"), xCurveCoordsCBuffer);
                    gamutMapMat.SetBuffer(Shader.PropertyToID("yCurveCoordsCBuffer"), yCurveCoordsCBuffer);
                    gamutMapMat.SetVectorArray("controlPoints", controlPointsUniform);

                    Graphics.Blit(renderBuffer, gamutMapRT, gamutMapMat);

                    Graphics.Blit(gamutMapRT, renderBuffer, fullScreenTextureMat);
                    colorGamut.SetCurveDataState(GamutMap.CurveDataState.Calculated);
                }
            }

            if (colorGamut.CurveState == GamutMap.CurveDataState.Calculated)
            {

                if (useCpuMode)
                {
                    Graphics.Blit(colorGamut.HdriTextureTransformed, renderBuffer, fullScreenTextureMat);
                }
                else
                {
                    colorGamut.CurveState = GamutMap.CurveDataState.Dirty;
                }


                if (debug)
                {
                    colorGamut.SaveToDisk(colorGamut.toTexture2D(renderBuffer).GetPixels(), "Spiaggia_Chromaticity_Plus_LUT_Baked.exr",
                        renderBuffer.width, renderBuffer.height);
                    debug = false;
                }

                fullScreenTextureMat.SetTexture("_MainTex", renderBuffer);
                Graphics.Blit(renderBuffer, dest, fullScreenTextureMat);
            }
        }
    }

    public void RenderColorGrade(Texture src, RenderTexture dest, Texture3D LUT)
    {
        colorGradingMat.SetTexture("_MainTex", src);
        colorGradingMat.SetTexture("_LUT", LUT);
        colorGradingMat.SetFloat("_MinExposureValue", colorGamut.MinRadiometricExposure);
        colorGradingMat.SetFloat("_MaxExposureValue", colorGamut.MaxRadiometricExposure);
        colorGradingMat.SetFloat("_MidGreyX", colorGamut.MidGreySdr.x);
        Graphics.Blit(src, dest, colorGradingMat);
    }

    public void ApplyGamutMap(RenderTexture renderTexture)
    {
        // Attempt to stop CoRoutine if it hasn't stopped already
        StopCoroutine(colorGamut.ApplyTransferFunction(renderTexture));
        StartCoroutine(colorGamut.ApplyTransferFunction(renderTexture));
    }

    public GamutMap getGamutMap()
    {
        if (colorGamut == null)
            initialiseColorGamut();

        return colorGamut;
    }
}
