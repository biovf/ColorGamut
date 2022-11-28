using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
/// <summary>
/// Struct used to package several gamut curve parameters together in one single bundle to make it easier to share
/// information between ColourPipelineEditor.cs and GamutMap.cs
/// </summary>
public struct CurveParams
{
    public float exposure;
    public float slope;
    public float originX;
    public float originY;
    public float curveCoordMaxLatitude;
    public float chromaticitydMaxLatitude;


    public CurveParams(float inExposure, float inSlope, float inOriginX,
        float inOriginY, float inCurveCoordMaxLatitude, float inChromaticitydMaxLatitude)
    {
        exposure = inExposure;
        slope = inSlope;
        originX = inOriginX;
        originY = inOriginY;
        curveCoordMaxLatitude = inCurveCoordMaxLatitude;
        chromaticitydMaxLatitude = inChromaticitydMaxLatitude;
    }
}
#endif
[ExecuteInEditMode]
public class ColourPipeline : MonoBehaviour
{
    public GamutMap ColourGamut
    {
        get
        {
            if (colorGamut == null)
            {
                InitialiseGamutMap();
            }
            return colorGamut;
        }
        set => colorGamut = value;
    }

    public float ScaleFactor
    {
        get => scaleFactor;
        set => scaleFactor = value;
    }

    public bool IsCurveEditingEnabled
    {
        set => isCurveEditingEnabled = value;
    }
    public bool isCurveEditingEnabled = false;
    public RenderTexture CurveRT => curveRT;
    public Vector4[] ControlPointsUniform => controlPointsUniform;
    public ComputeBuffer XCurveCoordsCBuffer => xCurveCoordsCBuffer;
    public ComputeBuffer YCurveCoordsCBuffer => yCurveCoordsCBuffer;

    private GamutMap colorGamut;
    private Material curveDrawMaterial;
    private RenderTexture curveRT;
    private float scaleFactor = 1.0f;

    private Vector4[] controlPointsUniform;
    private ComputeBuffer xCurveCoordsCBuffer;
    private ComputeBuffer yCurveCoordsCBuffer;
    private Vector4 midGreyUniformVec4;

    #region Cached uniforms
    private int exposureUniformID;
    private int greyPointUniformID;
    private int minRadiometricExposureUniformID;
    private int maxRadiometricExposureUniformID;
    private int maxRadiometricValueUniformID;
    private int minDisplayExposureUniformID;
    private int maxDisplayExposureUniformID;
    private int minDisplayValueUniformID;
    private int maxDisplayValueUniformID;
    private int chromaticityMaxLatitudeUniformID;
    private int inputArraySizeUniformID;
    private int xCurveCoordsCBufferUniformID;
    private int yCurveCoordsCBufferUniformID;
    private int colourGradeLUTUniformID;
    private int bakedLutUniformID;
    #endregion

    private void OnEnable()
    {
        InitialiseGamutMap();

        midGreyUniformVec4 = new Vector4(colorGamut.MidGreySdr.x, colorGamut.MidGreySdr.y, 0.0f, 0.0f);
        controlPointsUniform = new Vector4[7];
        xCurveCoordsCBuffer = new ComputeBuffer(1024, sizeof(float));
        yCurveCoordsCBuffer = new ComputeBuffer(1024, sizeof(float));

        CacheUniformIDs();
    }

    private void OnDisable()
    {
        if (xCurveCoordsCBuffer != null)
        {
            xCurveCoordsCBuffer.Dispose();
        }

        if (yCurveCoordsCBuffer != null)
        {
            yCurveCoordsCBuffer.Dispose();
        }
    }

    private void InitialiseGamutMap()
    {
        colorGamut = new GamutMap();
        colorGamut.Init();
    }

