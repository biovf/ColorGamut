using System;
using MathNet.Numerics;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
[CustomEditor(typeof(ColorGamut))]

public class ColorGamutEditor : Editor
{
    ColorGamut colorGamut;
    // public AnimationCurve filmicCurve;
    // [Header("Curve Parameters")]
    // #region DIRECT_PARAMS
    // public float originX        ;
    // public float originY        ;
    // public float midGreyX       ;
    // public float midGreyY       ;
    // public float shoulderStartX ;
    // public float shoulderStartY ;
    // public float shoulderEndX   ;
    // public float shoulderEndY;
    // #endregion
    
    // private Keyframe[] keyframes;
    // private Keyframe p0;
    // private Keyframe p1;
    // private Keyframe p2;
    // private Keyframe p3;
    
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
    private bool enableOldGamutMap = false;
    
    AnimationCurve animationCurve;
    private Vector2[] controlPoints;
    private CurveTest parametricCurve;
    
    private List<float> tValues;
    private List<float> xValues;
    private List<float> yValues;
    private List<Vector3> debugPoints;

    private Vector3 cubeWidgetSize = new Vector3(0.01f, 0.01f, 0.01f);
    public void OnEnable()
    {
        colorGamut = (ColorGamut)target;
        hdriNames = new List<string>();

        // Initialise parameters for the curve with sensible values
        colorGamut.getParametricCurveValues(out slope, out originPointX, out originPointY, out greyPointX, out greyPointY);
    }

    private void OnValidate()
    {
        // Debug.Log("Values changing");
    }

    void OnSceneGUI()
    {
         colorGamut = (ColorGamut)target;
        // Vector2[] controlPoints = colorGamut.getControlPoints();
        // Vector2 p0 = controlPoints[0];
        // Vector2 p1 = controlPoints[1];
        // Vector2 p2 = controlPoints[2];
        // Vector2 p3 = controlPoints[3];
        // Vector2 p4 = controlPoints[4];     
        // Vector2 p5 = controlPoints[5];
        // Vector2 p6 = controlPoints[6];
        //
        // Handles.DrawLine(new Vector3(0.0f, 0.0f), new Vector3(12.0f, 0.0f));
        // // Draw Y axis
        // Handles.DrawLine(new Vector3(0.0f, 0.0f), new Vector3(0.0f, 5.0f));
        // // Draw X = 1 line
        // Handles.DrawDottedLine(new Vector3(1.0f, 0.0f), new Vector3(1.0f, 5.0f), 4.0f);
        // // Draw Y = 1 line
        // Handles.DrawDottedLine(new Vector3(0.0f, 1.0f), new Vector3(colorGamut.MaxRadiometricValue, 1.0f), 4.0f);
        //
        if (GUI.changed || debugPoints == null || debugPoints.Count == 0)
        {
            Debug.Log("GUI has changed, recalculate");
            colorGamut.OnGuiChanged(true);
            // parametricCurve = colorGamut.getParametricCurve();
            // tValues = colorGamut.getTValues();
            // xValues = colorGamut.initialiseXCoordsInRange(colorGamut.CurveValueLutDim, colorGamut.MaxRadiometricValue);
            //
            // debugPoints = new List<Vector3>();
            // yValues = parametricCurve.calcYfromXQuadratic(xValues, tValues,
            //         new List<Vector2>(controlPoints));
            //
            // for (int i = 0; i < xValues.Count; i++)
            // {
            //     debugPoints.Add(new Vector3(xValues[i], yValues[i]));    
            // }
        }
        colorGamut.OnGuiChanged(false);

        // if (debugPoints != null && debugPoints.Count > 0)
        // {
        //     Handles.DrawPolyLine(debugPoints.ToArray());
        //
        // }
        // else
        // {
        //     Debug.LogError("DebugPoints is null or has no data");
        // }
        // Handles.DrawWireCube(new Vector3(p1.x, p1.y), cubeWidgetSize);
        // Handles.DrawWireCube(new Vector3(p3.x, p3.y), cubeWidgetSize);
        // Handles.DrawWireCube(new Vector3(p5.x, p5.y), cubeWidgetSize);
        
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
        enableOldGamutMap  = EditorGUILayout.Toggle("Enable Old Gamut Map",     enableOldGamutMap);
        enableSliders      = EditorGUILayout.Toggle("Enable Sliders",           enableSliders);

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        colorGamut.getParametricCurveValues(out slope, out originPointX, out originPointY, out greyPointX,
            out greyPointY);
        slope         = EditorGUILayout.Slider("Slope",    slope, colorGamut.SlopeMin, colorGamut.SlopeMax);
        originPointX  = EditorGUILayout.Slider("Origin X", originPointX, 0.0f, 1.0f);
        originPointY  = EditorGUILayout.Slider("Origin Y", originPointY, 0.0f, 1.0f);
        greyPointX    = EditorGUILayout.Slider("greyPointX", greyPointX, 0.0f, 1.0f);
        greyPointY    = EditorGUILayout.Slider("greyPointY", greyPointY, 0.0f, 1.0f);
        
        colorGamut.setHDRIIndex(hdriIndex);
        colorGamut.setShowSweep(showSweep);
        colorGamut.setBleaching(enableBleaching);
        colorGamut.setIsMultiThreaded(isMultiThreaded);
        // TODO: refactor
        // if (GUI.changed)
        {
            // colorGamut.OnGuiChanged(true);
            // Debug.Log("Passing new parameters");
            //colorGamut.setParametricCurveValues(slope, originPointX, originPointY, greyPointX, greyPointY);
        }
        // else
        // {
        //     colorGamut.OnGuiChanged(false);
        // }            


        colorGamut.setEnableOldGamutMap(enableOldGamutMap);
        
        base.serializedObject.ApplyModifiedProperties();
    }

  

}
