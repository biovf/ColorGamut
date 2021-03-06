﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Text;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Serialization;

public class ColorGradingHDR : MonoBehaviour
{
    public bool enableColorGrading = true;

    public Material colorGradingMat;
    public Material colorGrading3DTextureMat;
    public Material fullscreenMat;
    public Material toneMapMat;
    
    public Texture3D hdr3DLutToDecode;
    public Texture2D testTexture;

    private RenderTexture colorGradeRT;
    private RenderTexture toneMapRT;
    private Texture2D encodedInputTexture;
    
    private RenderTexture decodedRTLUT;
    private RenderTexture interceptDebugRT;

    void Start()
    {
        colorGradeRT = new RenderTexture(testTexture.width, testTexture.height, 0, RenderTextureFormat.ARGBHalf,
            RenderTextureReadWrite.Linear);
        toneMapRT = new RenderTexture(testTexture.width, testTexture.height, 0, RenderTextureFormat.ARGBHalf,
            RenderTextureReadWrite.Linear);
        interceptDebugRT = new RenderTexture(testTexture.width, testTexture.height, 0, RenderTextureFormat.ARGBHalf,
            RenderTextureReadWrite.Linear);
    }
    
    private void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (enableColorGrading)
        {
            Graphics.Blit(testTexture, colorGradeRT, fullscreenMat);
            Graphics.Blit(colorGradeRT, interceptDebugRT, fullscreenMat);

            colorGrading3DTextureMat.SetTexture("_LUT", hdr3DLutToDecode);
            Graphics.Blit(colorGradeRT, toneMapRT, colorGrading3DTextureMat);
            Graphics.Blit(toneMapRT, dest, toneMapMat);
        }
        else
        {
            Graphics.Blit(testTexture, dest, fullscreenMat);
        }
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            int xCoord = (int) Input.mousePosition.x;
            int yCoord = (int) Input.mousePosition.y;
            Texture2D colorGradedTexture = toTexture2D(colorGradeRT);
            Color colorGradedValues = colorGradedTexture.GetPixel(xCoord, yCoord);
            Debug.Log("Pixel color is \t" + colorGradedValues.ToString("F4"));
        }
    }

    public void generateCubeLut(string fileName, int lutDimension, bool useShaperFunction, bool useDisplayP3, float maxRadiometricValue)
    {
        Debug.Log("Generating .cube LUT of size " + lutDimension.ToString());
        
        Color[] hdr1DLut = LutGenerator.generateHdrTexLut(lutDimension, maxRadiometricValue, useShaperFunction);
        CubeLutExporter.saveLutAsCube(hdr1DLut, fileName, lutDimension, Vector3.zero,
            new Vector3(maxRadiometricValue, maxRadiometricValue, maxRadiometricValue), true);
    }

    public void saveInGameCapture(string saveFilePath)
    {
        // Encode in PQ
        PQShaper shaper = new PQShaper();
        Vector3 colorVec = Vector3.zero;

        Texture2D inGameCapture = toTexture2D(interceptDebugRT);
        Color[] inGameCapturePixels = inGameCapture.GetPixels();
        int inGameCapturePixelsLen = inGameCapturePixels.Length;

        for (int i = 0; i < inGameCapturePixelsLen; i++)
        {
            colorVec.Set(inGameCapturePixels[i].r, inGameCapturePixels[i].g, inGameCapturePixels[i].b);
            colorVec = shaper.LinearToPQ(colorVec, 100.0f);
            inGameCapturePixels[i] = new Color(colorVec.x, colorVec.y, colorVec.z);
        }
        SaveToDisk(inGameCapturePixels, saveFilePath, inGameCapture.width, inGameCapture.height);
    }

    public void generateTextureLut(string fileName, int lutDimension, bool generateHDRLut, bool useShaperFunction, bool useDisplayP3,
        float maxRadiometricValue)
    {
        Debug.Log("Generating LUT of size " + lutDimension.ToString());
        // TODO generateHdrTexLut does not use the P3 conversion yet
        Color[] lutColorArray;
        if (generateHDRLut)
        {
            Debug.Log("Generating HDR texture Lut");
            lutColorArray = LutGenerator.generateHdrTexLut(lutDimension, maxRadiometricValue, useShaperFunction);
           
            Debug.Log("Saving HDR texture Lut to disk");
            SaveToDisk(lutColorArray, fileName, lutDimension * lutDimension, lutDimension);
        }
        else
        {
            Debug.Log("Generating SDR texture Lut");
            lutColorArray = LutGenerator.generateSdrTexLut(lutDimension);
            
            Debug.Log("Saving SDR texture Lut to disk");
            SaveToDisk(lutColorArray, fileName, lutDimension * lutDimension, lutDimension, false);
        }
    }

    private Texture2D toTexture2D(RenderTexture rTex)
    {
        Texture2D tex = new Texture2D(rTex.width, rTex.height, TextureFormat.RGBAHalf, false, true);
        
        RenderTexture.active = rTex;
        tex.ReadPixels(new Rect(0, 0, rTex.width, rTex.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        return tex;
    }
    
    private Color[] decodeHDRLutPQToLinear(Color[] lutPixels, float maxValue)
    {
        Debug.Log("Starting to decode LUT with max value " + maxValue.ToString());
        PQShaper pqShaper = new PQShaper();
        int lutPixelsLen = lutPixels.Length;
        Vector3 tmpColorVec = Vector3.zero;
        
        for (int i = 0; i < lutPixelsLen; i++)
        {
            tmpColorVec.Set(lutPixels[i].r, lutPixels[i].g, lutPixels[i].b);
            Vector3 resultColor = pqShaper.PQToLinear(tmpColorVec, maxValue);
            lutPixels[i] = new Color(resultColor.x, resultColor.y, resultColor.z, lutPixels[i].a);
        }

        Debug.Log("Finished decoding LUT");
        return lutPixels;
    }

    private Color[] applyPQToTexture(Color[] pixels)
    {
        Debug.Log("Starting to encode LUT");
        PQShaper pqShaper = new PQShaper();
        Vector3 tmpColorVec = Vector3.zero;
        int pixelsLen = pixels.Length;
        for (int i = 0; i < pixelsLen; i++)
        {
            tmpColorVec.Set(pixels[i].r, pixels[i].g, pixels[i].b);
            Vector3 resultColor = pqShaper.LinearToPQ(tmpColorVec);
            pixels[i] = new Color(resultColor.x, resultColor.y, resultColor.z, pixels[i].a);
        }

        Debug.Log("Finished encoding LUT");
        return pixels;
    }
    
    private void SaveToDisk(Color[] pixels, string fileName, int width, int height, bool useExr = true)
    {
        Debug.Log("Preparing to save texture to disk");

        if (useExr)
        {
            Texture2D textureToSave = new Texture2D(width, height, TextureFormat.RGBAHalf, false, true);
            textureToSave.SetPixels(pixels);
            textureToSave.Apply();
            File.WriteAllBytes(@fileName, textureToSave.EncodeToEXR());
        }
        else
        {
            Texture2D textureToSave = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            textureToSave.SetPixels(pixels);
            textureToSave.Apply();
            File.WriteAllBytes(@fileName, textureToSave.EncodeToPNG());
        }

        Debug.Log("Texture " + fileName + " successfully saved to disk");
    }
    
    /// ***********************************************************
    /// Section with tests to verify correctness of the algorithms
    /// ***********************************************************
    private void testGenerateTestImageCombinations(Texture2D testImage)
    {
        Color[] pixels = testImage.GetPixels();
        string fileName = testImage.name + "LinearsRGB.exr";
        SaveToDisk(pixels, fileName, testImage.width, testImage.height);

        Color[] temp = applyPQToTexture(pixels);
        fileName = testImage.name + "PQsRGB.exr";
        SaveToDisk(temp, fileName, testImage.width, testImage.height);

        ColorSpace cs = new ColorSpace();
        for (int i = 0; i < pixels.Length; i++)
        {
            temp[i] = cs.srgbToDisplayP3(pixels[i]);
        }

        temp = applyPQToTexture(temp);
        fileName = testImage.name + "PQDisplayP3.exr";
        SaveToDisk(temp, fileName, testImage.width, testImage.height);
        StringBuilder colorInfo = new StringBuilder();
        for (int i = 0; i < pixels.Length; i++)
        {
            temp[i] = cs.srgbToDisplayP3(pixels[i]);
            colorInfo.Append("Before: \t" + pixels[i].ToString() + "| After: \t" + temp[i].ToString() + "\n" +
                             (pixels[i] != temp[i] ? " <------------------------ DIFERENT" : ""));
        }

        fileName = testImage.name + "LinearDisplayP3.exr";
        SaveToDisk(temp, fileName, testImage.width, testImage.height);
        Debug.Log("Saving log file to disk");
        File.WriteAllText(testImage.name + "PQDisplayP3Log.txt", colorInfo.ToString());
    }


    private void testCompareTextureContents(Texture2D tex1, Texture2D tex2)
    {
        // Get pixels and save temp color
        Color[] tex1Texels = tex1.GetPixels();
        Color[] tex2Texels = tex2.GetPixels();
        int[] randomPixels = {0, 32, 64, 100, 128};
        for (int i = 0; i < 5; i++)
        {
            Color tex1PixelColor = tex1Texels[randomPixels[i]];
            Color tex2PixelColor = tex2Texels[randomPixels[i]];
            Debug.Log("Tex1 Pixel " + i + " \tdata " + tex1PixelColor.ToString());
            Debug.Log("Tex2 Pixel " + i + " \tdata " + tex2PixelColor.ToString());
            Debug.Log("------------------------------------------------------------------");
        }
    }

    private void testEncodeDecode(Texture2D originalTex)
    {
        // Get pixels and save temp color
        Color[] lutPixels = originalTex.GetPixels();
        int[] randomPixels = {0, 48, 64, 153, 512};
        PQShaper pqShaper = new PQShaper();
        for (int i = 0; i < 5; i++)
        {
            Color startPixelColor = lutPixels[randomPixels[i]];
            Vector3 pixelColorPQ =
                pqShaper.LinearToPQ(new Vector3(startPixelColor.r, startPixelColor.g, startPixelColor.b));
            Vector3 finalPixelData = pqShaper.PQToLinear(pixelColorPQ);
            Color finalPixelColor = new Color(finalPixelData.x, finalPixelData.y, finalPixelData.z);
            Debug.Log("Initial \tdata " + startPixelColor.ToString());
            Debug.Log("Final \tdata " + finalPixelColor.ToString());
            Debug.Log("------------------------------------------------------------------");
        }
    }

    private void testDecode(Texture2D encodedTex, Texture2D originalTex)
    {
        // Get pixels and save temp color
        Color[] lutPixels = encodedTex.GetPixels();
        Color[] originalTexPixels = originalTex.GetPixels();
        PQShaper pqShaper = new PQShaper();
        int[] randomPixels = {0, 48, 64, 153, 512};
        for (int i = 0; i < 5; i++)
        {
            Color encodedPixelColor = lutPixels[randomPixels[i]];
            Color originalPixelColor = originalTexPixels[randomPixels[i]];

            Vector3 finalPixelData =
                pqShaper.PQToLinear(new Vector3(encodedPixelColor.r, encodedPixelColor.g, encodedPixelColor.b));
            Color finalPixelColor = new Color(finalPixelData.x, finalPixelData.y, finalPixelData.z);
            Debug.Log("Original Data \tdata " + originalPixelColor.ToString());
            Debug.Log("Decoded  Data \tdata " + finalPixelColor.ToString());
            Debug.Log("------------------------------------------------------------------");
        }
    }
}