    private void CacheUniformIDs()
    {
        exposureUniformID = Shader.PropertyToID("exposure");
        greyPointUniformID = Shader.PropertyToID("greyPoint");
        inputArraySizeUniformID = Shader.PropertyToID("inputArraySize");
        minDisplayValueUniformID = Shader.PropertyToID("minDisplayValue");
        maxDisplayValueUniformID = Shader.PropertyToID("maxDisplayValue");
        minDisplayExposureUniformID = Shader.PropertyToID("minDisplayExposure");
        maxDisplayExposureUniformID = Shader.PropertyToID("maxDisplayExposure");
        maxRadiometricValueUniformID = Shader.PropertyToID("maxRadiometricValue");
        xCurveCoordsCBufferUniformID = Shader.PropertyToID("xCurveCoordsCBuffer");
        yCurveCoordsCBufferUniformID = Shader.PropertyToID("yCurveCoordsCBuffer");
        minRadiometricExposureUniformID = Shader.PropertyToID("minRadiometricExposure");
        maxRadiometricExposureUniformID = Shader.PropertyToID("maxRadiometricExposure");
        chromaticityMaxLatitudeUniformID = Shader.PropertyToID("chromaticityMaxLatitude");
        colourGradeLUTUniformID = Shader.PropertyToID("_ColorGradeLUT");
        bakedLutUniformID = Shader.PropertyToID("_BakedLUT");
    }


    public void SetAnalyticalColourPipelineUniforms(CommandBuffer cmdBuffer, RenderTargetIdentifier rTI,
        Texture3D colorGradeLUT)
    {
        cmdBuffer.SetGlobalFloat(exposureUniformID, colorGamut.Exposure);
        cmdBuffer.SetGlobalFloat(minDisplayValueUniformID, colorGamut.MinDisplayValue);
        cmdBuffer.SetGlobalFloat(maxDisplayValueUniformID, colorGamut.MaxDisplayValue);
        cmdBuffer.SetGlobalFloat(minDisplayExposureUniformID, colorGamut.MinDisplayExposure);
        cmdBuffer.SetGlobalFloat(maxDisplayExposureUniformID, colorGamut.MaxDisplayExposure);
        cmdBuffer.SetGlobalFloat(maxRadiometricValueUniformID, colorGamut.MaxRadiometricValue);
        cmdBuffer.SetGlobalFloat(minRadiometricExposureUniformID, colorGamut.MinRadiometricExposure);
        cmdBuffer.SetGlobalFloat(maxRadiometricExposureUniformID, colorGamut.MaxRadiometricExposure);
        cmdBuffer.SetGlobalFloat(chromaticityMaxLatitudeUniformID, colorGamut.ChromaticityMaxLatitude);

        midGreyUniformVec4.Set(colorGamut.MidGreySdr.x, colorGamut.MidGreySdr.y, 0.0f, 0.0f);
        cmdBuffer.SetGlobalVector(greyPointUniformID, midGreyUniformVec4);
        cmdBuffer.SetGlobalInt(inputArraySizeUniformID, colorGamut.XCameraIntrinsicValues.Count - 1);

        xCurveCoordsCBuffer.SetData(colorGamut.XCameraIntrinsicValues);
        yCurveCoordsCBuffer.SetData(colorGamut.YDisplayIntrinsicValues);
        cmdBuffer.SetGlobalBuffer(xCurveCoordsCBufferUniformID, xCurveCoordsCBuffer);
        cmdBuffer.SetGlobalBuffer(yCurveCoordsCBufferUniformID, yCurveCoordsCBuffer);

        cmdBuffer.SetGlobalTexture(colourGradeLUTUniformID, colorGradeLUT);
    }

    public void SetBakedColourPipelineUniforms(CommandBuffer cmdBuffer, RenderTargetIdentifier rTI,
        Texture3D bakedColourPipelineLUT)
    {
        cmdBuffer.SetGlobalTexture(bakedLutUniformID, bakedColourPipelineLUT);
        cmdBuffer.SetGlobalFloat(maxRadiometricValueUniformID, colorGamut.MaxRadiometricValue);
        cmdBuffer.SetGlobalFloat(minRadiometricExposureUniformID, colorGamut.MinRadiometricExposure);
        cmdBuffer.SetGlobalFloat(maxRadiometricExposureUniformID, colorGamut.MaxRadiometricExposure);

        midGreyUniformVec4.Set(colorGamut.MidGreySdr.x, colorGamut.MidGreySdr.y, 0.0f, 0.0f);
        cmdBuffer.SetGlobalVector(greyPointUniformID, midGreyUniformVec4);
    }

    public void RecalculateParametricCurve()
    {
        if (!isCurveEditingEnabled) return;

        colorGamut.RecalculateParametricCurve();
    }

}
