using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Serialization;

[CustomEditor(typeof(ColorGradingHDR1))]
public class ColorGradingHDREditor1 : Editor
{
    #region CubeLut Variables

    private bool useShaperFunction = true;
    private bool useDisplayP3 = false;
    private bool foldoutStateCubeLut = false;
    private int lutDimension = 33;
    private float maxRadiometricValue = 1.0f;
    private string outPathCubeLut = "";
    private string defaultCubeLutFileName;
    #endregion

    #region Lut Texture Variables
    private bool generateHDRLut = false;
    private bool useShaperFunctionTexLut = true;
    private bool useDisplayP3TexLut = false;
    private bool foldoutState1DLut = false;
    private int lutDimensionTexLut = 32;
    private float maxRadiometricValueTexLut = 1.0f;
    private string outPathTextureLut = "";
    private string defaultTextureFileName;
    #endregion

    #region EXR Capture Variables 
    private bool foldoutStateSaveExr = false;
    private string outPathGameCapture = "";
    #endregion

    private ColorGradingHDR1 colorGradingHDR;

    public void OnEnable()
    {
        // Initialise default file name for texture lut
        defaultTextureFileName = "LUT" + lutDimensionTexLut +
                                 (useShaperFunctionTexLut == true ? "PQ" : "Linear") +
                                 (useDisplayP3TexLut == true ? "DisplayP3" : "sRGB") +
                                 "MaxRange" + maxRadiometricValueTexLut.ToString();
        
        defaultCubeLutFileName = "CubeLut" + lutDimension.ToString() +
                                 (useShaperFunction == true ? "PQ" : "Linear") +
                                 (useDisplayP3 == true ? "DisplayP3" : "sRGB") +
                                 "MaxRange" + maxRadiometricValue.ToString();
        
    }

    public override void OnInspectorGUI()
    {
        colorGradingHDR = (ColorGradingHDR1) target;

        DrawDefaultInspector();
        DrawSaveExrInspectorProps();
        DrawTexLutInspectorProps();
        DrawCubeInspectorProps();
    }

    private void DrawSaveExrInspectorProps()
    {
        EditorGUILayout.Space(10.0f);
        EditorGUILayout.LabelField("In-Game Capture ");

        if (GUILayout.Button("Save Game Capture To"))
        {
            outPathGameCapture = EditorUtility.SaveFilePanel("In Game capture EXR...", "", "Capture", "exr");

            if (string.IsNullOrEmpty(outPathGameCapture))
            {
                Debug.LogError("File path to save game capture is invalid");
                return;
            }
         
            colorGradingHDR.saveInGameCapture(outPathGameCapture);
        }
        else if (outPathGameCapture.Length < 1)
        {
            outPathGameCapture = Application.dataPath;
        }

        outPathGameCapture = EditorGUILayout.TextField("Save to", outPathGameCapture);
    }

    private void DrawTexLutInspectorProps()
    {
        EditorGUILayout.Space(10.0f);
        EditorGUILayout.LabelField("Color Grading LUT Texture ");
        foldoutState1DLut = EditorGUILayout.Foldout(foldoutState1DLut, "Generate Texture LUT Options", true);
        if (foldoutState1DLut)
        {
            generateHDRLut = EditorGUILayout.Toggle("HDR", generateHDRLut);
            if (generateHDRLut == true)
            {
                useShaperFunctionTexLut = EditorGUILayout.Toggle("Use Shaper Function", useShaperFunctionTexLut);
                if (useShaperFunctionTexLut)
                {
                    maxRadiometricValueTexLut =
                        EditorGUILayout.FloatField("Max Radiometric Value", maxRadiometricValueTexLut);
                }

                useDisplayP3TexLut = EditorGUILayout.Toggle("Convert to Display-P3", useDisplayP3TexLut);
                // TODO explore the GUILayoutOptions to show that the floating point number is actually a FP value
            }

            lutDimensionTexLut = EditorGUILayout.IntField("LUT Dimension", lutDimensionTexLut);
        }

        if (GUILayout.Button("Generate LUT Texture"))
        {
            if (generateHDRLut)
            {
                defaultTextureFileName = "HDR_Lut" + lutDimensionTexLut +
                                         (useShaperFunctionTexLut == true ? "PQ" : "Linear") +
                                         (useDisplayP3TexLut == true ? "DisplayP3" : "sRGB") +
                                         "MaxRange" + maxRadiometricValueTexLut.ToString();
            }
            else
            {
                defaultTextureFileName = "SDR_Lut" + lutDimensionTexLut;
            }

            outPathTextureLut = EditorUtility.SaveFilePanel("Save LUT Texture to...", "", defaultTextureFileName, 
                (generateHDRLut ? "exr" : "png"));

            if (string.IsNullOrEmpty(outPathTextureLut))
            {
                Debug.LogError("File path to save Lut texture is invalid");
                return;
            }

            colorGradingHDR.generateTextureLut(outPathTextureLut, lutDimensionTexLut, generateHDRLut, useShaperFunctionTexLut, useDisplayP3TexLut,
                maxRadiometricValueTexLut);
        }
    }

    private void DrawCubeInspectorProps()
    {
        EditorGUILayout.Space(10.0f);
        EditorGUILayout.LabelField("Color Grading .cube LUT File");
        foldoutStateCubeLut = EditorGUILayout.Foldout(foldoutStateCubeLut, "Generate .cube LUT Options", true);
        if (foldoutStateCubeLut)
        {
            useShaperFunction = EditorGUILayout.Toggle("Use Shaper Function", useShaperFunction);
            if (useShaperFunction)
            {
                maxRadiometricValue = EditorGUILayout.FloatField("Max Radiometric Value", maxRadiometricValue);
            }

            useDisplayP3 = EditorGUILayout.Toggle("Convert to Display-P3", useDisplayP3);
            lutDimension = EditorGUILayout.IntField("LUT Dimension", lutDimension);
        }

        if (GUILayout.Button("Generate .cube LUT File"))
        {
            defaultCubeLutFileName = "CubeLut" + lutDimension.ToString() +
                                     (useShaperFunction == true ? "PQ" : "Linear") +
                                     (useDisplayP3 == true ? "DisplayP3" : "sRGB") +
                                     "MaxRange" + maxRadiometricValue.ToString();
            outPathCubeLut = EditorUtility.SaveFilePanel("Save .cube LUT file to...", "", defaultCubeLutFileName, 
                     "cube" );

            if (string.IsNullOrEmpty(outPathCubeLut))
            {
                Debug.LogError("File path to save cube Lut file is invalid");
                return;
            }

            colorGradingHDR.generateCubeLut(outPathCubeLut, lutDimension, useShaperFunction, useDisplayP3, maxRadiometricValue);
        }
    }
}