using System;
using MathNet.Numerics;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(ColorGamut))]
public class ColorGamutEditor : Editor
{
    ColorGamut colorGamut;

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

    private List<Texture2D> HDRis;
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

    private Vector3 cubeWidgetSize = new Vector3(0.01f, 0.01f, 0.01f);

    // Prototype the state approach to test caching of data
    private enum CurveGuiDataState
    {
        NotCalculated,
        MustRecalculate,
        Calculated
    }

    private CurveGuiDataState _curveGuiDataState = CurveGuiDataState.NotCalculated;

    List<float> xTempValues;
    public void OnEnable()
    {
        colorGamut = (ColorGamut) target;
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

        // xTempValues = initialiseXCoordsInRangeTest(colorGamut.CurveLutLength,
        //     colorGamut.MaxRadiometricValue);
      
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
        xValues = colorGamut.initialiseXCoordsInRange(colorGamut.CurveLutLength,
            colorGamut.MaxRadiometricValue);

        debugPoints.Clear();
        yValues = parametricCurve.calcYfromXQuadratic(xValues, tValues,
            new List<Vector2>(controlPoints));

        for (int i = 0; i < xValues.Count; i++)
        {
            debugPoints.Add(new Vector3(xValues[i], yValues[i]));
        }
    }

    //  Temp
   // public List<float> initialiseXCoordsInRangeTest(int dimension, float maxRange)
   //  {
   //      List<float> xValues = new List<float>(dimension);
   //  
   //      // Use 
   //      float step = maxRange / (float) dimension;
   //
   //      float xCoord = 0.0f;
   //      for (int i = 0; i < dimension; ++i)
   //      {
   //          xCoord = colorGamut.MinRadiometricValue + (i * step);
   //          xValues.Add(Shaper.calculateLinearToLog((1.0f/(xCoord + 0.0001f)))); //Mathf.Pow(xCoord, 2.0f));
   //      }
   //
   //      // for (int i = 0; i <= (dimension/2); ++i)
   //      // {
   //      //     xCoord = colorGamut.MinRadiometricValue + (i * stepPreMidGrey);
   //      //     
   //      //     if (xCoord < colorGamut.MinRadiometricValue)
   //      //         continue;
   //      //
   //      //     if (Mathf.Approximately(xCoord, maxRange))
   //      //         break;
   //      //
   //      //     xValues.Add(Shaper.calculateLinearToLog(xCoord));
   //      //     Debug.Log("1st half - Index: " + i + " xCoord: " + xCoord + " \t Shaped Value " + xValues[i] + " \t ");
   //      // }
   //      //
   //      // int len = (dimension % 2) == 0 ? dimension / 2 : (dimension / 2) + 1;
   //      // for (int i = 1; i < len; ++i)
   //      // {
   //      //     xCoord = 0.18f + (i * stepPostMidGrey);
   //      //      
   //      //     if (xCoord < colorGamut.MinRadiometricValue)
   //      //         continue;
   //      //
   //      //     // if (Mathf.Approximately(xCoord, maxRange))
   //      //     //     break;
   //      //
   //      //     xValues.Add(Shaper.calculateLinearToLog(xCoord));
   //      //     Debug.Log("2nd half -Index: " + (xValues.Count - 1) + " xCoord: " + xCoord + " \t Shaped Value " + xValues[xValues.Count - 1] + " \t ");
   //      // }
   //  
   //      return xValues;
   //  }


    void OnSceneGUI()
    {
        if (Application.isPlaying)
        {
            if (debugPoints == null || debugPoints.Count == 0)
                _curveGuiDataState = CurveGuiDataState.NotCalculated;


            colorGamut = (ColorGamut) target;
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


            xTempValues = colorGamut.getXValues();
            foreach (var item in xTempValues)
            {
                Handles.DrawWireCube(new Vector3(item, 5.0f), cubeWidgetSize);
            }
            foreach (var item in xTempValues)
            {
                Handles.DrawWireCube(new Vector3(Shaper.calculateLogToLinear(item), 8.0f), cubeWidgetSize);
            }

            if (_curveGuiDataState == CurveGuiDataState.MustRecalculate ||
                _curveGuiDataState == CurveGuiDataState.NotCalculated)
            {
                recalculateCurveParameters();
                _curveGuiDataState = CurveGuiDataState.Calculated;
            }

            Handles.DrawPolyLine(debugPoints.ToArray());
            Handles.DrawWireCube(new Vector3(p1.x, p1.y), cubeWidgetSize);
            Handles.DrawWireCube(new Vector3(p3.x, p3.y), cubeWidgetSize);
            Handles.DrawWireCube(new Vector3(p5.x, p5.y), cubeWidgetSize);
        }
    }

    public override void OnInspectorGUI()
    {
        colorGamut = (ColorGamut) target;

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
        exposure = EditorGUILayout.Slider("Exposure", exposure, -6.0f, 6.0f);
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

                // Debug.Log("OnInspectorGUI() - GUI Changed");
                colorGamut.setParametricCurveValues(slope, originPointX, originPointY, greyPointX, greyPointY);
                _curveGuiDataState = CurveGuiDataState.Calculated;
            }
        }

        base.serializedObject.ApplyModifiedProperties();
    }
}