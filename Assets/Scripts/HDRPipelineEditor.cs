using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(HDRPipeline))]
public class HDRPipelineEditor : Editor
{
    HDRPipeline hdrPipeline;
    private GamutMap colorGamut;
    private float exposure = 0.0f;
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
    // private bool foldoutStateSaveExr = false;
    private string outPathGameCapture = "";
    #endregion

    private bool isColorGradingTabOpen = true;
    // private ColorGrade colorGradingHDR;
    private GamutMap.CurveDataState guiWidgetsState = GamutMap.CurveDataState.NotCalculated;
    
    //#region DebugOptions
    private bool enableCPUMode = true;
    private bool saveGamutMapDebugImages = false;
    private bool heatmapToggle = false;
    //#endregion

    private Rect curveRect;
    private float scaleFactor = 1.0f;
    private float curveCoordMaxLowerBoundLatitude = 0.85f;
    private float curveChromaticityMaxLowerBoundLatitude = 0.85f;
    private float gamutCompressionRatioPowerLowerBound = 2.0f;

    //private float curveCoordMaxHigherBoundLatitude = 0.85f;
    private float curveChromaticityMaxHigherBoundLatitude = 0.85f;
    //private float gamutCompressionRatioPowerHigherBound = 2.0f;

    private float minRadiometricExposure = -6.0f;
    private float maxRadiometricExposure = 6.0f;

