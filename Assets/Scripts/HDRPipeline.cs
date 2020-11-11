using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HDRPipeline : MonoBehaviour
{
    // Gamut Mapping public member variables
    public Material colorGamutMat;
    public Material fullScreenTextureMat;
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
             StopCoroutine(colorGamut.ApplyTransferFunction(hdriRenderTexture));
             StartCoroutine(colorGamut.ApplyTransferFunction(hdriRenderTexture));
         }

         if (colorGamut.CurveState == ColorGamut1.CurveDataState.Calculated)
         {
            // Debug.Log("Started Color Grading image");
            colorGrading.OnRenderImage(colorGamut.HdriTextureTransformed, renderBuffer, hdr3DLutToDecode);
            // Debug.Log("Finished color grading the image");
            fullScreenTextureMat.SetTexture("_MainTex", renderBuffer);
            Graphics.Blit(renderBuffer, dest, fullScreenTextureMat);
         }
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
