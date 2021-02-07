using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LutBaker
{
    private HDRPipeline hdrPipeline;
    private GamutMap gamutMap;

    public LutBaker(HDRPipeline hdrPipeline)
    {
        this.hdrPipeline = hdrPipeline;
        this.gamutMap = hdrPipeline.getGamutMap();
    }

    public Texture3D BakeLUT(int lutDimension)
    {
        var lut3DTexture = new Texture3D(lutDimension, lutDimension, lutDimension, TextureFormat.RGBAHalf, false)
        {
            // @TODO enable trilinear?
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            anisoLevel = 0
        };

        // Start identity cube LUT
        Color[] identity3DLut = LutGenerator.generateIdentityCubeLUT(lutDimension);
        // Apply Chromaticity compression
        //Color[] lutPixels = gamutMap.ApplyChromaticityCompression(identity3DLut);
        // ColorGrade blit
        // hdrPipeline.RenderColorGrade();

        // dest.ReadPixels(new Rect(0, 0, dest.width, dest.height), 0, 0, false);
        // Aesthetic TF baking
        gamutMap.CalculateTransferTransform(ref lutPixels);
        // Store data in LUT

        return lut3DTexture;
    }
}
