using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


public struct CurveParams
{
    public bool isGamutCompressionActive;
    public float exposure;
    public float slope;
    public float originX;
    public float originY;
    public GamutMappingMode ActiveGamutMappingMode;

    public CurveParams(float inExposure, float inSlope, float inOriginX,
        float inOriginY, GamutMappingMode inActiveGamutMappingMode, bool inIsGamutCompressionActive)
    {
        exposure = inExposure;
        slope = inSlope;
        originX = inOriginX;
        originY = inOriginY;
        ActiveGamutMappingMode = inActiveGamutMappingMode;
        isGamutCompressionActive = inIsGamutCompressionActive;

    }
}

[CustomEditor(typeof(HDRPipeline))]
public class HDRPipelineEditor : Editor
{
    HDRPipeline hdrPipeline;
    private GamutMapping colorGamut;
    private float exposure = 0.0f;
    private int gamutCompressionRatioPower = 2;
    private GamutMappingMode _activeGamutMappingMode = GamutMappingMode.Max_RGB;

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
    private bool isGamutCompressionActive = true;
    private bool isMultiThreaded = false;
    private bool showPixelsOutOfGamut = false;

    private Vector2[] controlPoints;
    private GamutCurve _parametricGamutCurve;

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
    private ColorGrading colorGradingHDR;
    private bool shapeImage = true;
    private GamutMapping.CurveDataState guiWidgetsState = GamutMapping.CurveDataState.NotCalculated;
    
    #region DebugOptions
    private bool enableCPUMode = false;
    private bool saveGamutMapDebugImages = false;
    #endregion

    private Rect curveRect;
    private float scaleFactor = 1.0f; 

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

