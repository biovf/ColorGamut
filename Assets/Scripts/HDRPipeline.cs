﻿using System;
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

    public void drawGamutCurveWidget()
    {
        var oldRt = RenderTexture.active;
        //curveDrawMaterial.SetFloat("scaleFactor", scaleFactor);
        //curveDrawMaterial.SetFloatArray("xCoords", colorGamut.getXValues().ToArray());
        //curveDrawMaterial.SetFloatArray("yCoords", colorGamut.getYValues().ToArray());
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
        
         if (useCpuMode && (colorGamut.CurveState == ColorGamut1.CurveDataState.NotCalculated ||
             colorGamut.CurveState == ColorGamut1.CurveDataState.Dirty))
         {
             ApplyGamutMap();
         }
         else if(!useCpuMode)
         {
             activeTransferFunction = (colorGamut.ActiveGamutMappingMode == GamutMappingMode.Max_RGB) ? 0 : 1;
             gamutMap.SetTexture("_MainTex", hdriRenderTexture);
             gamutMap.SetFloat("exposure", colorGamut.Exposure);
             gamutMap.SetVector("greyPoint", new Vector4(colorGamut.GreyPoint.x,colorGamut.GreyPoint.y, 0.0f));
             gamutMap.SetFloat("minExposure", colorGamut.MINExposureValue);
             gamutMap.SetFloat("maxExposure", colorGamut.MAXExposureValue);
             gamutMap.SetFloat("minRadiometricValue", colorGamut.MinRadiometricValue);
             gamutMap.SetFloat("maxRadiometricValue", colorGamut.MaxRadiometricValue);
             gamutMap.SetInt("inputArraySize", colorGamut.getXValues().Count - 1);
             gamutMap.SetInt("usePerChannel", activeTransferFunction);
             gamutMap.SetFloatArray("xCoords", colorGamut.getXValues().ToArray());
             gamutMap.SetFloatArray("yCoords", colorGamut.getYValues().ToArray());

           
             gamutMap.SetVectorArray("controlPoints", controlPointsUniform);
             
             Graphics.Blit(hdriRenderTexture, gamutMapRT, gamutMap);
                 
             Graphics.Blit(gamutMapRT, hdriRenderTexture, fullScreenTextureMat);
             colorGamut.SetCurveDataState(ColorGamut1.CurveDataState.Calculated);
         }
         
         if (colorGamut.CurveState == ColorGamut1.CurveDataState.Calculated)
         {
            if (useCpuMode)
            {
                colorGrading.OnRenderImage(colorGamut.HdriTextureTransformed, renderBuffer, hdr3DLutToDecode);
            }
            else
            {
                colorGrading.OnRenderImage(hdriRenderTexture, renderBuffer, hdr3DLutToDecode);
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
