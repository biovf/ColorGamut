using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using System.IO;

public class CubeLutExporter 
{
    public static void saveLutAsCube(float[] inputLutTex, string fileName, int lutDim,
        Vector3 domainMin, Vector3 domainMax, bool convertTo3DLut, string outFilePath = "")
    {
        StringBuilder fileContents = new StringBuilder();
        fileContents.Append("TITLE \"" + fileName + "\"");
        fileContents.AppendLine();
        
        fileContents.Append("DOMAIN_MIN " + domainMin.x.ToString() + " " + domainMin.y.ToString() + " " + domainMin.z.ToString());
        fileContents.AppendLine();
        
        fileContents.Append("DOMAIN_MAX " + domainMax.x.ToString() + " " + domainMax.y.ToString() + " " + domainMax.z.ToString());
        fileContents.AppendLine();
        
        if (convertTo3DLut == false)
        {
            int texPixelsLen = lutDim;//inputLutTex.Length;
            int lut1DSize = texPixelsLen;
            fileContents.Append("LUT_1D_SIZE " + lut1DSize.ToString());
            fileContents.AppendLine();
            for (int i = 0; i < texPixelsLen; i++)
            {
                fileContents.Append(inputLutTex[i].ToString() + " " + inputLutTex[i].ToString() + " " +
                                    inputLutTex[i].ToString());
                fileContents.AppendLine();
            }
        }
        else
        {
            // Write out 3D file
            fileContents.Append("LUT_3D_SIZE " + lutDim.ToString());
            fileContents.AppendLine();

            float[] lut3D = convertLut1DTo3D(inputLutTex,lutDim);
            for (int i = 0; i < lut3D.Length; i++)
            {
                fileContents.Append(lut3D[i].ToString() + " " + lut3D[i].ToString() + " " + lut3D[i].ToString());
                fileContents.AppendLine();
            }
        }

        string outFile = fileName;
        if (outFilePath.Length > 0)
            outFile = outFilePath + "\\" + outFile;
        
        File.WriteAllText(outFile, fileContents.ToString());
        Debug.Log("Saved file " + outFile + " to disk");

    }
    
    
    
    public static void saveLutAsCube(Color[] inputLutTex, string fileName, int lutDim,
        Vector3 domainMin, Vector3 domainMax, bool isLut3D, string outFilePath = "")
    {
        StringBuilder fileContents = new StringBuilder();
        fileContents.Append("TITLE \"" + fileName + "\"");
        fileContents.AppendLine();
        
        fileContents.Append("DOMAIN_MIN " + domainMin.x.ToString() + " " + domainMin.y.ToString() + " " + domainMin.z.ToString());
        fileContents.AppendLine();
        
        fileContents.Append("DOMAIN_MAX " + domainMax.x.ToString() + " " + domainMax.y.ToString() + " " + domainMax.z.ToString());
        fileContents.AppendLine();
        
        if (isLut3D == false)
        {
            // Write a 1D LUT out
            int texPixelsLen = inputLutTex.Length;
            int lut1DSize = texPixelsLen;
            fileContents.Append("LUT_1D_SIZE " + lut1DSize.ToString());
            fileContents.AppendLine();
            for (int i = 0; i < texPixelsLen; i++)
            {
                fileContents.Append(inputLutTex[i].r.ToString() + " " + inputLutTex[i].g.ToString() + " " +
                                    inputLutTex[i].b.ToString());
                fileContents.AppendLine();
            }
        }
        else
        {
            // Write out a 3D LUT file
            fileContents.Append("LUT_3D_SIZE " + lutDim.ToString());
            fileContents.AppendLine();

            // Color[] lut3D = convertLut1DTo3D(inputLutTex,lutDim);
            int inputLutColorsArrayLen = inputLutTex.Length;
            for (int i = 0; i < inputLutColorsArrayLen; i++)
            {
                fileContents.Append(inputLutTex[i].r.ToString() + " " + inputLutTex[i].g.ToString() + " " + inputLutTex[i].b.ToString());
                fileContents.AppendLine();
            }
        }

        string outFile = fileName;
        if (outFilePath.Length > 0)
            outFile = outFilePath + "\\" + outFile;
        
        Debug.Log("Writing file " + outFile + " to disk");
        File.WriteAllText(outFile, fileContents.ToString());
    }

    private static Color[] convertLut1DTo3D(Color[] lut1d, int lutSliceDim)
    {
        int lut1dLen = lut1d.Length;
        Color[] lut3d = new Color[lut1dLen];
        int textureWidth = lutSliceDim * lutSliceDim;
        int counter = 0;
        
        for (int cellIndex = 0; cellIndex < lutSliceDim; cellIndex++)
        {
            for (int row = 0; row < lutSliceDim; row++)
            {
                for (int index = 0; index < lutSliceDim; index++)
                {
                    int lut1dIndex = row * textureWidth  + (index + (cellIndex * lutSliceDim));
                    lut3d[counter] = lut1d[lut1dIndex];
                    counter++;
                }
            }
        }
        return lut3d;
    }

    private static float[] convertLut1DTo3D(float[] lut1d, int lutSliceDim)
    {
        int lut1dLen = lut1d.Length;
        float[] lut3d = new float[lut1dLen];
        int textureWidth = lutSliceDim * lutSliceDim;
        int counter = 0;
        
        for (int cellIndex = 0; cellIndex < lutSliceDim; cellIndex++)
        {
            for (int row = 0; row < lutSliceDim; row++)
            {
                for (int index = 0; index < lutSliceDim; index++)
                {
                    int lut1dIndex = row * textureWidth  + (index + (cellIndex * lutSliceDim));
                    lut3d[counter] = lut1d[lut1dIndex];
                    counter++;
                }
            }
        }
        return lut3d;
    }
    
}
