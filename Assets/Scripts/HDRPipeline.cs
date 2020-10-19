using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class HDRPipeline : MonoBehaviour
{
    // Gamut Mapping public variables
    public Material colorGamutMat;
    public Material fullScreenTextureMat;
    public Texture2D sweepTexture;
    public List<Texture2D> HDRIList;

    // Color Grading public variables
    public Material colorGradingMat;
    public Material colorGrading3DTextureMat;
    public Material fullscreenMat;
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
    }
    
    // Update is called once per frame
    void Update()
    {
        if (colorGrading != null)
        {
            colorGrading.Update();
        }
        
        if (colorGamut != null)
        {
            colorGamut.Update();
        }
    }

    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        Graphics.Blit(HDRIList[0], hdriRenderTexture, fullScreenTextureMat);

        if (colorGrading != null)
        {
            colorGrading.OnRenderImage(hdriRenderTexture, renderBuffer);
        }
        if (colorGamut != null)
        {
            colorGamut.OnRenderImage(renderBuffer, dest);
        }
        
        // if (colorGamut != null)
        // {
        //     colorGamut.OnRenderImage(src, renderBuffer);
        // }
        //
        // if (colorGrading != null)
        // {
        //     colorGrading.OnRenderImage(renderBuffer, dest);
        // }
    }
    
    private void initialiseColorGamut()
    {
        colorGamut = new ColorGamut1(colorGamutMat, fullScreenTextureMat, HDRIList);
        colorGamut.Start(this);
        if (Application.isPlaying && this.isActiveAndEnabled)
        {
            StartCoroutine(colorGamut.CpuGGMIterative());
        }

    }

    public ColorGamut1 getColorGamut()
    {
        if(colorGamut == null)
            initialiseColorGamut();
        return colorGamut;
    }
    
    private void initialiseColorGrading()
    {
        colorGrading = new ColorGradingHDR1(colorGamut.getHDRITexture(), colorGradingMat, colorGrading3DTextureMat, 
            fullscreenMat);
        colorGrading.Start(this, hdr3DLutToDecode);
    }
    
    public ColorGradingHDR1 getColorGrading()
    {
        if(colorGrading == null)
            initialiseColorGrading();
        return colorGrading;
    }
}
