using System;
using System.IO;
using UnityEditor;
using UnityEditor.VersionControl;
using UnityEngine;

public class LutBaker
{
    private ColourPipeline hdrPipeline;
    private GamutMap gamutMap;
    private Material lutBakerMat;

    public LutBaker(ColourPipeline inHdrPipeline)
    {
        hdrPipeline = inHdrPipeline;
        gamutMap = hdrPipeline.ColourGamut;
        Shader lutBakerShader = Shader.Find("Post/LutBaker");
        lutBakerMat = new Material(lutBakerShader);
    }

    public static Color[] GenerateIdentityCubeLUT(int cubeSideLenght)
    {
        Color[] identityLUT = new Color[cubeSideLenght * cubeSideLenght * cubeSideLenght];

        for (int bChannel = 0; bChannel < cubeSideLenght; bChannel++)
        {
            for (int gChannel = 0; gChannel < cubeSideLenght; gChannel++)
            {
                for (int rChannel = 0; rChannel < cubeSideLenght; rChannel++)
                {
                    int index = rChannel + (cubeSideLenght * gChannel) +
                                (cubeSideLenght * cubeSideLenght * bChannel);
                    identityLUT[index] =
                        new Color((float)rChannel / (float)(cubeSideLenght - 1),
                            (float)gChannel / (float)(cubeSideLenght - 1),
                            (float)bChannel / (float)(cubeSideLenght - 1));
                }
            }
        }

        return identityLUT;
    }


    public Texture3D BakeLut(Texture3D colourGradeLut, int lutDimension)
    {
        // Initialise buffer to retrieve 3D texture into
        Color[][] slicesOf3DColorBuffers = new Color[lutDimension][];
        for (int i = 0; i < slicesOf3DColorBuffers.Length; i++)
        {
            slicesOf3DColorBuffers[i] = new Color[lutDimension * lutDimension];
        }

        // Create an identity cube LUT
        Color[] identity3DLutPixels = GenerateIdentityCubeLUT(lutDimension);
        var input3DLutTex = new Texture3D(lutDimension, lutDimension, lutDimension, TextureFormat.RGBAHalf, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            anisoLevel = 0
        };

        input3DLutTex.SetPixels(identity3DLutPixels);
        input3DLutTex.Apply();
        Color[] buffer3DPixels = input3DLutTex.GetPixels();

        // Create a 2D texture to represent a 2D slice from the 3D texture so that we can render to it
        RenderTexture renderTexture2DSlice = new RenderTexture(lutDimension, lutDimension, 1,
            RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);

        // This loop bakes into a 2D slice of a 3D texture the following operations:
        // 1 - Chromaticity Compression
        // 2 - Color Grading
        // 3 - Aesthetic Transfer Function
        // The final result will be stored into an array of 2D slices that will be later used to re-create a 3D texture
        for (int layerIndex = 0; layerIndex < lutDimension; layerIndex++)
        {
            // Create a texture 2D from every layer of a 3D texture
            Texture2D slice2DFrom3DLut =
                new Texture2D(lutDimension, lutDimension, TextureFormat.RGBAHalf, false, true);
            // Create a colour array so that we're able to change the 2D texture slice data
            Color[] temp2DSlice = new Color[lutDimension * lutDimension];
            // Iterate over the y coordinates and x columns and convert the data from Log2 to linear
            for (int yCoord = 0; yCoord < lutDimension; yCoord++)
            {
                for (int xCoord = 0; xCoord < lutDimension; xCoord++)
                {
                    Color buffer3DColor =
                        buffer3DPixels[xCoord + yCoord * lutDimension + layerIndex * lutDimension * lutDimension];
                    buffer3DColor.r = Shaper.CalculateLog2ToLinear(buffer3DColor.r, gamutMap.MidGreySdr.x,
                        gamutMap.MinRadiometricExposure,
                        gamutMap.MaxRadiometricExposure);
                    buffer3DColor.g = Shaper.CalculateLog2ToLinear(buffer3DColor.g, gamutMap.MidGreySdr.x,
                        gamutMap.MinRadiometricExposure,
                        gamutMap.MaxRadiometricExposure);
                    buffer3DColor.b = Shaper.CalculateLog2ToLinear(buffer3DColor.b, gamutMap.MidGreySdr.x,
                        gamutMap.MinRadiometricExposure,
                        gamutMap.MaxRadiometricExposure);
                    temp2DSlice[xCoord + yCoord * lutDimension] = buffer3DColor;
                }
            }

            // TODO: add GPU version of the ChromaticityCompression
            // Bake the chromaticity compression into the 2D array of colours
            // Input Data Format: Radiometric linear
            // Output Data Format: Log2 camera intrinsic
            temp2DSlice = gamutMap.ApplyChromaticityCompressionCPU(temp2DSlice);
            // Write this array of colours back into the 2D texture
            slice2DFrom3DLut.SetPixels(temp2DSlice);
            slice2DFrom3DLut.Apply();

            // Bake the colour grading step
            lutBakerMat.SetTexture("_MainTex", slice2DFrom3DLut);
            lutBakerMat.SetTexture("_LUT", colourGradeLut);
            Graphics.Blit(slice2DFrom3DLut, renderTexture2DSlice, lutBakerMat);

            // Bake the Aesthetic Transfer function to the 2D array of colours
            slice2DFrom3DLut = gamutMap.ApplyTransferFunctionTo2DSlice(renderTexture2DSlice);
            Color[] colorSlice = slice2DFrom3DLut.GetPixels();
            for (int i = 0; i < colorSlice.Length; i++)
            {
                slicesOf3DColorBuffers[layerIndex][i] = colorSlice[i];
            }
        }

        // Concatenate every 2D slice into a single array
        Color[] final3DTexturePixels = new Color[lutDimension * lutDimension * lutDimension];
        int slicesOf3DColorBuffersLen = slicesOf3DColorBuffers.Length;
        int index = 0;
        for (int l = 0; l < slicesOf3DColorBuffersLen; l++)
        {
            Array.Copy(slicesOf3DColorBuffers[l], 0, final3DTexturePixels, index, slicesOf3DColorBuffers[l].Length);
            index += slicesOf3DColorBuffers[l].Length;
        }

        // Write the single array color information into the 3D texture
        input3DLutTex.SetPixels(final3DTexturePixels);
        input3DLutTex.Apply();

        // // // Debug: Write every slice to disk to save it
        // for (int i = 0; i < slicesOf3DColorBuffers.Length; i++)
        // {
        //     SaveToDisk(slicesOf3DColorBuffers[i], "CSR3Layer" + i + ".exr", lutDimension, lutDimension);
        // }



        return input3DLutTex;
    }

    // public void SaveToDisk(Color[] pixels, string fileName, int width, int height)
    // {
    //     Debug.Log("Preparing to save image to disk");
    //
    //     Texture2D textureToSave = new Texture2D(width, height, TextureFormat.RGBAHalf, false, true);
    //     textureToSave.SetPixels(pixels);
    //     textureToSave.Apply();
    //     File.WriteAllBytes(@fileName, textureToSave.EncodeToEXR());
    //
    //     Debug.Log("Successfully saved " + fileName + " to disk");
    // }


    public static void SaveLutToDisk(Texture3D bakedLut, string fileNameAndPath)
    {
        if (File.Exists(fileNameAndPath))
        {
            UnityEditor.VersionControl.Provider.Checkout(fileNameAndPath, CheckoutMode.Both);
        }
        AssetDatabase.CreateAsset(bakedLut, fileNameAndPath + ".asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
