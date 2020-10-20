using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


[CustomEditor(typeof(HDRPipeline))]
public class HDRPipelineEditor : Editor
{
    HDRPipeline hdrPipeline;
    private ColorGamut1 colorGamut;
    private float exposure = 1;
    private int bleachingRatioPower = 2;
    private TransferFunction activeTransferFunction = TransferFunction.Max_RGB;

    #region Parametric Curve Parameters

    private float slope;
    private float originPointX;
    private float originPointY;
    private float greyPointX;
    private float greyPointY;

    #endregion

    
    private List<string> hdriNames;

    private int hdriIndex = 0;

    private bool showSweep = false;
    private bool enableBleaching = true;
    private bool isMultiThreaded = false;
    private bool showPixelsOutOfGamut = false;

    AnimationCurve animationCurve;
    private Vector2[] controlPoints;
    private CurveTest parametricCurve;

    private List<float> tValues;
    private List<float> xValues;
    private List<float> yValues;
    private List<Vector3> debugPoints = new List<Vector3>();

    // Color grading member variables
    private Vector3 cubeWidgetSize = new Vector3(0.01f, 0.01f, 0.01f);
    
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

    private bool isColorGradingTabOpen = true;
    private ColorGradingHDR1 colorGradingHDR;
    private bool shapeImage = true;

    // Prototype the state approach to test caching of data
    private enum CurveGuiDataState
    {
        NotCalculated,
        MustRecalculate,
        Calculated
    }

    private CurveGuiDataState _curveGuiDataState = CurveGuiDataState.NotCalculated;

    private List<float> xTempValues;
    private List<float> yTempValues;
    
    public void OnEnable()
    {
        hdrPipeline = (HDRPipeline) target;
        if (!hdrPipeline.isActiveAndEnabled)
            return;
        
        colorGamut = hdrPipeline.getColorGamut();
        colorGradingHDR = hdrPipeline.getColorGrading();
        if (colorGamut != null)
        {
            hdriNames = new List<string>();
            if (hdrPipeline.HDRIList != null)
            {
                for (int i = 0; i < hdrPipeline.HDRIList.Count; i++)
                {
                    hdriNames.Add(new string(hdrPipeline.HDRIList[i].ToString().ToCharArray()));
                }
            }

            colorGamut.setInputTexture(hdrPipeline.HDRIList[hdriIndex]);
        }

        // Initialise parameters for the curve with sensible values
        if (_curveGuiDataState == CurveGuiDataState.NotCalculated)
            colorGamut.getParametricCurveValues(out slope, out originPointX, out originPointY, out greyPointX,
                out greyPointY);
        
        // Color grading initialisation
        defaultTextureFileName = "LUT" + lutDimensionTexLut +
                                 (useShaperFunctionTexLut == true ? "PQ" : "Linear") +
                                 (useDisplayP3TexLut == true ? "DisplayP3" : "sRGB") +
                                 "MaxRange" + maxRadiometricValueTexLut.ToString();
        
        defaultCubeLutFileName = "CubeLut" + lutDimension.ToString() +
                                 (useShaperFunction == true ? "PQ" : "Linear") +
                                 (useDisplayP3 == true ? "DisplayP3" : "sRGB") +
                                 "MaxRange" + maxRadiometricValue.ToString();
        
    }

    public void OnDisable()
    {
        // Debug.Log("OnDisable being invoked");
    }

    private void OnValidate()
    {
        // Debug.Log("Values changing");
    }

    private void recalculateCurveParameters()
    {
        parametricCurve = colorGamut.getParametricCurve();
        tValues = colorGamut.getTValues();
        xValues = colorGamut.getXValues();
        yValues = colorGamut.getYValues();

        debugPoints.Clear();
        for (int i = 0; i < xValues.Count; i++)
        {
            debugPoints.Add(new Vector3(xValues[i], yValues[i]));
        }
    }
    
