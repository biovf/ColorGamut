using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HDRPipeline : MonoBehaviour
{
    // Gamut Mapping public member variables
    public Material colorGamutMat;
    public Material fullScreenTextureMat;
    public Material gamutMap;
    public Texture2D sweepTexture;
    public List<Texture2D> HDRIList;

    // Color Grading public member variables
    public Material colorGrading3DTextureMat;
    public Material log2Shaper;
    public Texture3D hdr3DLutToDecode;
    
    private ColorGamut1 colorGamut;
    private ColorGradingHDR1 colorGrading;

    private RenderTexture renderBuffer;
    private RenderTexture hdriRenderTexture;
    
    private bool CPUMode = true;

    void Start()
    {
        renderBuffer = new RenderTexture(HDRIList[0].width, HDRIList[0].height, 0, RenderTextureFormat.ARGBHalf,
            RenderTextureReadWrite.Linear);
        hdriRenderTexture = new RenderTexture(HDRIList[0].width, HDRIList[0].height, 0, RenderTextureFormat.ARGBHalf,
            RenderTextureReadWrite.Linear);
        initialiseColorGamut();
        initialiseColorGrading();
    }

    private void initialiseColorGamut()
    {
        colorGamut = new ColorGamut1(colorGamutMat, fullScreenTextureMat, HDRIList);
        colorGamut.Start(this);
    }
    private void initialiseColorGrading()
    {
        colorGrading = new ColorGradingHDR1(colorGamut.getHDRITexture(), colorGrading3DTextureMat, 
            fullScreenTextureMat, log2Shaper);
        colorGrading.Start(this, hdr3DLutToDecode);
    }
    
    void Update()
    {
        colorGrading.Update();
        colorGamut.Update();
    }

    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
         Graphics.Blit(HDRIList[0], hdriRenderTexture, fullScreenTextureMat);
        
         if (colorGamut.CurveState == ColorGamut1.CurveDataState.NotCalculated ||
             colorGamut.CurveState == ColorGamut1.CurveDataState.Dirty)
         {
             // Attempt to stop CoRoutine if it hasn't stopped already
             // StopCoroutine(colorGamut.ApplyTransferFunction(hdriRenderTexture));
             // StartCoroutine(colorGamut.ApplyTransferFunction(hdriRenderTexture));

             if (CPUMode)
             {
                 ApplyGamutMap();
             }
             else
             {
                 gamutMap.SetTexture("_MainTex", hdriRenderTexture);
                 gamutMap.SetFloat("Exposure", colorGamut.Exposure);
                 gamutMap.SetVector("greyPoint", new Vector4(colorGamut.GreyPoint.x,colorGamut.GreyPoint.y, 0.0f));
                 gamutMap.SetFloat("minExposure", colorGamut.MINExposureValue);
                 gamutMap.SetFloat("maxExposure", colorGamut.MAXExposureValue);
                 gamutMap.SetFloat("maxRadiometricValue", colorGamut.MaxRadiometricValue);
                 gamutMap.SetInt("inputArraySize", colorGamut.getXValues().Count);
                 gamutMap.SetFloatArray("xCoords", colorGamut.getXValues().ToArray());
                 gamutMap.SetFloatArray("yCoords", colorGamut.getYValues().ToArray());
                 gamutMap.SetFloatArray("tValues", colorGamut.getTValues().ToArray());
                 Graphics.Blit(hdriRenderTexture, renderBuffer, gamutMap);
                 
                 Graphics.Blit(renderBuffer, hdriRenderTexture, fullScreenTextureMat);
                 colorGamut.SetCurveDataState(ColorGamut1.CurveDataState.Calculated);
             }
         }

         if (colorGamut.CurveState == ColorGamut1.CurveDataState.Calculated)
         {
            // Debug.Log("Started Color Grading image");
            if (CPUMode)
            {
                colorGrading.OnRenderImage(colorGamut.HdriTextureTransformed, renderBuffer, hdr3DLutToDecode);
            }
            else
            {
                colorGrading.OnRenderImage(hdriRenderTexture, renderBuffer, hdr3DLutToDecode);
            }
            // Debug.Log("Finished color grading the image");
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

    public ColorGamut1 getColorGamut()
    {
        if(colorGamut == null)
            initialiseColorGamut();
        
        return colorGamut;
    }
    
    public ColorGradingHDR1 getColorGrading()
    {
        if(colorGrading == null)
            initialiseColorGrading();
        
        return colorGrading;
    }
}