    public void OnEnable()
    {
        hdrPipeline = (HDRPipeline) target;
        if (!hdrPipeline.isActiveAndEnabled)
            return;
        
        colorGamut = hdrPipeline.getGamutMap();
        // colorGradingHDR = hdrPipeline.getColorGrading();
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
        if (guiWidgetsState ==  GamutMap.CurveDataState.NotCalculated)
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
        
        enableCPUMode = hdrPipeline.CPUMode;
        curveCoordMaxLowerBoundLatitude = colorGamut.CurveCoordMaxLowerBoundLatitude;
        curveChromaticityMaxLowerBoundLatitude = colorGamut.ChromaticityMaxLowerBoundLatitude;
        gamutCompressionRatioPowerLowerBound = colorGamut.GamutCompressionRatioPowerLowerBound;

        //curveCoordMaxHigherBoundLatitude = colorGamut.CurveCoordMaxHigherBoundLatitude;
        //curveChromaticityMaxHigherBoundLatitude = colorGamut.ChromaticityMaxHigherBoundLatitude;
        //gamutCompressionRatioPowerHigherBound = colorGamut.GamutCompressionRatioPowerHigherBound;

        minRadiometricExposure = colorGamut.MinRadiometricExposure;
        maxRadiometricExposure = colorGamut.MaxRadiometricExposure;
        heatmapToggle = hdrPipeline.heatMapToggle;
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
                guiWidgetsState =  GamutMap.CurveDataState.NotCalculated;
            
            if (colorGamut == null)
            {
                hdrPipeline = (HDRPipeline) target;
                colorGamut = hdrPipeline.getGamutMap();
            }
            
            controlPoints = colorGamut.getControlPoints();
            Vector2 p0 = controlPoints[0];
            Vector2 p1 = controlPoints[1];
            Vector2 p2 = controlPoints[2];
            Vector2 p3 = controlPoints[3];
            Vector2 p4 = controlPoints[4];
            Vector2 p5 = controlPoints[5];
            Vector2 p6 = controlPoints[6];

            Handles.DrawLine(new Vector3(0.0f, 0.0f), new Vector3(colorGamut.MaxRadiometricDynamicRange, 0.0f)); // Draw X Axis
            Handles.DrawLine(new Vector3(0.0f, 0.0f), new Vector3(0.0f, 5.0f)); // Draw Y axis
            // Draw auxiliary information on the graph
            Handles.DrawDottedLine(new Vector3(1.0f, 0.0f), new Vector3(1.0f, 5.0f), 4.0f); // Draw X = 1 line
            Handles.DrawDottedLine(new Vector3(0.0f, 1.0f), new Vector3(colorGamut.MaxRadiometricDynamicRange, 1.0f), 4.0f); // Draw Y = 1 line
            //Handles.DrawDottedLine(new Vector3(0.0f, 1.5f), new Vector3(colorGamut.MaxRadiometricValue, 1.5f), 4.0f); // Draw Y = 1.5 line
            Handles.DrawDottedLine(new Vector3(p4.x, 0.0f), new Vector3(p4.x, p4.y), 2.0f); // Draw vertical line from 0.18f
            Handles.DrawDottedLine(new Vector3(0.0f, p4.y), new Vector3(p4.x, p4.y), 2.0f); // Draw vertical line from 0.18f
            //Handles.DrawDottedLine(new Vector3(0.5f, 0.0f), new Vector3(0.5f, 0.5f), 4.0f);
            Handles.DrawDottedLine(new Vector3(curveCoordMaxLowerBoundLatitude, 0.0f), new Vector3(curveCoordMaxLowerBoundLatitude, 1.0f), 4.0f); // Draw vertical line for gamut compression start

            //Handles.DrawDottedLine(new Vector3(curveChromaticityMaxLowerBoundLatitude, 0.0f), new Vector3(curveChromaticityMaxLowerBoundLatitude, 1.0f), 8.0f); // Draw vertical line for gamut compression start
            //Handles.DrawDottedLine(new Vector3(curveChromaticityMaxHigherBoundLatitude, 0.0f), new Vector3(curveChromaticityMaxHigherBoundLatitude, 1.0f), 8.0f); // Draw vertical line for gamut compression start



            if (guiWidgetsState ==  GamutMap.CurveDataState.Dirty ||
                guiWidgetsState ==  GamutMap.CurveDataState.NotCalculated)
            {
                recalculateCurveParameters();
                guiWidgetsState =  GamutMap.CurveDataState.Calculated;
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
        colorGamut = hdrPipeline.getGamutMap();
        // colorGradingHDR = hdrPipeline.getColorGrading();
        
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
        
        exposure = EditorGUILayout.Slider("Exposure Value (EV)", exposure, colorGamut.MinRadiometricExposure, colorGamut.MaxRadiometricExposure);
        slope = EditorGUILayout.Slider("Slope", slope, colorGamut.SlopeMin, colorGamut.SlopeMax);
        curveCoordMaxLowerBoundLatitude = EditorGUILayout.Slider("Curve Max Lower Bound Coordinate Latitude", curveCoordMaxLowerBoundLatitude, 0.1f, 1.0f);
        curveChromaticityMaxLowerBoundLatitude = EditorGUILayout.Slider("Curve Max Lower Bound Chromaticity Latitude", curveChromaticityMaxLowerBoundLatitude, 0.1f, 1.0f);
        //curveChromaticityMaxHigherBoundLatitude = EditorGUILayout.Slider("Curve Max Higher Bound Chromaticity Latitude",
        //    ((curveChromaticityMaxHigherBoundLatitude <= curveChromaticityMaxLowerBoundLatitude) ? (curveChromaticityMaxLowerBoundLatitude + 0.01f) : curveChromaticityMaxHigherBoundLatitude), 0.1f, 1.0f);

        gamutCompressionRatioPowerLowerBound = EditorGUILayout.Slider("Gamut Compression Lower Bound Exponent", gamutCompressionRatioPowerLowerBound, 0.001f, 5.0f);
        //gamutCompressionRatioPowerHigherBound = EditorGUILayout.Slider("Gamut Compression Higher Bound Exponent", gamutCompressionRatioPowerHigherBound, 0.001f, 5.0f);
        
        minRadiometricExposure = EditorGUILayout.Slider("Minimum Radiometric Exposure", minRadiometricExposure, -20.0f, 0.0f);
        maxRadiometricExposure = EditorGUILayout.Slider("Maximum Radiometric Exposure", maxRadiometricExposure, 0.0f, 20.0f);

        EditorGUILayout.Space();

        if (Application.isPlaying)
        {
            if (GUI.changed)
            {
                guiWidgetsState =  GamutMap.CurveDataState.Dirty;
            }

            if (enableCPUMode == true && guiWidgetsState == GamutMap.CurveDataState.Dirty && 
                GUILayout.Button("Generate Image"))
            {
                Debug.Log("Generating new image with new parameters");
                colorGamut.setExposure(exposure);
                colorGamut.setGamutCompression(isGamutCompressionActive);
                colorGamut.setMinMaxRadiometricExposure(minRadiometricExposure, maxRadiometricExposure);
                colorGamut.setGamutCompressionRatioPower(gamutCompressionRatioPowerLowerBound);
                RecalculateImageInCpuMode();
            } else if (enableCPUMode == false && guiWidgetsState == GamutMap.CurveDataState.Dirty
               /* && GUILayout.Button("Recalculate Curve Parameters")*/)
            {
                colorGamut.setActiveTransferFunction(_activeGamutMappingMode);
                colorGamut.setExposure(exposure);
                colorGamut.setGamutCompression(isGamutCompressionActive);
                colorGamut.setMinMaxRadiometricExposure(minRadiometricExposure, maxRadiometricExposure);
                colorGamut.setGamutCompressionRatioPower(gamutCompressionRatioPowerLowerBound);
                RecalculateCurveParameters();

                guiWidgetsState = GamutMap.CurveDataState.Calculated;

            }
        }

        DrawGamutCurveWidget();

        
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

        EditorGUILayout.LabelField("Save Baked LUT ");
        if (GUILayout.Button("Save baked LUT to disk"))
        {
            outPathCubeLut = EditorUtility.SaveFilePanel("Save 3D LUT LUT file to...", "", "BakedLUT","asset" );

            if (string.IsNullOrEmpty(outPathCubeLut))
            {
                Debug.LogError("File path to save cube Lut file is invalid");
                return;
            }

            // We must sanitise the path to become relative to "Assets/..."
            string currentDirectory = Directory.GetCurrentDirectory();
            string relativeOutPathCubeLut = GetRelativePath(currentDirectory, outPathCubeLut);
            LutBaker lutbaker = new LutBaker(hdrPipeline);
            Texture3D runtimeBakedLUT = lutbaker.BakeLUT(33);
            hdrPipeline.bakedLUT = runtimeBakedLUT;

            LutBaker.SaveLutToDisk(runtimeBakedLUT, relativeOutPathCubeLut);
        }

        EditorGUILayout.Separator();
        EditorGUILayout.LabelField("Debug Options");
        EditorGUILayout.Space();

        DrawDebugOptionsWidgets();
        hdrPipeline.heatMapToggle = heatmapToggle;

        base.serializedObject.ApplyModifiedProperties();
    }

    private void DrawGamutCurveWidget()
    {
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
        colorGamut.setChromaticityMaxLatitude(curveCoordMaxLowerBoundLatitude);
        CurveParams curveParams = new CurveParams(exposure, slope, originPointX,
         originPointY, _activeGamutMappingMode, isGamutCompressionActive, curveCoordMaxLowerBoundLatitude, curveChromaticityMaxLowerBoundLatitude);
        colorGamut.setCurveParams(curveParams);
    }

    private void RecalculateImageInCpuMode()
    {
        colorGamut.setChromaticityMaxLatitude(curveCoordMaxLowerBoundLatitude);
        CurveParams curveParams = new CurveParams(exposure, slope, originPointX,
            originPointY, _activeGamutMappingMode, isGamutCompressionActive, curveCoordMaxLowerBoundLatitude, curveChromaticityMaxLowerBoundLatitude);
        colorGamut.setCurveParams(curveParams);
        //hdrPipeline.ApplyGamutMap();
        hdrPipeline.getGamutMap().CurveState = GamutMap.CurveDataState.NotCalculated;
        guiWidgetsState = GamutMap.CurveDataState.Calculating;
    }

    private void DrawDebugOptionsWidgets()
    {
        heatmapToggle = EditorGUILayout.Toggle("Pixel Heatmap", heatmapToggle);
        showSweep = EditorGUILayout.Toggle("Enable Color Sweep", colorGamut.getShowSweep());
        isGamutCompressionActive = EditorGUILayout.Toggle("Enable Gamut Compression", isGamutCompressionActive);
        isMultiThreaded = EditorGUILayout.Toggle("Enable MultiThreading", isMultiThreaded);
        showPixelsOutOfGamut = EditorGUILayout.Toggle("Show Pixels Out of Gamut", showPixelsOutOfGamut);
    }

    private void DrawSaveGameCaptureWidgets()
    {
        EditorGUILayout.Space(10.0f);
        EditorGUILayout.LabelField("Export of In-Game Capture ");

        if (GUILayout.Button("Export Game Capture to Resolve"))
        {
            outPathGameCapture = EditorUtility.SaveFilePanel("In Game capture EXR...", "", "Capture", "exr");

            if (string.IsNullOrEmpty(outPathGameCapture))
            {
                Debug.LogError("File path to save game capture is invalid");
                return;
            }
         
            colorGamut.saveInGameCapture(outPathGameCapture);
        }
        else if (outPathGameCapture.Length < 1)
        {
            outPathGameCapture = Application.dataPath;
        }

        outPathGameCapture = EditorGUILayout.TextField("Save to", outPathGameCapture);
    }


      /// <summary>
        /// Creates a relative path from one file or folder to another.
        /// Solution from https://stackoverflow.com/questions/275689/how-to-get-relative-path-from-absolute-path because we're
        /// still using .Net Standard 2.0 and the .Net API Path.GetRelativePath(...) is not available
        /// </summary>
        /// <param name="fromPath">Contains the directory that defines the start of the relative path.</param>
        /// <param name="toPath">Contains the path that defines the endpoint of the relative path.</param>
        /// <returns>The relative path from the start directory to the end path.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="fromPath"/> or <paramref name="toPath"/> is <c>null</c>.</exception>
        /// <exception cref="UriFormatException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public static string GetRelativePath(string fromPath, string toPath)
        {
            if (string.IsNullOrEmpty(fromPath))
            {
                throw new ArgumentNullException("fromPath");
            }

            if (string.IsNullOrEmpty(toPath))
            {
                throw new ArgumentNullException("toPath");
            }

            Uri fromUri = new Uri(AppendDirectorySeparatorChar(fromPath));
            Uri toUri = new Uri(AppendDirectorySeparatorChar(toPath));

            if (fromUri.Scheme != toUri.Scheme)
            {
                return toPath;
            }

            Uri relativeUri = fromUri.MakeRelativeUri(toUri);
            string relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            if (string.Equals(toUri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
            {
                relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }

            return relativePath;
        }

        private static string AppendDirectorySeparatorChar(string path)
        {
            // Append a slash only if the path is a directory and does not have a slash.
            if (!Path.HasExtension(path) &&
                !path.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                return path + Path.DirectorySeparatorChar;
            }

            return path;
        }
    
}
