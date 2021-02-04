using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LutBaker
{
    private GamutMap gamutMap;
    private ColorGrade colorGrade;

    public LutBaker(GamutMap gamutMap, ColorGrade colorGrade)
    {
        this.gamutMap = gamutMap;
        this.colorGrade = colorGrade;
    }

    public Texture3D BakeLUT(int lutDimension)
    {
        var lut = new Texture3D(lutDimension, lutDimension, lutDimension, TextureFormat.RGBAHalf, false)
        {
            // @TODO enable trilinear?
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            anisoLevel = 0
        };
        Color[] identity3DLut = LutGenerator.generateIdentityCubeLUT(lutDimension);

        // Chromaticity compression
        Color[] lutPixels = gamutMap.ChromaticityCompression(identity3DLut);
        // ColorGrade blit

        // ReadPixels
        // Aesthetic TF baking
        // Store data in LUT

        return lut;
    }
}
