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
    public float maxLatitude;
    public GamutMappingMode ActiveGamutMappingMode;

    public CurveParams(float inExposure, float inSlope, float inOriginX,
        float inOriginY, GamutMappingMode inActiveGamutMappingMode, bool inIsGamutCompressionActive, float inMaxLatitude)
    {
        exposure = inExposure;
        slope = inSlope;
        originX = inOriginX;
        originY = inOriginY;
        ActiveGamutMappingMode = inActiveGamutMappingMode;
        isGamutCompressionActive = inIsGamutCompressionActive;
        maxLatitude = inMaxLatitude;
    }
}

public class HDRPipeline : MonoBehaviour
{
    // Gamut Mapping public member variables
    public Material fullScreenTextureMat;
    [FormerlySerializedAs("gamutMap")]
    public Material gamutMapMat;
    public Texture2D sweepTexture;
    public List<Texture2D> HDRIList;

    // Color Grading public member variables
    public Material colorGradingMat;
    public Material colorGradingBakerMat;
    public Texture3D colorGradeLUT;

    private GamutMap colorGamut;

    private RenderTexture renderBuffer;
    private RenderTexture hdriRenderTexture;
    private RenderTexture gamutMapRT;

    private bool useCpuMode = true;
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

    private Vector4[] controlPointsUniform;
    private ComputeBuffer xCurveCoordsCBuffer;
    private ComputeBuffer yCurveCoordsCBuffer;


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

        controlPointsUniform = new Vector4[7];

        xCurveCoordsCBuffer = new ComputeBuffer(1024, sizeof(float));
        yCurveCoordsCBuffer = new ComputeBuffer(1024, sizeof(float));
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

        if (useCpuMode && (colorGamut.CurveState == GamutMap.CurveDataState.NotCalculated ||
            colorGamut.CurveState == GamutMap.CurveDataState.Dirty))
        {
            // Chromaticity compression
            Texture2D hdriTexture2D = colorGamut.toTexture2D(hdriRenderTexture);
            Color[] hdriTexturePixels = colorGamut.ApplyChromaticityCompression(hdriTexture2D.GetPixels(), false);
            hdriTexture2D.SetPixels(hdriTexturePixels);
            colorGamut.SaveToDisk(hdriTexture2D.GetPixels(), "DebugData/ColorGradingInput.exr", hdriTexture2D.width, hdriTexture2D.height);
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
            ApplyGamutMap(renderBuffer);

        }
        else if (!useCpuMode)
        {
            activeTransferFunction = (colorGamut.ActiveGamutMappingMode == GamutMappingMode.Max_RGB) ? 0 : 1;
            gamutMapMat.SetTexture("_MainTex", hdriRenderTexture);
            gamutMapMat.SetFloat("exposure", colorGamut.Exposure);
            gamutMapMat.SetVector("greyPoint", new Vector4(colorGamut.MidGreySdr.x, colorGamut.MidGreySdr.y, 0.0f));
            gamutMapMat.SetFloat("minExposure", colorGamut.MinRadiometricExposure);
            gamutMapMat.SetFloat("maxExposure", colorGamut.MaxRadiometricExposure);
            gamutMapMat.SetFloat("maxRadiometricValue", colorGamut.MaxRadiometricDynamicRange);
            gamutMapMat.SetFloat("minDisplayExposure", colorGamut.MinDisplayExposure);
            gamutMapMat.SetFloat("maxDisplayExposure", colorGamut.MaxDisplayExposure);
            gamutMapMat.SetFloat("minDisplayValue", colorGamut.MinDisplayValue);
            gamutMapMat.SetFloat("maxDisplayValue", colorGamut.MaxDisplayValue);
            gamutMapMat.SetFloat("maxLatitudeLimit", colorGamut.CurveMaxLatitude);
            gamutMapMat.SetInt("inputArraySize", colorGamut.getXValues().Count - 1);
            gamutMapMat.SetInt("usePerChannel", activeTransferFunction);

            xCurveCoordsCBuffer.SetData(colorGamut.getXValues().ToArray());
            yCurveCoordsCBuffer.SetData(colorGamut.getYValues().ToArray());
            gamutMapMat.SetBuffer(Shader.PropertyToID("xCurveCoordsCBuffer"), xCurveCoordsCBuffer);
            gamutMapMat.SetBuffer(Shader.PropertyToID("yCurveCoordsCBuffer"), yCurveCoordsCBuffer);
            gamutMapMat.SetVectorArray("controlPoints", controlPointsUniform);

            Graphics.Blit(hdriRenderTexture, gamutMapRT, gamutMapMat);

            Graphics.Blit(gamutMapRT, hdriRenderTexture, fullScreenTextureMat);
            colorGamut.SetCurveDataState(GamutMap.CurveDataState.Calculated);
        }

        if (colorGamut.CurveState == GamutMap.CurveDataState.Calculated)
        {
            Graphics.Blit(colorGamut.HdriTextureTransformed, renderBuffer, fullScreenTextureMat);

            fullScreenTextureMat.SetTexture("_MainTex", renderBuffer);
            Graphics.Blit(renderBuffer, dest, fullScreenTextureMat);
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

    public void BakeColorGrade(Texture src, RenderTexture dest, Texture3D LUT)
    {
        colorGradingBakerMat.SetTexture("_MainTex", src);
        colorGradingBakerMat.SetTexture("_LUT", LUT);
        colorGradingBakerMat.SetFloat("_MinExposureValue", colorGamut.MinRadiometricExposure);
        colorGradingBakerMat.SetFloat("_MaxExposureValue", colorGamut.MaxRadiometricExposure);
        colorGradingBakerMat.SetFloat("_MidGreyX", colorGamut.MidGreySdr.x);
        Graphics.Blit(src, dest, colorGradingBakerMat);
    }

    public void ApplyGamutMap()
    {
        ApplyGamutMap(renderBuffer);
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
