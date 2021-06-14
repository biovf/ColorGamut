using System;
 using UnityEditor;
using UnityEngine;

public class LutBaker
{
    private ComputeShader lutBakerShader;
    private ComputeShader slicerShader;

    private HDRPipeline hdrPipeline;
    private GamutMap gamutMap;
    private Material lutBakerMat;
    private bool useCPUBaker = true;
    private Material chromaticityCompMat;

    public LutBaker(HDRPipeline inHdrPipeline)
    {
        hdrPipeline = inHdrPipeline;
        gamutMap = hdrPipeline.getGamutMap();
        Shader chromaticityCompression = Shader.Find("Custom/ChromaticityCompression");
        chromaticityCompMat = new Material(chromaticityCompression);
    }


    public Texture3D BakeLUT(int lutDimension)
    {
        // Initialise buffer to retrieve 3D texture into
        Color[][] slicesOf3DColorBuffers = new Color[lutDimension][];
        for (int i = 0; i < slicesOf3DColorBuffers.Length; i++)
        {
            slicesOf3DColorBuffers[i] = new Color[lutDimension * lutDimension];
        }

        // Create an identity cube LUT
        Color[] identity3DLutPixels = LutGenerator.generateIdentityCubeLUT(lutDimension);
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
        RenderTexture renderTexture2DSlicePing = new RenderTexture(lutDimension, lutDimension, 1,
            RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        RenderTexture renderTexture2DSlicePong = new RenderTexture(lutDimension, lutDimension, 1,
          RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);

        // This loop bakes into a 2D slice from a 3D texture the following operations:
        // - Chromaticity Compression
        // - Color Grading
        // - Aesthetic Transfer Function
        // The result will be stored into an array of 2D slices that will be later used to create a 3D texture
        for (int layerIndex = 0; layerIndex < lutDimension; layerIndex++)
        {
            // Create a texture 2D from every layer of a 3D texture
            Texture2D slice2DFrom3DLut = new Texture2D(lutDimension, lutDimension, TextureFormat.RGBAHalf, false, true);
            // Create a colour array so that we're able to change the 2D texture slice data
            Color[] temp2DSlice = new Color[lutDimension * lutDimension];
            // Iterate over the y coordinates and x columns and convert the data from Log2 to linear
            for (int yCoord = 0; yCoord < lutDimension; yCoord++)
            {
                for (int xCoord = 0; xCoord < lutDimension; xCoord++)
                {
                    Color buffer3DColor = buffer3DPixels[xCoord + yCoord * lutDimension + layerIndex * lutDimension * lutDimension];
                    buffer3DColor.r = Shaper.calculateLog2ToLinear(buffer3DColor.r, gamutMap.MidGreySdr.x, gamutMap.MinRadiometricExposure,
                        gamutMap.MaxRadiometricExposure);
                    buffer3DColor.g = Shaper.calculateLog2ToLinear(buffer3DColor.g, gamutMap.MidGreySdr.x, gamutMap.MinRadiometricExposure,
                        gamutMap.MaxRadiometricExposure);
                    buffer3DColor.b = Shaper.calculateLog2ToLinear(buffer3DColor.b, gamutMap.MidGreySdr.x, gamutMap.MinRadiometricExposure,
                        gamutMap.MaxRadiometricExposure);
                    temp2DSlice[xCoord + yCoord * lutDimension] = buffer3DColor;
                }
            }
            // Bake the chromaticity compression into the 2D array of colours
            // Input Data Format: Radiometric linear
            // Output Data Format: Log2 camera intrinsic
            //temp2DSlice = gamutMap.ApplyChromaticityCompressionCPU(temp2DSlice);
            temp2DSlice = gamutMap.luminanceCompression(temp2DSlice);

            // Write this array of colours back into the 2D texture
            slice2DFrom3DLut.SetPixels(temp2DSlice);
            slice2DFrom3DLut.Apply();

            // Bake the colour grading step
            hdrPipeline.colorGradingMat.SetTexture("_MainTex", slice2DFrom3DLut);
            hdrPipeline.colorGradingMat.SetTexture("_LUT", hdrPipeline.colorGradeLUT);
            Graphics.Blit(slice2DFrom3DLut, renderTexture2DSlicePing, hdrPipeline.colorGradingMat);

            // Bake the Aesthetic Transfer function to the 2D array of colours
            slice2DFrom3DLut = gamutMap.ApplyTransferFunctionTo2DSlice(renderTexture2DSlicePing);
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

        // Debug: Write every slice to disk to save it
        // for (int i = 0; i < slicesOf3DColorBuffers.Length; i++)
        // {
        //     hdrPipeline.getGamutMap().SaveToDisk(slicesOf3DColorBuffers[i], "ColorAppLayer" + i + ".exr", lutDimension, lutDimension);
        //
        // }

        return input3DLutTex;
    }

    public static void SaveLutToDisk(Texture3D bakedLut, string fileNameAndPath)
    {

        AssetDatabase.CreateAsset(bakedLut, fileNameAndPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Texture3D export = new Texture3D(lutDimension, lutDimension, lutDimension, TextureFormat.RGBAHalf, false);
        // RenderTexture selectedRenderTexture = inputRT;
        //
        // RenderTexture[] layers = new RenderTexture[lutDimension];
        // for( int i = 0; i < lutDimension; i++)
        //     layers[i] = Copy3DSliceToRenderTexture(selectedRenderTexture, i, lutDimension);
        //
        // Texture2D[] finalSlices = new Texture2D[lutDimension];
        // for ( int i = 0; i < lutDimension; i++)
        //     finalSlices[i] = ConvertFromRenderTexture(layers[i], lutDimension);

        // Texture3D output = new Texture3D(lutDimension, lutDimension, lutDimension, TextureFormat.RGBAHalf, false);
        // output.filterMode = FilterMode.Trilinear;
        // Color[] outputPixels = output.GetPixels();
        //
        // for (int k = 0; k < lutDimension; k++) {
        //     Color[] layerPixels = finalSlices[k].GetPixels();
        //     for (int i = 0; i < lutDimension; i++)
        //     for (int j = 0; j < lutDimension; j++)
        //     {
        //         outputPixels[i + j * lutDimension + k * lutDimension * lutDimension] = layerPixels[i + j * lutDimension];
        //     }
        // }
        //
        // output.SetPixels(outputPixels);
        // output.Apply();

    }

    private RenderTexture Copy3DSliceToRenderTexture(RenderTexture source, int layer, int lutDimension)
    {
         RenderTexture render = new RenderTexture(lutDimension, lutDimension, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
         render.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
         render.enableRandomWrite = true;
         render.wrapMode = TextureWrapMode.Clamp;
         render.Create();

         int kernelIndex = slicerShader.FindKernel("CSMain");
         slicerShader.SetTexture(kernelIndex, "voxels", source);
         slicerShader.SetInt("layer", layer);
         slicerShader.SetTexture(kernelIndex, "Result", render);
         slicerShader.Dispatch(kernelIndex, lutDimension, lutDimension, 1);

         return render;
    }

     private Texture2D ConvertFromRenderTexture(RenderTexture rt, int lutDimension)
     {
         Texture2D output = new Texture2D(lutDimension, lutDimension);
         RenderTexture.active = rt;
         output.ReadPixels(new Rect(0, 0, lutDimension, lutDimension), 0, 0);
         output.Apply();
         return output;
     }




}
