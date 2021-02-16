using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class LutBaker
{
    private ComputeShader lutBakerShader;
    private ComputeShader slicerShader;

    private HDRPipeline hdrPipeline;
    private GamutMap gamutMap;
    private int kernelHandle;
    private Material lutBakerMat;
    private bool useCPUBaker = true;

    public LutBaker(HDRPipeline inHdrPipeline, ComputeShader inLutBakeShader, ComputeShader inSlicerShader)
    {
        hdrPipeline = inHdrPipeline;
        gamutMap = hdrPipeline.getGamutMap();
        lutBakerShader = inLutBakeShader;
        kernelHandle = lutBakerShader.FindKernel("CSMain");
        slicerShader = inSlicerShader;
        // lutBakerMat = new Material(Shader.Find("Custom/LutBaker"));
    }


    public Texture3D BakeLUT(int lutDimension)
    {
        // Initialise buffer to retrieve 3D texture
        Color[][] slicesOf3DColorBuffers = new Color[lutDimension][];
        for (int i = 0; i < slicesOf3DColorBuffers.Length; i++)
        {
            slicesOf3DColorBuffers[i] = new Color[lutDimension * lutDimension];
        }

        // Create identity cube LUT
        Color[] identity3DLutPixels = LutGenerator.generateIdentityCubeLUT(lutDimension);
        var input3DLutTex = new Texture3D(lutDimension, lutDimension, lutDimension, TextureFormat.RGBAHalf, false)
        {
            // @TODO enable trilinear?
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

        // Apply color grading and the aesthetic transfer function on each slice of the 3D texture
        for (int layerIndex = 0; layerIndex < lutDimension; layerIndex++)
        {
            // Create a texture 2D from every layer of a 3D texture
            Texture2D sliceFrom3DLut = new Texture2D(lutDimension, lutDimension, TextureFormat.RGBAHalf, false, true);
            Color[] temp2DSlice = new Color[lutDimension * lutDimension];
            for (int yCoord = 0; yCoord < lutDimension; yCoord++)
            {
                for (int xCoord = 0; xCoord < lutDimension; xCoord++)
                {
                    temp2DSlice[xCoord + yCoord * lutDimension] = buffer3DPixels[xCoord + yCoord * lutDimension + layerIndex * lutDimension * lutDimension];
                }
            }
            sliceFrom3DLut.SetPixels(temp2DSlice);
            sliceFrom3DLut.Apply();

            // Render the 2D slice with color grade LUT
            Color[] hdriTexturePixels = sliceFrom3DLut.GetPixels();
            for (int i = 0; i < hdriTexturePixels.Length; i++)
            {
                hdriTexturePixels[i].r = Shaper.calculateLog2ToLinear(hdriTexturePixels[i].r,
                    hdrPipeline.getGamutMap().MidGreySdr.x,
                    hdrPipeline.getGamutMap().MinRadiometricExposure, hdrPipeline.getGamutMap().MaxRadiometricExposure);
                hdriTexturePixels[i].g = Shaper.calculateLog2ToLinear(hdriTexturePixels[i].g,
                    hdrPipeline.getGamutMap().MidGreySdr.x,
                    hdrPipeline.getGamutMap().MinRadiometricExposure, hdrPipeline.getGamutMap().MaxRadiometricExposure);
                hdriTexturePixels[i].b = Shaper.calculateLog2ToLinear(hdriTexturePixels[i].b,
                    hdrPipeline.getGamutMap().MidGreySdr.x,
                    hdrPipeline.getGamutMap().MinRadiometricExposure, hdrPipeline.getGamutMap().MaxRadiometricExposure);
            }
            hdriTexturePixels = hdrPipeline.getGamutMap().ApplyChromaticityCompression(hdriTexturePixels, true);
            sliceFrom3DLut.SetPixels(hdriTexturePixels);
            sliceFrom3DLut.Apply();

            hdrPipeline.colorGradingMat.SetTexture("_MainTex", sliceFrom3DLut);
            hdrPipeline.colorGradingMat.SetTexture("_LUT", hdrPipeline.colorGradeLUT);
            Graphics.Blit(sliceFrom3DLut, renderTexture2DSlice, hdrPipeline.colorGradingMat);

            if (useCPUBaker)
            {
                sliceFrom3DLut = gamutMap.ApplyTransferFunctionTo2DSlice(renderTexture2DSlice);
                Color[] colorSlice = sliceFrom3DLut.GetPixels();
                for (int i = 0; i < colorSlice.Length; i++)
                {
                    slicesOf3DColorBuffers[layerIndex][i] = colorSlice[i];
                }
            }
            else
            {
                Texture2D inputSlice2DTexture = hdrPipeline.getGamutMap().toTexture2D(renderTexture2DSlice);

                hdrPipeline.gamutMapMat.SetTexture("_MainTex", inputSlice2DTexture);
                hdrPipeline.gamutMapMat.SetFloat("exposure", hdrPipeline.getGamutMap().Exposure);
                hdrPipeline.gamutMapMat.SetVector("greyPoint", new Vector4(hdrPipeline.getGamutMap().MidGreySdr.x,
                    hdrPipeline.getGamutMap().MidGreySdr.y, 0.0f));
                hdrPipeline.gamutMapMat.SetFloat("minExposure", hdrPipeline.getGamutMap().MinRadiometricExposure);
                hdrPipeline.gamutMapMat.SetFloat("maxExposure", hdrPipeline.getGamutMap().MaxRadiometricExposure);
                hdrPipeline.gamutMapMat.SetFloat("maxRadiometricValue", hdrPipeline.getGamutMap().MaxRadiometricDynamicRange);
                hdrPipeline.gamutMapMat.SetFloat("minDisplayExposure", hdrPipeline.getGamutMap().MinDisplayExposure);
                hdrPipeline.gamutMapMat.SetFloat("maxDisplayExposure", hdrPipeline.getGamutMap().MaxDisplayExposure);
                hdrPipeline.gamutMapMat.SetFloat("minDisplayValue", hdrPipeline.getGamutMap().MinDisplayValue);
                hdrPipeline.gamutMapMat.SetFloat("maxDisplayValue", hdrPipeline.getGamutMap().MaxDisplayValue);
                hdrPipeline.gamutMapMat.SetFloat("maxLatitudeLimit", hdrPipeline.getGamutMap().CurveCoordMaxLatitude);
                hdrPipeline.gamutMapMat.SetInt("inputArraySize", hdrPipeline.getGamutMap().getXValues().Count - 1);
                hdrPipeline.gamutMapMat.SetInt("usePerChannel", 0);

                hdrPipeline.XCurveCoordsCBuffer.SetData(hdrPipeline.getGamutMap().getXValues().ToArray());
                hdrPipeline.YCurveCoordsCBuffer.SetData(hdrPipeline.getGamutMap().getYValues().ToArray());
                hdrPipeline.gamutMapMat.SetBuffer(Shader.PropertyToID("xCurveCoordsCBuffer"), hdrPipeline.XCurveCoordsCBuffer);
                hdrPipeline.gamutMapMat.SetBuffer(Shader.PropertyToID("yCurveCoordsCBuffer"), hdrPipeline.YCurveCoordsCBuffer);
                hdrPipeline.gamutMapMat.SetVectorArray("controlPoints", hdrPipeline.ControlPointsUniform);

                Graphics.Blit(inputSlice2DTexture, renderTexture2DSlice, hdrPipeline.gamutMapMat);

                // Retrieve the 2D slice rendered and store it back into a 2D array of slices
                Color[] colorSlice = hdrPipeline.getGamutMap().toTexture2D(renderTexture2DSlice).GetPixels();//sliceFrom3DLut.GetPixels();
                for (int i = 0; i < colorSlice.Length; i++)
                {
                    slicesOf3DColorBuffers[layerIndex][i] = colorSlice[i];
                }
            }
        }

        // Concatenate every 2D slice into a single array
        Color[] final3DTexturePixels = new Color[lutDimension * lutDimension * lutDimension];
        int index = 0;
        for (int l = 0; l < slicesOf3DColorBuffers.Length; l++)
        {
            // final3DTexturePixels = final3DTexturePixels.Concat(slicesOf3DColorBuffers[l]).ToArray();
            for (int i = 0; i < slicesOf3DColorBuffers[l].Length; i++)
            {
                final3DTexturePixels[index] = slicesOf3DColorBuffers[l][i];
                index++;
            }
        }
        // Write the single array color information into the 3D texture
        input3DLutTex.SetPixels(final3DTexturePixels);
        input3DLutTex.Apply();


        // // Debug: Write every slice to disk to save it
        // for (int i = 0; i < slicesOf3DColorBuffers.Length; i++)
        // {
        //     hdrPipeline.getGamutMap().SaveToDisk(slicesOf3DColorBuffers[i], "Layer" + i + ".exr", lutDimension, lutDimension);
        //
        // }


        
        // RenderTexture output3DLutTex = new RenderTexture(lutDimension, lutDimension, lutDimension, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        // output3DLutTex.enableRandomWrite = true;
        // output3DLutTex.Create();
        //
        // // Setup the compute lutBakerShader
        // lutBakerShader.SetTexture(kernelHandle, "inputLut", input3DLutTex);
        // lutBakerShader.SetTexture(kernelHandle, "outputLut", output3DLutTex);
        // lutBakerShader.Dispatch(kernelHandle, 17, 17, 17);
        // Save(lutDimension, output3DLutTex);


        // Apply Chromaticity compression
        //Color[] lutPixels = gamutMap.ApplyChromaticityCompression(identity3DLut);
        // ColorGrade blit
        // hdrPipeline.RenderColorGrade();
        // dest.ReadPixels(new Rect(0, 0, dest.width, dest.height), 0, 0, false);
        // Aesthetic TF baking
        // gamutMap.CalculateTransferTransform(ref lutPixels);
        // Store data in LUT

        return input3DLutTex;
    }

    private void Save(int lutDimension, RenderTexture inputRT)
    {
        Texture3D export = new Texture3D(lutDimension, lutDimension, lutDimension, TextureFormat.RGBAHalf, false);
        RenderTexture selectedRenderTexture = inputRT;

        RenderTexture[] layers = new RenderTexture[lutDimension];
        for( int i = 0; i < lutDimension; i++)
            layers[i] = Copy3DSliceToRenderTexture(selectedRenderTexture, i, lutDimension);

        Texture2D[] finalSlices = new Texture2D[lutDimension];
        for ( int i = 0; i < lutDimension; i++)
            finalSlices[i] = ConvertFromRenderTexture(layers[i], lutDimension);

        Texture3D output = new Texture3D(lutDimension, lutDimension, lutDimension, TextureFormat.RGBAHalf, false);
        output.filterMode = FilterMode.Trilinear;
        Color[] outputPixels = output.GetPixels();

        for (int k = 0; k < lutDimension; k++) {
            Color[] layerPixels = finalSlices[k].GetPixels();
            for (int i = 0; i < lutDimension; i++)
            for (int j = 0; j < lutDimension; j++)
            {
                outputPixels[i + j * lutDimension + k * lutDimension * lutDimension] = layerPixels[i + j * lutDimension];
            }
        }

        output.SetPixels(outputPixels);
        output.Apply();

        AssetDatabase.CreateAsset(output, "Assets/" + "nameOfTheAsset" + ".asset");
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
