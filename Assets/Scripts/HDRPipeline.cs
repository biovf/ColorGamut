using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public struct CurveParams
{
    public bool isGamutCompressionActive;
    public float exposure;
    public float slope;
    public float originX;
    public float originY;
    public GamutMappingMode ActiveGamutMappingMode;

    public CurveParams(float inExposure, float inSlope, float inOriginX,
        float inOriginY, GamutMappingMode inActiveGamutMappingMode, bool inIsGamutCompressionActive)
    {
        exposure = inExposure;
        slope = inSlope;
        originX = inOriginX;
        originY = inOriginY;
        ActiveGamutMappingMode = inActiveGamutMappingMode;
        isGamutCompressionActive = inIsGamutCompressionActive;

    }
}

public class HDRPipeline : MonoBehaviour
{
    // Gamut Mapping public member variables
    public Material fullScreenTextureMat;
    public Material gamutMap;
    public Texture2D sweepTexture;
    public List<Texture2D> HDRIList;

    // Color Grading public member variables
    public Material colorGradingMat;
    public Texture3D colorGradeLUT;
    
    private GamutMap colorGamut;
    private ColorGrade colorGrade;

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
        renderBuffer = new RenderTexture(HDRIList[0].width, HDRIList[0].height, 0, RenderTextureFormat.ARGBHalf,
            RenderTextureReadWrite.Linear);
        hdriRenderTexture = new RenderTexture(HDRIList[0].width, HDRIList[0].height, 0, RenderTextureFormat.ARGBHalf,
            RenderTextureReadWrite.Linear);
        gamutMapRT = new RenderTexture(HDRIList[0].width, HDRIList[0].height, 0, RenderTextureFormat.ARGBHalf,
            RenderTextureReadWrite.Linear);
        initialiseColorGamut();
        initialiseColorGrading();
        
        curveRT = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGB32);
        curveDrawMaterial = new Material(Shader.Find("Custom/DrawCurve"));
        controlPointsUniform = new Vector4[7];
        
        xCurveCoordsCBuffer = new ComputeBuffer(1024, sizeof(float));
        yCurveCoordsCBuffer = new ComputeBuffer(1024, sizeof(float));

        
    }

    private void initialiseColorGamut()
    {
        colorGamut = new GamutMap(fullScreenTextureMat, HDRIList);
        colorGamut.Start(this);
    }
    private void initialiseColorGrading()
    {
        colorGrade = new ColorGrade(colorGamut.getHDRITexture(), colorGradingMat, 
            fullScreenTextureMat);
        colorGrade.Start(this, colorGradeLUT);
    }
    
    void Update()
    {
        colorGrade.Update();
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
             ApplyGamutMap();
         }
         else if(!useCpuMode)
         {
             activeTransferFunction = (colorGamut.ActiveGamutMappingMode == GamutMappingMode.Max_RGB) ? 0 : 1;
             gamutMap.SetTexture("_MainTex", hdriRenderTexture);
             gamutMap.SetFloat("exposure", colorGamut.Exposure);
             gamutMap.SetVector("greyPoint", new Vector4(colorGamut.GreyPoint.x,colorGamut.GreyPoint.y, 0.0f));
             gamutMap.SetFloat("minExposure", colorGamut.MinRadiometricExposure);
             gamutMap.SetFloat("maxExposure", colorGamut.MaxRadiometricExposure);
             gamutMap.SetFloat("maxRadiometricValue", colorGamut.MaxRadiometricDynamicRange);
             gamutMap.SetInt("inputArraySize", colorGamut.getXValues().Count - 1);
             gamutMap.SetInt("usePerChannel", activeTransferFunction);

             xCurveCoordsCBuffer.SetData(colorGamut.getXValues().ToArray());
             yCurveCoordsCBuffer.SetData(colorGamut.getYValues().ToArray());
             gamutMap.SetBuffer(Shader.PropertyToID("xCurveCoordsCBuffer"), xCurveCoordsCBuffer);
             gamutMap.SetBuffer(Shader.PropertyToID("yCurveCoordsCBuffer"), yCurveCoordsCBuffer);
             gamutMap.SetVectorArray("controlPoints", controlPointsUniform);
             
             Graphics.Blit(hdriRenderTexture, gamutMapRT, gamutMap);
                 
             Graphics.Blit(gamutMapRT, hdriRenderTexture, fullScreenTextureMat);
             colorGamut.SetCurveDataState(GamutMap.CurveDataState.Calculated);
         }
         
         if (colorGamut.CurveState == GamutMap.CurveDataState.Calculated)
         {
            if (useCpuMode)
            {
                colorGrade.OnRenderImage(colorGamut.HdriTextureTransformed, renderBuffer, colorGradeLUT);
            }
            else
            {
                colorGrade.OnRenderImage(hdriRenderTexture, renderBuffer, colorGradeLUT);
            }

            fullScreenTextureMat.SetTexture("_MainTex", renderBuffer);
            Graphics.Blit(renderBuffer, dest, fullScreenTextureMat);
         }
    }

    public void ApplyGamutMap()
    {
        // Attempt to stop CoRoutine if it hasn't stopped already
        StopCoroutine(colorGamut.ApplyTransferFunction(hdriRenderTexture));
        StartCoroutine(colorGamut.ApplyTransferFunction(hdriRenderTexture));
    }

    public GamutMap getColorGamut()
    {
        if(colorGamut == null)
            initialiseColorGamut();
        
        return colorGamut;
    }
    
    public ColorGrade getColorGrading()
    {
        if(colorGrade == null)
            initialiseColorGrading();
        
        return colorGrade;
    }
}