    void OnSceneGUI()
    {
        if (!hdrPipeline.isActiveAndEnabled)
            return;
        
        if (Application.isPlaying)
        {
            if (debugPoints == null || debugPoints.Count == 0)
                _curveGuiDataState = CurveGuiDataState.NotCalculated;
            
            if (colorGamut == null)
            {
                hdrPipeline = (HDRPipeline) target;
                colorGamut = hdrPipeline.getColorGamut();
            }
            
            controlPoints = colorGamut.getControlPoints();
            Vector2 p0 = controlPoints[0];
            Vector2 p1 = controlPoints[1];
            Vector2 p2 = controlPoints[2];
            Vector2 p3 = controlPoints[3];
            Vector2 p4 = controlPoints[4];
            Vector2 p5 = controlPoints[5];
            Vector2 p6 = controlPoints[6];

            Handles.DrawLine(new Vector3(0.0f, 0.0f), new Vector3(colorGamut.MaxRadiometricValue, 0.0f)); // Draw X Axis
            Handles.DrawLine(new Vector3(0.0f, 0.0f), new Vector3(0.0f, 5.0f)); // Draw Y axis
            // Draw auxiliary information on the graph
            Handles.DrawDottedLine(new Vector3(1.0f, 0.0f), new Vector3(1.0f, 5.0f), 4.0f); // Draw X = 1 line
            Handles.DrawDottedLine(new Vector3(0.0f, 1.0f), new Vector3(colorGamut.MaxRadiometricValue, 1.0f),
                4.0f); // Draw Y = 1 line
            Handles.DrawDottedLine(new Vector3(0.0f, 1.5f), new Vector3(colorGamut.MaxRadiometricValue, 1.5f),
            4.0f); // Draw Y = 1.5 line
            Handles.DrawDottedLine(new Vector3(0.18f, 0.0f), new Vector3(0.18f, 1.5f), 2.0f); // Draw vertical line from 0.18f
            Handles.DrawDottedLine(new Vector3(0.0f, 0.18f), new Vector3(0.18f, 0.18f), 2.0f); // Draw vertical line from 0.18f
            Handles.DrawDottedLine(new Vector3(0.5f, 0.0f), new Vector3(0.5f, 0.5f), 4.0f);
            Handles.DrawDottedLine(new Vector3(0.0f, 0.5f), new Vector3(0.5f, 0.5f), 4.0f); // Draw vertical line from 0.18f

            xTempValues = colorGamut.getXValues();
            yTempValues = colorGamut.getYValues();
            for (int i = 0; i < xTempValues.Count; i++)
            {
                Vector3 logPoint = new Vector3(xTempValues[i], yTempValues[i]);
                Vector3 linearPoint = new Vector3(Shaper.calculateLogToLinear(xTempValues[i], colorGamut.GreyPoint.x, colorGamut.MinRadiometricValue, colorGamut.MaxRadiometricValue),0.0f);
                Handles.DrawWireCube(logPoint, cubeWidgetSize);
                // Handles.DrawWireCube( linearPoint, cubeWidgetSize);
                // Handles.DrawDottedLine(linearPoint, logPoint, 1.0f);
            }

            if (_curveGuiDataState == CurveGuiDataState.MustRecalculate ||
                _curveGuiDataState == CurveGuiDataState.NotCalculated)
            {
                recalculateCurveParameters();
                _curveGuiDataState = CurveGuiDataState.Calculated;
            }

            Handles.DrawPolyLine(debugPoints.ToArray());
            // Handles.DrawWireCube(new Vector3(p1.x, p1.y), cubeWidgetSize);
            // Handles.DrawWireCube(new Vector3(p3.x, p3.y), cubeWidgetSize);
            // Handles.DrawWireCube(new Vector3(p5.x, p5.y), cubeWidgetSize);
        }
    }

