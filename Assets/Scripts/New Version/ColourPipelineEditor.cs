using System;
using System.IO;
using System.Security;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.VersionControl;
using UnityEngine;

/// <summary>
/// Editor only class that exposes in the editor the functionality to tweak, export and save everything related with gamutmapping
/// and colour grading. This class is also responsible for drawing the editor curve function widget
/// </summary>
[CustomEditor(typeof(ColourPipeline))]
public class ColourPipelineEditor : UnityEditor.Editor
{
    ColourPipeline colourPipeline;
    private GamutMap colorGamut;

    #region Gamut Curve Editor Parameters
    private float slope;
    private float exposure = 0.0f;
    private float curveCoordMaxLatitude = 0.85f;
    private float curveChromaticityMaxLatitude = 0.85f;
    #endregion

    #region Gamut Curve Parameters
    private float originPointX;
    private float originPointY;
    private float greyPointX;
    private float greyPointY;
    #endregion

    #region LUT & Game Capture Member Variables
    private const int lutDimension = 33;
    private string outPathCubeLut = "";
    private string defaultCubeLutFileName;
    private string outPathGameCapture = "";
    #endregion

    private Vector4[] controlPointsUniform;
    private bool showCurve = false;

    #region Curve Editor Widget Variables
    private Rect curveRect;
    private RenderTexture curveRT;
    private Material curveDrawMaterial;
    private const string drawCurveShaderName = "Custom/DrawCurve";
    #endregion