            _activeGamutMappingMode = colorGamut.ActiveGamutMappingMode;
            // colorGamut.setInputTexture(hdrPipeline.HDRIList[hdriIndex]);
        }
        
        // Initialise parameters for the curve with sensible values
        if (guiWidgetsState ==  GamutMapping.CurveDataState.NotCalculated)
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
        
        hdrPipeline.CPUMode = enableCPUMode;

    }

    public void OnDisable()
    {
        
    }

    private void OnValidate()
    {
        // Debug.Log("Values changing");
    }

    private void recalculateCurveParameters()
    {
        _parametricGamutCurve = colorGamut.getParametricCurve();
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
                guiWidgetsState =  GamutMapping.CurveDataState.NotCalculated;
            
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
            
            if (guiWidgetsState ==  GamutMapping.CurveDataState.Dirty ||
                guiWidgetsState ==  GamutMapping.CurveDataState.NotCalculated)
            {
                recalculateCurveParameters();
                guiWidgetsState =  GamutMapping.CurveDataState.Calculated;
            }

            Handles.DrawPolyLine(debugPoints.ToArray());
            Handles.DrawWireCube(new Vector3(p1.x, p1.y), cubeWidgetSize);
            Handles.DrawWireCube(new Vector3(p3.x, p3.y), cubeWidgetSize);
            Handles.DrawWireCube(new Vector3(p5.x, p5.y), cubeWidgetSize);
        }
    }

    public override void OnInspectorGUI()
    {
        base.serializedObject.UpdateIfRequiredOrScript();
        base.serializedObject.Update();
        base.DrawDefaultInspector();
    
        hdrPipeline = (HDRPipeline) target;
        colorGamut = hdrPipeline.getColorGamut();
        colorGradingHDR = hdrPipeline.getColorGrading();
        
        if (!hdrPipeline.isActiveAndEnabled)
            return;
        
        if (hdriNames != null && hdriNames.Count > 0)
        {
            hdriIndex = EditorGUILayout.Popup("HDRI to use", hdriIndex, hdriNames.ToArray());
        }

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        _activeGamutMappingMode =
            (GamutMappingMode) EditorGUILayout.EnumPopup("Active Transfer Function", _activeGamutMappingMode);
        EditorGUILayout.Space();
        // gamutCompressionRatioPower = EditorGUILayout.IntSlider("Bleaching Ratio Power", gamutCompressionRatioPower, 1, 7);
        
        exposure = EditorGUILayout.Slider("Exposure Value (EV)", exposure, colorGamut.MINExposureValue, colorGamut.MAXExposureValue);
        slope = EditorGUILayout.Slider("Slope", slope, colorGamut.SlopeMin, colorGamut.SlopeMax);
        originPointX = EditorGUILayout.Slider("Origin X", originPointX, 0.0f, 1.0f);
        originPointY = EditorGUILayout.Slider("Origin Y", originPointY, 0.0f, 1.0f);
        greyPointX = EditorGUILayout.Slider("greyPointX", greyPointX, 0.0f, 1.0f);
        greyPointY = EditorGUILayout.Slider("greyPointY", greyPointY, 0.0f, 1.0f);
        EditorGUILayout.Space();

        if (Application.isPlaying)
        {
            if (GUI.changed)
            {
                Debug.Log("GUI Changed");
                guiWidgetsState =  GamutMapping.CurveDataState.Dirty;
            }

            if (enableCPUMode == true && guiWidgetsState == GamutMapping.CurveDataState.Dirty && 
                GUILayout.Button("Generate Image"))
            {
                Debug.Log("Generating new image with new parameters");
                RecalculateImageInCpuMode();
            } else if (enableCPUMode == false && guiWidgetsState == GamutMapping.CurveDataState.Dirty 
                && GUILayout.Button("Recalculate Curve Parameters"))
            {
                RecalculateCurveParameters();
                colorGamut.setActiveTransferFunction(_activeGamutMappingMode);
                colorGamut.setExposure(exposure);
                guiWidgetsState = GamutMapping.CurveDataState.Calculated;

            }
        }

        
        EditorGUILayout.LabelField("Transfer Function Export ");
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
        
        DrawSaveGameCaptureWidgets();
        // DrawGradingLUTWidgets();
        // DrawCubeLUTWidgets();

        DrawGamutMapCurveWidget();
        
        EditorGUILayout.Separator();
        EditorGUILayout.LabelField("Debug Options");
        EditorGUILayout.Space();

        DrawDebugOptionsWidgets();

        base.serializedObject.ApplyModifiedProperties();
    }

    private void DrawGamutMapCurveWidget()
    {
        hdrPipeline.ScaleFactor = EditorGUILayout.Slider("Curve Scale Factor", hdrPipeline.ScaleFactor, 0.0f, 10.0f);
        curveRect = GUILayoutUtility.GetRect(128, 256);

        if (hdrPipeline.CurveRT != null && hdrPipeline.CurveRT.IsCreated() && 
            colorGamut != null && colorGamut.getXValues() != null && colorGamut.getYValues() != null)
        {
            GUI.DrawTexture(curveRect, hdrPipeline.CurveRT);
        }
        
        Handles.DrawSolidRectangleWithOutline(curveRect, Color.clear, Color.white * 0.4f);
    }

    private void RecalculateCurveParameters() 
    {
        CurveParams curveParams = new CurveParams(exposure, slope, originPointX,
         originPointY, _activeGamutMappingMode, isGamutCompressionActive);
        colorGamut.setCurveParams(curveParams);
    }

    private void RecalculateImageInCpuMode()
    {
        CurveParams curveParams = new CurveParams(exposure, slope, originPointX,
            originPointY, _activeGamutMappingMode, isGamutCompressionActive);
        colorGamut.setCurveParams(curveParams);
        hdrPipeline.ApplyGamutMap();
        guiWidgetsState = GamutMapping.CurveDataState.Calculating;
    }

    private void DrawDebugOptionsWidgets()
    {
        showSweep = EditorGUILayout.Toggle("Enable Color Sweep", colorGamut.getShowSweep());
        isGamutCompressionActive = EditorGUILayout.Toggle("Enable Gamut Compression", isGamutCompressionActive);
        isMultiThreaded = EditorGUILayout.Toggle("Enable MultiThreading", isMultiThreaded);
        showPixelsOutOfGamut = EditorGUILayout.Toggle("Show Pixels Out of Gamut", showPixelsOutOfGamut);
        // @TODO Needs to be properly rewritten
        // if (EditorGUILayout.Toggle("Enable CPU mode", enableCPUMode))
        // {
        //     enableCPUMode = true;
        //     if (enableCPUMode != hdrPipeline.CPUMode)
        //     {
        //         RecalculateImageInCpuMode();
        //     }
        //     hdrPipeline.CPUMode = enableCPUMode;
        // }
        // else
        // {
        //     enableCPUMode = false;
        // }

        // saveGamutMapDebugImages = EditorGUILayout.Toggle("Save gamut mapping debug images to disk", saveGamutMapDebugImages);
    }

    private void DrawSaveGameCaptureWidgets()
    {
        EditorGUILayout.Space(10.0f);
        EditorGUILayout.LabelField("Export of In-Game Capture ");
        
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

    private void DrawGradingLUTWidgets()
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

    private void DrawCubeLUTWidgets()
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
