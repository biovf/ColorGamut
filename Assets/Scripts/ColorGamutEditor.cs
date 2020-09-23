using System;
using MathNet.Numerics;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
[CustomEditor(typeof(ColorGamut))]

public class ColorGamutEditor : Editor
{
    ColorGamut colorGamut;
    
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

    private bool enableSliders    = false;
    private bool enableBleaching  = false;
    private bool isMultiThreaded  = false;
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


    public void OnEnable()
    {
        colorGamut = (ColorGamut)target;
        hdriNames = new List<string>();

        // Initialise parameters for the curve with sensible values
        if(_curveGuiDataState == CurveGuiDataState.NotCalculated)
            colorGamut.getParametricCurveValues(out slope, out originPointX, out originPointY, out greyPointX, out greyPointY);
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
        xValues = colorGamut.initialiseXCoordsInRange(colorGamut.CurveValueLutDim,
            colorGamut.MaxRadiometricValue);

        debugPoints.Clear();
        yValues = parametricCurve.calcYfromXQuadratic(xValues, tValues,
            new List<Vector2>(controlPoints));

        for (int i = 0; i < xValues.Count; i++)
        {
            debugPoints.Add(new Vector3(xValues[i], yValues[i]));
        }
    }

    //private float calculateXCoord() 
    //{
    //    if (colorGamut == null)
    //        return -100.0f;

    //    CurveTest parametricCurve = colorGamut.getParametricCurve();
    //    List<Vector2> cps = new List<Vector2>() { controlPoints[4], controlPoints[5], controlPoints[6] };
    //    //return parametricCurve.getXCoordinate(1.0f, colorGamut.getYValues(), colorGamut.getTValues(), cps);

    //    //for (int i = 0; i < cps.Count - 1; i += 3)
    //    {
    //        Vector2 p0 = cps[0];
    //        Vector2 p1 = cps[1];
    //        Vector2 p2 = cps[2];

    //        if (p0.x <= 1.0f && 1.0f <= p2.x)
    //        {
    //            // Search closest x value to xValue and grab its index in the array too
    //            // The array index is used to lookup the tValue
    //            int idx = 0;
    //            CurveTest.ClosestTo(colorGamut.getYValues(), 1.0f, out idx);
    //            float tValue = colorGamut.getTValues()[idx];

    //            return (Mathf.Pow(1.0f - tValue, 2.0f) * p0.y) +
    //                         (2.0f * (1.0f - tValue) * tValue * p1.y) +
    //                         (Mathf.Pow(tValue, 2.0f) * p2.y);
    //        }
    //    }
    //    return -100.0f;

    //}

    float XValue = -1000.0f;
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

            ////if(XValue < - 100.0f)
            //    XValue = calculateXCoord();
            //Handles.DrawWireCube(new Vector3(XValue, 1.0f), cubeWidgetSize);

            Handles.DrawLine(new Vector3(0.0f, 0.0f), new Vector3(12.0f, 0.0f)); // Draw X Axis
            Handles.DrawLine(new Vector3(0.0f, 0.0f), new Vector3(0.0f, 5.0f));  // Draw Y axis
            Handles.DrawDottedLine(new Vector3(1.0f, 0.0f), new Vector3(1.0f, 5.0f), 4.0f); // Draw X = 1 line
            Handles.DrawDottedLine(new Vector3(0.0f, 1.0f), new Vector3(colorGamut.MaxRadiometricValue, 1.0f), 4.0f);  // Draw Y = 1 line
        
            
            if (_curveGuiDataState == CurveGuiDataState.MustRecalculate || _curveGuiDataState == CurveGuiDataState.NotCalculated)
            {
                Debug.Log("OnSceneGUI has changed, recalculate");
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
        colorGamut = (ColorGamut)target;
        
        if (colorGamut.HDRIList != null)
        {
            HDRis = colorGamut.HDRIList;
            for (int i = 0; i < HDRis.Count; i++)
            {
                hdriNames.Add(new string(HDRis[i].ToString().ToCharArray()));
            }
        }
        base.serializedObject.UpdateIfRequiredOrScript();
        base.serializedObject.Update();
        base.DrawDefaultInspector();

        if (hdriNames != null && hdriNames.Count > 0)
        {
            hdriIndex = EditorGUILayout.Popup("HDRI to use", hdriIndex, hdriNames.ToArray());
        }

        bool showSweep     = EditorGUILayout.Toggle("Enable Color Sweep", colorGamut.getShowSweep());
        enableBleaching    = EditorGUILayout.Toggle("Enable Bleaching",         enableBleaching);
        isMultiThreaded    = EditorGUILayout.Toggle("Enable MultiThreading",    isMultiThreaded);
        showPixelsOutOfGamut  = EditorGUILayout.Toggle("Show Pixels Out of Gamut",     showPixelsOutOfGamut);
        // enableSliders      = EditorGUILayout.Toggle("Enable Sliders",           enableSliders);

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        colorGamut.getParametricCurveValues(out slope, out originPointX, out originPointY, out greyPointX,
            out greyPointY);
        slope         = EditorGUILayout.Slider("Slope",    slope, colorGamut.SlopeMin, colorGamut.SlopeMax);
        originPointX  = EditorGUILayout.Slider("Origin X", originPointX, 0.0f, 1.0f);
        originPointY  = EditorGUILayout.Slider("Origin Y", originPointY, 0.0f, 1.0f);
        greyPointX    = EditorGUILayout.Slider("greyPointX", greyPointX, 0.0f, 1.0f);
        greyPointY    = EditorGUILayout.Slider("greyPointY", greyPointY, 0.0f, 1.0f);
       
        if (GUI.changed)
        {
            _curveGuiDataState = CurveGuiDataState.MustRecalculate;
        }
        
        // Only write back values once we are in Play mode
        if (Application.isPlaying)
        {
            colorGamut.setHDRIIndex(hdriIndex);
            colorGamut.setShowSweep(showSweep);
            colorGamut.setBleaching(enableBleaching);
            colorGamut.setIsMultiThreaded(isMultiThreaded);
            colorGamut.setShowOutOfGamutPixels(showPixelsOutOfGamut);

            if(_curveGuiDataState == CurveGuiDataState.MustRecalculate || _curveGuiDataState == CurveGuiDataState.NotCalculated)
            {
                Debug.Log("OnInspectorGUI() - GUI Changed");
                colorGamut.setParametricCurveValues(slope, originPointX, originPointY, greyPointX, greyPointY);
                _curveGuiDataState = CurveGuiDataState.Calculated;
            }
        }

        base.serializedObject.ApplyModifiedProperties();
    }

  

}