    public void OnEnable()
    {
        colourPipeline = (ColourPipeline)target;
        if (!colourPipeline.isActiveAndEnabled)
        {
            return;
        }

        // Initialise parameters for the curve with sensible values
        colorGamut = colourPipeline.ColourGamut;
        colorGamut.GetParametricCurveValues(out slope, out originPointX, out originPointY, out greyPointX, out greyPointY);
        curveCoordMaxLatitude = colorGamut.CurveCoordMaxLatitude;
        curveChromaticityMaxLatitude = colorGamut.ChromaticityMaxLatitude;

        curveRT = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGB32);
        Shader curveShader = Shader.Find(drawCurveShaderName);
        curveDrawMaterial = new Material(curveShader);
        controlPointsUniform = new Vector4[7];

    }

    public override void OnInspectorGUI()
    {
        base.serializedObject.UpdateIfRequiredOrScript();
        base.serializedObject.Update();
        base.DrawDefaultInspector();

        colourPipeline = (ColourPipeline)target;
        colorGamut = colourPipeline.ColourGamut;

        if (!colourPipeline.isActiveAndEnabled)
            return;

        EditorGUILayout.Space();

        // Draw all the debugging widgets
        DrawTransferFunctionExport();
        DrawSaveGameCaptureWidgets();
        DrawSaveBakeLutToDiskWidgets();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(ColourPipelineEditorUIStrings.advCurveOptionsLabel);
        showCurve = EditorGUILayout.Foldout(showCurve, ColourPipelineEditorUIStrings.aestheticTransferCurveFoldout);
        colourPipeline.IsCurveEditingEnabled = showCurve;
        if (showCurve)
        {
            slope = EditorGUILayout.Slider(ColourPipelineEditorUIStrings.curveSlopeSlider, slope, colorGamut.SlopeMin, colorGamut.SlopeMax);
            DrawGamutMapCurveWidget();
        }
        EditorGUILayout.Space();
        EditorGUILayout.Space();

        if (Application.isPlaying)
        {
            if (GUI.changed)
            {
                RecalculateCurveParameters();
            }
        }
        base.serializedObject.ApplyModifiedProperties();
    }

    private void DrawTransferFunctionExport()
    {
        EditorGUILayout.Space(10.0f);
        EditorGUILayout.LabelField(ColourPipelineEditorUIStrings.gamutMapFunctionExportLabel);
        if (GUILayout.Button(ColourPipelineEditorUIStrings.exportGamutMapFuncButton))
        {
            defaultCubeLutFileName = "CubeLut" + lutDimension.ToString();
            outPathCubeLut = EditorUtility.SaveFilePanel(ColourPipelineEditorUIStrings.saveCubeLUTSaveFilePanel, "",
                defaultCubeLutFileName, ColourPipelineEditorUIStrings.cubeFileFormat);

            if (string.IsNullOrEmpty(outPathCubeLut))
            {
                Debug.Log( "File path to save cube Lut file is invalid");
                return;
            }

            ExportTransferFunction(outPathCubeLut);
        }
    }

    private void DrawSaveBakeLutToDiskWidgets()
    {
        EditorGUILayout.LabelField(ColourPipelineEditorUIStrings.saveBakedLutLabel);
        if (GUILayout.Button(ColourPipelineEditorUIStrings.saveBakedLutButton))
        {
            outPathCubeLut = EditorUtility.SaveFilePanel(ColourPipelineEditorUIStrings.saveBakeLUTSaveFilePanel, "Assets/", "BakedLUT", "asset");

            if (string.IsNullOrEmpty(outPathCubeLut))
            {
                Debug.Log("File path to save cube Lut file is invalid");
                return;
            }
            // We must sanitise the path to become relative to "Assets/..."
            string currentDirectory = Directory.GetCurrentDirectory();
            string relativeOutPathCubeLut = GetRelativePath(currentDirectory, outPathCubeLut);

            Texture3D bakedLut = BakeLUT();
            postProcess.ColourPipelineEffect.BakedColourPipelineLUT = bakedLut;
            LutBaker.SaveLutToDisk(bakedLut, relativeOutPathCubeLut);
        }
    }

    private Texture3D BakeLUT()
    {
        LutBaker lutBaker = new LutBaker(colourPipeline);
        return lutBaker.BakeLut(postProcess.ColourPipelineEffect.DynColourPipelineLUT, 33);
    }

    private void RecalculateCurveParameters()
    {
        colorGamut.ChromaticityMaxLatitude = curveChromaticityMaxLatitude;
        CurveParams curveParams = new CurveParams(exposure, slope, originPointX,
            originPointY, curveCoordMaxLatitude,
            curveChromaticityMaxLatitude);
        colorGamut.SetCurveParams(curveParams);
    }

    private void DrawGamutMapCurveWidget()
    {
        Vector2[] controlPoints = colorGamut.GetControlPoints();
        for (int i = 0; i < 7; i++)
        {
            controlPointsUniform[i] = new Vector4(controlPoints[i].x, controlPoints[i].y);
        }

        var oldRt = RenderTexture.active;
        curveDrawMaterial.SetVectorArray("controlPoints", controlPointsUniform);
        Graphics.Blit(null, curveRT, curveDrawMaterial);
        RenderTexture.active = oldRt;

        curveRect = GUILayoutUtility.GetRect(128, 256);

        if (curveRT != null && curveRT.IsCreated() &&
            colorGamut != null && colorGamut.XCameraIntrinsicValues != null && colorGamut.YDisplayIntrinsicValues != null)
        {
            GUI.DrawTexture(curveRect, curveRT);
        }

        Handles.DrawSolidRectangleWithOutline(curveRect, Color.clear, Color.white * 0.4f);
    }

    private void DrawSaveGameCaptureWidgets()
    {
        EditorGUILayout.LabelField(ColourPipelineEditorUIStrings.gameCaptureExportLabel);
        if (GUILayout.Button(ColourPipelineEditorUIStrings.gameCaptureExportButton))
        {
            outPathGameCapture = EditorUtility.SaveFilePanel(ColourPipelineEditorUIStrings.saveGameCaptureSaveFilePanel, "", "Capture", "exr");

            if (string.IsNullOrEmpty(outPathGameCapture))
            {
               Debug.Log("File path to save game capture is invalid");
                return;
            }

            SaveInGameCapture(outPathGameCapture);
        }
        else if (outPathGameCapture.Length < 1)
        {
            outPathGameCapture = Application.dataPath;
        }

    }

    public void ExportTransferFunction(string filePath)
    {
        Vector3 minCameraNativeVec = new Vector3(colorGamut.XCameraIntrinsicValues[0], colorGamut.XCameraIntrinsicValues[0],
            colorGamut.XCameraIntrinsicValues[0]);
        Vector3 maxCameraNativeVec = new Vector3(colorGamut.XCameraIntrinsicValues[colorGamut.XCameraIntrinsicValues.Count - 1],
            colorGamut.XCameraIntrinsicValues[colorGamut.XCameraIntrinsicValues.Count - 1],
            colorGamut.XCameraIntrinsicValues[colorGamut.XCameraIntrinsicValues.Count - 1]);

        Color[] identity3DLut = LutBaker.GenerateIdentityCubeLUT(lutDimension);
        colorGamut.CalculateTransferTransform(ref identity3DLut);

        CubeLutExporter.SaveLutAsCube(identity3DLut, filePath, lutDimension, minCameraNativeVec, maxCameraNativeVec,
            true);
    }

    public void SaveToDisk(Color[] pixels, string fileName, int width, int height)
    {
        Debug.Log("Preparing to save texture file to disk");

        Texture2D textureToSave = new Texture2D(width, height, TextureFormat.RGBAHalf, false, true);
        textureToSave.SetPixels(pixels);
        textureToSave.Apply();
        try
        {
            if (File.Exists(fileName))
            {
                UnityEditor.VersionControl.Provider.Checkout(fileName, CheckoutMode.Both);
            }
            File.WriteAllBytes(@fileName, textureToSave.EncodeToEXR());
            Debug.Log("Texture " + fileName + " successfully saved to disk");
        }
        catch (UnauthorizedAccessException excp)
        {
            Debug.Log(excp.ToString());
            Debug.Log("Error while saving the file. Make sure that you're not overriding an " +
                                                          "existing file or try saving the file in a different directory");
        }
        catch (SecurityException excp)
        {
            Debug.Log(excp.ToString());
            Debug.Log("Permission denied to save the file in this folder. Please choose a different location");
        }
    }

    public void SaveInGameCapture(string saveFilePath)
    {
        int width = 2048;
        int height = 1024;

        int captureFramerate = 30;

        Shader.DisableKeyword("POST_RUNTIME_COLOUR_PIPELINE");
        Shader.DisableKeyword("POST_BAKED_COLOUR_PIPELINE");
        Shader.EnableKeyword("HDR_OUTPUT");

        RenderTexture c = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf,
            RenderTextureReadWrite.Linear);
        RenderTexture r = RenderTexture.active;
        postProcess.GetCamera().targetTexture = c;
        postProcess.GetCamera().Render();

        if (postProcess.ColourPipelineEffect.ActiveColourPipeline == ActiveColourPipeline.DynamicPipeline)
        {
            Shader.EnableKeyword("POST_RUNTIME_COLOUR_PIPELINE");
        }
        else
        {
            Shader.EnableKeyword("POST_BAKED_COLOUR_PIPELINE");
        }

        postProcess.GetCamera().targetTexture = null;
        RenderTexture.active = c;
        Texture2D FrameCaptureTexture = new Texture2D(width, height, TextureFormat.RGBAHalf, false, true);
        FrameCaptureTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        FrameCaptureTexture.Apply();
        Color[] inGameCapturePixels = FrameCaptureTexture.GetPixels();

        Color[] outputColorBuffer = colorGamut.ApplyChromaticityCompressionCPU(inGameCapturePixels);
        SaveToDisk(outputColorBuffer, saveFilePath, FrameCaptureTexture.width, FrameCaptureTexture.height);
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

public static class ColourPipelineEditorUIStrings
{
    public const string advCurveOptionsLabel = "Advanced Curve Options";
    public const string aestheticTransferCurveFoldout = "Aesthetic Transfer Curve";
    public const string curveSlopeSlider = "Curve Slope";

    public const string gamutMapFunctionExportLabel = "Gamut Mapping Function Export";
    public const string exportGamutMapFuncButton = "Export Gamut Mapping Function to Resolve";

    public const string saveCubeLUTSaveFilePanel = "Save .cube LUT file to...";
    public const string cubeFileFormat = "cube";

    public const string saveBakedLutLabel = "Save Baked LUT to Disk";
    public const string saveBakedLutButton = "Save Baked LUT";
    public const string saveBakeLUTSaveFilePanel = "Save 3D LUT file to...";
    public const string gameCaptureExportLabel = "In-Game Capture Export";
    public const string gameCaptureExportButton = "Export Game Capture to Resolve";
    public const string saveGameCaptureSaveFilePanel = "In Game capture EXR...";
}