    public override void OnInspectorGUI()
    {
        base.serializedObject.UpdateIfRequiredOrScript();
        base.serializedObject.Update();
        base.DrawDefaultInspector();

        if (colorGamut == null)
        {
            hdrPipeline = (HDRPipeline) target;
            colorGamut = hdrPipeline.getColorGamut();
            colorGradingHDR = hdrPipeline.getColorGrading();
        }

        if (!hdrPipeline.isActiveAndEnabled)
            return;
        
        if (hdriNames != null && hdriNames.Count > 0)
        {
            hdriIndex = EditorGUILayout.Popup("HDRI to use", hdriIndex, hdriNames.ToArray());
        }

        activeTransferFunction =
            (TransferFunction) EditorGUILayout.EnumPopup("Active Transfer Function", activeTransferFunction);
        EditorGUILayout.Space();
        exposure = EditorGUILayout.Slider("Exposure", exposure, colorGamut.MINExposureValue, colorGamut.MAXExposureValue);
        bleachingRatioPower = EditorGUILayout.IntSlider("Bleaching Ratio Power", bleachingRatioPower, 1, 7);
        showSweep = EditorGUILayout.Toggle("Enable Color Sweep", colorGamut.getShowSweep());
        enableBleaching = EditorGUILayout.Toggle("Enable Bleaching", enableBleaching);
        isMultiThreaded = EditorGUILayout.Toggle("Enable MultiThreading", isMultiThreaded);
        showPixelsOutOfGamut = EditorGUILayout.Toggle("Show Pixels Out of Gamut", showPixelsOutOfGamut);

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        colorGamut.getParametricCurveValues(out slope, out originPointX, out originPointY, out greyPointX,
            out greyPointY);
        slope = EditorGUILayout.Slider("Slope", slope, colorGamut.SlopeMin, colorGamut.SlopeMax);
        originPointX = EditorGUILayout.Slider("Origin X", originPointX, 0.0f, 1.0f);
        originPointY = EditorGUILayout.Slider("Origin Y", originPointY, 0.0f, 1.0f);
        greyPointX = EditorGUILayout.Slider("greyPointX", greyPointX, 0.0f, 1.0f);
        greyPointY = EditorGUILayout.Slider("greyPointY", greyPointY, 0.0f, 1.0f);
        
        if (GUILayout.Button("Export transfer function to Resolve"))
        {
            defaultCubeLutFileName = "CubeLut" + lutDimension.ToString();
            outPathCubeLut = EditorUtility.SaveFilePanel("Save .cube LUT file to...", "", defaultCubeLutFileName,"cube" );

            if (string.IsNullOrEmpty(outPathCubeLut))
            {
                Debug.LogError("File path to save cube Lut file is invalid");
                return;
            }

            colorGamut.exportTransferFunction(outPathCubeLut);
        }
        
        isColorGradingTabOpen = EditorGUILayout.InspectorTitlebar(isColorGradingTabOpen, hdrPipeline);
        if (isColorGradingTabOpen)
        {
            DrawSaveExrInspectorProps();
            DrawTexLutInspectorProps();
            DrawCubeInspectorProps();
        }

        if (GUI.changed)
        {
            _curveGuiDataState = CurveGuiDataState.MustRecalculate;
        }

        // Only write back values once we are in Play mode
        if (Application.isPlaying)
        {
            if (_curveGuiDataState == CurveGuiDataState.MustRecalculate ||
                _curveGuiDataState == CurveGuiDataState.NotCalculated)
            {
                // colorGamut.setHDRIIndex(hdriIndex);
                colorGamut.setShowSweep(showSweep, hdrPipeline.HDRIList[hdriIndex]);
                colorGamut.setBleaching(enableBleaching);
                colorGamut.setIsMultiThreaded(isMultiThreaded);
                colorGamut.setShowOutOfGamutPixels(showPixelsOutOfGamut);
                colorGamut.setExposure(exposure);
                colorGamut.setActiveTransferFunction(activeTransferFunction);
                colorGamut.setBleachingRatioPower(bleachingRatioPower);

                colorGamut.setParametricCurveValues(slope, originPointX, originPointY, greyPointX, greyPointY);
                _curveGuiDataState = CurveGuiDataState.Calculated;
            }
        }

        base.serializedObject.ApplyModifiedProperties();
    }
    
     private void DrawSaveExrInspectorProps()
    {
        EditorGUILayout.Space(10.0f);
        EditorGUILayout.LabelField("In-Game Capture ");
        
        shapeImage = EditorGUILayout.Toggle("Apply Shaper to exported capture", shapeImage);

        if (GUILayout.Button("Export Game Capture to Resolve"))
        {
            outPathGameCapture = EditorUtility.SaveFilePanel("In Game capture EXR...", "", "Capture", "exr");

            if (string.IsNullOrEmpty(outPathGameCapture))
            {
                Debug.LogError("File path to save game capture is invalid");
                return;
            }
         
            colorGradingHDR.saveInGameCapture(outPathGameCapture, shapeImage);
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
