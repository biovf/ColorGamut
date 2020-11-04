using System;
using MathNet.Numerics;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(ColorGamutShaped))]
public class ColorGamutShapedEditor : Editor
{
    ColorGamutShaped colorGamut;

    private float exposure = 0;
    private int bleachingRatioPower = 2;
    private TransferFunction activeTransferFunction = TransferFunction.Max_RGB;

    #region Parametric Curve Parameters

    private float slope;
    private float originPointX;
    private float originPointY;
    private float greyPointX;
    private float greyPointY;

    #endregion

    private List<Texture2D> HDRis;
    private List<string> hdriNames;

    private int hdriIndex = 0;

    private bool showSweep = false;
    private bool enableBleaching = true;
    private bool isMultiThreaded = false;
    private bool showPixelsOutOfGamut = false;

    AnimationCurve animationCurve;
    private Vector2[] controlPoints;
    private Vector2[] controlPointsLinear; 

    private CurveTest parametricCurve;
    private CurveTest parametricCurveLinear;

    private List<float> tValues;
    private List<float> xValues;
    private List<float> yValues;
    private List<float> tValuesLinear;
    private List<float> xValuesLinear;
    private List<float> yValuesLinear;
    
    private List<Vector3> debugPoints = new List<Vector3>();
    private List<Vector3> debugPointsLinear = new List<Vector3>();

    private Vector3 cubeWidgetSize = new Vector3(0.01f, 0.01f, 0.01f);

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
        colorGamut = (ColorGamutShaped) target;
        hdriNames = new List<string>();
        if (colorGamut.HDRIList != null)
        {
            HDRis = colorGamut.HDRIList;
            for (int i = 0; i < HDRis.Count; i++)
            {
                hdriNames.Add(new string(HDRis[i].ToString().ToCharArray()));
            }
        }

        // Initialise parameters for the curve with sensible values
        if (_curveGuiDataState == CurveGuiDataState.NotCalculated)
            colorGamut.getParametricCurveValues(out slope, out originPointX, out originPointY, out greyPointX,
                out greyPointY);
        
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
        
        // Linear curve
        parametricCurveLinear = new CurveTest(colorGamut.MINExposureValue, colorGamut.MAXExposureValue, 
            colorGamut.MaxRadiometricValue, colorGamut.MAXDisplayValue );
        controlPointsLinear = parametricCurveLinear.createControlPointsinLinear(
            new Vector2(colorGamut.MinRadiometricValue, 0.00001f), new Vector2(0.18f, 0.18f), 2.0f);
        xValuesLinear = initialiseXCoordsInRange(1024, colorGamut.MaxRadiometricValue);
        tValuesLinear = parametricCurveLinear.calcTfromXquadratic(xValuesLinear.ToArray(), controlPointsLinear);
        yValuesLinear =
            parametricCurveLinear.calcYfromXQuadratic(xValuesLinear, tValuesLinear, new List<Vector2>(controlPointsLinear));
        
