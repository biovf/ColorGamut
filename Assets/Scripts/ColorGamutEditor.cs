using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
[CustomEditor(typeof(ColorGamut))]

public class ColorGamutEditor : Editor
{
    ColorGamut colorGamut;
    AnimationCurve filmicCurve;

    [Header("Hable Curve Parameters")]
    #region DIRECT_PARAMS
    public float m_x0 = 0.18f;      //dstParams.m_x0  min 0        max 0.5
    public float m_y0 = 0.18f;      //dstParams.m_y0  min 0        max 0.5
    public float m_x1 = 0.75f;      //dstParams.m_x1  min 0        max 1.5
    public float m_y1 = 0.75f;      //dstParams.m_y1  min 0        max .99999 
    public float m_W = 1.0f;        //dstParams.m_W   min 1        max 2.5
    public float m_overshootX = 1.0f;
    public float m_overshootY = 1.0f;
    #endregion

    private Keyframe[] keyframes;
    private Keyframe p0;
    private Keyframe p1;
    private Keyframe p2;
    private Keyframe p3;

    public void OnEnable()
    {
        m_x0 = 0.18f;
        m_y0 = 0.18f;
        m_x1 = 0.75f;
        m_y1 = 0.75f;
        m_W = 1.0f;

        p0 = new Keyframe(0.0f, 0.0f);
        p0.weightedMode = WeightedMode.Out;
        p0.outWeight = 0.5f;

        p1 = new Keyframe(m_x0, m_y0);
        p1.weightedMode = WeightedMode.Out;
        p1.inWeight = 0.0f;
        p1.outWeight = 0.0f;

        p2 = new Keyframe(m_x1, m_y1);
        p2.weightedMode = WeightedMode.Out;
        p2.inWeight = 0.0f;
        p2.outWeight = 0.0f;

        p3 = new Keyframe(m_W, 1.0f);
        p3.weightedMode = WeightedMode.Out;
        p3.inWeight = 0.0f;
        p3.outWeight = 0.0f;

        keyframes = new Keyframe[] { p0, p1, p2, p3 };
        filmicCurve = new AnimationCurve(keyframes);
    }


    public override void OnInspectorGUI() 
    {
        colorGamut = (ColorGamut)target;

        base.serializedObject.UpdateIfRequiredOrScript();
        base.serializedObject.Update();
        base.DrawDefaultInspector();
        
        m_x0 = EditorGUILayout.Slider("x0", m_x0, 0.0f, 0.5f);
        m_y0 = EditorGUILayout.Slider("y0", m_y0, 0.0f, 0.5f);
        m_x1 = EditorGUILayout.Slider("x1", m_x1, 0.0f, 1.5f);
        m_y1 = EditorGUILayout.Slider("y1", m_y1, 0.0f, 1.0f);
        m_W  = EditorGUILayout.Slider("W", m_W, 0.0f, 200.0f);
        m_overshootX = EditorGUILayout.Slider("OverShootX", m_overshootX, 0.0f, 25.0f);
        m_overshootY = EditorGUILayout.Slider("OverShootY", m_overshootY, 0.0f, 25.0f);

        p0 = new Keyframe(0.0f, 0.0f);
        p1 = new Keyframe(m_x0, m_y0);
        p2 = new Keyframe(m_x1, m_y1);
        p3 = new Keyframe(m_W, 1.0f);

        AnimationCurve linearCurve01 = AnimationCurve.Linear(p0.time, p0.value, p1.time, p1.value);
        AnimationCurve linearCurve12 = AnimationCurve.Linear(p1.time, p1.value, p2.time, p2.value);
        AnimationCurve linearCurve23 = AnimationCurve.Linear(p2.time, p2.value, p3.time, p3.value);

        keyframes = new Keyframe[]{ p0, p1, p2, p3 };
        keyframes[0].outTangent = linearCurve01[0].outTangent;
        keyframes[1].inTangent = linearCurve01[0].outTangent;
        keyframes[1].outTangent = linearCurve12[0].outTangent;
        keyframes[2].inTangent = linearCurve12[0].outTangent;
        keyframes[2].outTangent = linearCurve23[0].outTangent;

        filmicCurve = new AnimationCurve(keyframes);
        filmicCurve = EditorGUILayout.CurveField("Filmic Curve", filmicCurve);
        
        m_x0 = filmicCurve.keys[1].time;
        m_y0 = filmicCurve.keys[1].value;
        m_x1 = filmicCurve.keys[2].time;
        m_y1 = filmicCurve.keys[2].value;
        m_W = filmicCurve.keys[3].time;

        colorGamut.setCurveValues(m_x0, m_y0, m_x1, m_y1, m_W, m_overshootX, m_overshootY);

        base.serializedObject.ApplyModifiedProperties();
    }
}