        debugPointsLinear.Clear();
        for (int i = 0; i < xValuesLinear.Count; i++)
        {
            debugPointsLinear.Add(new Vector3(Shaper.calculateLinearToLog2(xValuesLinear[i], 0.18f, colorGamut.MINExposureValue, colorGamut.MAXExposureValue), 
                Mathf.Log10(yValuesLinear[i] + 1.0f)));
        }
    }

    private List<float> initialiseXCoordsInRange(int dimension, float maxRange)
    {
        List<float> xValues = new List<float>(dimension);
        float xCoord = 0.0f;
        for (int i = 0; i < dimension ; ++i)
        {
            xCoord = colorGamut.MinRadiometricValue + (Mathf.Pow((float) i / (float) dimension, 2.0f) * maxRange);
            xValues.Add(xCoord);
        }

        return xValues;
    }

    void OnSceneGUI()
    {
        if (Application.isPlaying)
        {
            if (debugPoints == null || debugPoints.Count == 0 || debugPointsLinear == null || debugPointsLinear.Count == 0)
                _curveGuiDataState = CurveGuiDataState.NotCalculated;


            if (_curveGuiDataState == CurveGuiDataState.MustRecalculate ||
                _curveGuiDataState == CurveGuiDataState.NotCalculated)
            {
                recalculateCurveParameters();
                _curveGuiDataState = CurveGuiDataState.Calculated;
            }
            
            colorGamut = (ColorGamutShaped) target;
            controlPoints = colorGamut.getControlPoints();
            Vector2 p0 = controlPoints[0];
            Vector2 p1 = controlPoints[1];
            Vector2 p2 = controlPoints[2];
            Vector2 p3 = controlPoints[3];
            Vector2 p4 = controlPoints[4];
            Vector2 p5 = controlPoints[5];
            Vector2 p6 = controlPoints[6];
            
            Vector2 p0Linear = controlPointsLinear[0];
            Vector2 p1Linear = controlPointsLinear[1];
            Vector2 p2Linear = controlPointsLinear[2];
            Vector2 p3Linear = controlPointsLinear[3];
            Vector2 p4Linear = controlPointsLinear[4];
            Vector2 p5Linear = controlPointsLinear[5];
            Vector2 p6Linear = controlPointsLinear[6];

            Handles.DrawLine(new Vector3(0.0f, 0.0f), new Vector3(colorGamut.MaxRadiometricValue, 0.0f)); // Draw X Axis
            Handles.DrawLine(new Vector3(0.0f, 0.0f), new Vector3(0.0f, 5.0f)); // Draw Y axis
            // Draw auxiliary information on the graph

            Handles.DrawDottedLine(new Vector3(0.18f, 0.0f), new Vector3(0.18f, 0.18f), 1.0f); // Draw vertical line from 0.18f
            Handles.DrawDottedLine(new Vector3(0.0f, 0.18f), new Vector3(0.18f, 0.18f), 1.0f); // Draw vertical line from 0.18f
            Handles.DrawDottedLine(new Vector3(0.5f, 0.0f), new Vector3(0.5f, 0.5f), 4.0f);
            Handles.DrawDottedLine(new Vector3(0.0f, 0.5f), new Vector3(0.5f, 0.5f), 4.0f); // Draw vertical line from 0.18f
            Handles.DrawDottedLine(new Vector3(1.0f, 0.0f), new Vector3(1.0f, 1.0f), 8.0f); // Draw X = 1 line
            Handles.DrawDottedLine(new Vector3(0.0f, 1.0f), new Vector3(colorGamut.MaxRadiometricValue, 1.0f),8.0f); // Draw Y = 1 line
            Handles.DrawDottedLine(new Vector3(0.0f, 1.5f), new Vector3(colorGamut.MaxRadiometricValue, 1.5f),16.0f); // Draw Y = 1.5 line
            
            xTempValues = colorGamut.getXValues();
            yTempValues = colorGamut.getYValues();
            // for (int i = 0; i < xTempValues.Count; i++)
            // {
            //     Vector3 logPoint = new Vector3(xTempValues[i], yTempValues[i]);
            //     Vector3 linearPoint = new Vector3(Shaper.calculateLog2ToLinear(xTempValues[i], colorGamut.GreyPoint.x, colorGamut.MinRadiometricValue, colorGamut.MaxRadiometricValue),0.0f);
            //     Handles.DrawWireCube(logPoint, cubeWidgetSize);
            //     // Handles.DrawWireCube( linearPoint, cubeWidgetSize);
            //     // Handles.DrawDottedLine(linearPoint, logPoint, 1.0f);
            // }

          

            // debugPoints.Clear();
            // for (int i = 0; i < xValues.Count; i++)
            // {
            //     debugPoints.Add(new Vector3(Shaper.calculateLog2ToLinear(xValues[i], colorGamut.GreyPoint.x, colorGamut.MINExposureValue, colorGamut.MAXExposureValue),
            //         Shaper.calculateLog10ToLinear(yValues[i], colorGamut.GreyPoint.x, colorGamut.MINExposureValue, colorGamut.MAXExposureValue)));
            // }
            Handles.DrawPolyLine(debugPoints.ToArray());
            // Handles.DrawWireCube(new Vector3(p1.x, p1.y), cubeWidgetSize);
            // Handles.DrawWireCube(new Vector3(p3.x, p3.y), cubeWidgetSize);
            // Handles.DrawWireCube(new Vector3(p5.x, p5.y), cubeWidgetSize);
            
            // Draw linear curve
            //Handles.DrawPolyLine(debugPointsLinear.ToArray());
            // Handles.DrawWireCube(new Vector3(p1Linear.x, p1Linear.y), cubeWidgetSize * 2.0f);
            // Handles.DrawWireCube(new Vector3(p3Linear.x, p3Linear.y), cubeWidgetSize * 2.0f);
            // Handles.DrawWireCube(new Vector3(p5Linear.x, p5Linear.y), cubeWidgetSize * 2.0f);
        }
    }

    public override void OnInspectorGUI()
    {
        colorGamut = (ColorGamutShaped) target;

        base.serializedObject.UpdateIfRequiredOrScript();
        base.serializedObject.Update();
        base.DrawDefaultInspector();

        if (hdriNames != null && hdriNames.Count > 0)
        {
            hdriIndex = EditorGUILayout.Popup("HDRI to use", hdriIndex, hdriNames.ToArray());
        }

        activeTransferFunction =
            (TransferFunction) EditorGUILayout.EnumPopup("Active Transfer Function", activeTransferFunction);
        EditorGUILayout.Space();
        exposure = EditorGUILayout.Slider("Exposure", exposure, -32.0f, 32.0f);
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

        if (GUILayout.Button("Save Image to Disk"))
        {
            string fileName = Time.frameCount.ToString() + ".exr";
            string outPathTextureLut = EditorUtility.SaveFilePanel("Save Image to Disk", "", fileName,"exr");
            File.WriteAllBytes(@outPathTextureLut, colorGamut.HdriTextureTransformed.EncodeToEXR());
        }
        
        
        if (GUI.changed)
        {
            _curveGuiDataState = CurveGuiDataState.MustRecalculate;
        }

        // Only write back values once we are in Play mode
        if (Application.isPlaying && colorGamut.isActiveAndEnabled)
        {
            if (_curveGuiDataState == CurveGuiDataState.MustRecalculate ||
                _curveGuiDataState == CurveGuiDataState.NotCalculated)
            {
                colorGamut.setHDRIIndex(hdriIndex);
                colorGamut.setShowSweep(showSweep);
                colorGamut.setBleaching(enableBleaching);
                colorGamut.setIsMultiThreaded(isMultiThreaded);
                colorGamut.setShowOutOfGamutPixels(showPixelsOutOfGamut);
                colorGamut.setExposure(exposure);
                colorGamut.setActiveTransferFunction(activeTransferFunction);
                colorGamut.setBleachingRatioPower(bleachingRatioPower);

                //              colorGamut.setParametricCurveValues(slope, originPointX, originPointY, greyPointX, greyPointY);
                _curveGuiDataState = CurveGuiDataState.Calculated;
            }
        }

        base.serializedObject.ApplyModifiedProperties();
    }
}