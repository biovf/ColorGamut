using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
[CustomEditor(typeof(ColorGamut))]

public class ColorGamutEditor : Editor
{
    ColorGamut colorGamut;
    public AnimationCurve filmicCurve;

    [Header("Curve Parameters")]
    #region DIRECT_PARAMS
    //public float m_x0 = 0.18f;      //dstParams.m_x0  min 0        max 0.5
    //public float m_y0 = 0.18f;      //dstParams.m_y0  min 0        max 0.5
    //public float m_x1 = 0.75f;      //dstParams.m_x1  min 0        max 1.5
    //public float m_y1 = 0.75f;      //dstParams.m_y1  min 0        max .99999 
    //public float m_W = 1.0f;        //dstParams.m_W   min 1        max 2.5
    //public float m_overshootX = 1.0f;
    //public float m_overshootY = 1.0f;
    public float originX        ;
    public float originY        ;
    public float midGreyX       ;
    public float midGreyY       ;
    public float shoulderStartX ;
    public float shoulderStartY ;
    public float shoulderEndX   ;
    public float shoulderEndY;
    #endregion

    private Keyframe[] keyframes;
    private Keyframe p0;
    private Keyframe p1;
    private Keyframe p2;
    private Keyframe p3;

    private List<Texture2D> HDRis;
    private List<string> hdriNames;
    public void OnEnable()
    {
        //m_x0 = 0.18f;
        //m_y0 = 0.18f;
        //m_x1 = 0.75f;
        //m_y1 = 0.75f;
        //m_W = 1.0f;
        originX = 0.0f;
        originY = 0.0f;
        midGreyX = 0.18f;
        midGreyY = 0.18f;
        shoulderStartX = 0.75f;
        shoulderStartY = 0.75f;
        shoulderEndX = 1.0f;
        shoulderEndY = 1.0f;

        p0 = new Keyframe(originX, originY);
        //p0.weightedMode = WeightedMode.Out;
        //p0.outWeight = 0.5f;

        p1 = new Keyframe(midGreyX, midGreyY);
        //p1.weightedMode = WeightedMode.Out;
        //p1.inWeight = 0.0f;
        //p1.outWeight = 0.0f;

        p2 = new Keyframe(shoulderStartX, shoulderStartY);
        //p2.weightedMode = WeightedMode.Out;
        //p2.inWeight = 0.0f;
        //p2.outWeight = 0.0f;

        p3 = new Keyframe(shoulderEndX, shoulderEndY);
        //p3.weightedMode = WeightedMode.Out;
        //p3.inWeight = 0.0f;
        //p3.outWeight = 0.0f;

        keyframes = new Keyframe[] { p0, p1, p2, p3 };
        filmicCurve = new AnimationCurve(keyframes);
        //colorGamut.setAnimationCurve(filmicCurve);

        hdriNames = new List<string>();
    }

    void writeBackValues(float inOriginX, float inOriginY, float inMidGreyX, float inMidGreyY, float inShoulderStartX,
        float inShoulderStartY, float inShoulderEndX, float inShoulderEndY)
    {
        if (inOriginX != originX) 
        {
            originX = inOriginX;
        }
        if (inOriginY != originY)
        {
            originY = inOriginY;
        }
        if (inMidGreyX != midGreyX)
        {
            midGreyX = inMidGreyX;
        }
        if (inMidGreyY != midGreyY)
        {
            midGreyY = inMidGreyY;
        }
        if (inShoulderStartX != shoulderStartX) 
        {
            shoulderStartX = inShoulderStartX;
        }
        if (inShoulderStartY != shoulderStartY) 
        {
            shoulderStartY = inShoulderStartY;
        }
        if (inShoulderEndX != shoulderEndX) 
        {
            shoulderEndX = inShoulderEndX;
        }
        if (inShoulderEndY != shoulderEndY)
        {
            shoulderEndY = inShoulderEndY;
        }

    }

    private int hdriIndex = 0;
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


        if(hdriNames != null && hdriNames.Count > 0)
            hdriIndex = EditorGUILayout.Popup("HDRI to use", hdriIndex, hdriNames.ToArray());

        float temp_originX     = EditorGUILayout.Slider("x0", originX, 0.0f, 10.0f);
        float temp_originY     = EditorGUILayout.Slider("y0", originY, 0.0f, 10.0f);
        float temp_midGreyX    = EditorGUILayout.Slider("Mid Grey X", midGreyX, 0.0f, 1.5f);
        float temp_midGreyY    = EditorGUILayout.Slider("Mid Grey Y", midGreyY, 0.0f, 1.5f);
        float temp_shoulderStartX  = EditorGUILayout.Slider("Shoulder Start X", shoulderStartX, 0.0f, 10.5f);
        float temp_shoulderStartY  = EditorGUILayout.Slider("Shoulder Start Y", shoulderStartY, 0.0f, 10.5f);
        float temp_shoulderEndX    = EditorGUILayout.Slider("Shoulder End X", shoulderEndX, 0.0f, 40.5f);
        float temp_shoulderEndY    = EditorGUILayout.Slider("Shoulder End Y", shoulderEndY, 0.0f, 40.5f);

        writeBackValues(temp_originX, temp_originY, temp_midGreyX, temp_midGreyY, temp_shoulderStartX, temp_shoulderStartY,
            temp_shoulderEndX, temp_shoulderEndY);

        //m_x1 = EditorGUILayout.Slider("x1", m_x1, 0.0f, 1.5f);
        //m_y1 = EditorGUILayout.Slider("y1", m_y1, 0.0f, 1.0f);
        //m_W  = EditorGUILayout.Slider("W", m_W, 0.0f, 200.0f);
        //m_overshootX = EditorGUILayout.Slider("OverShootX", m_overshootX, 0.0f, 25.0f);
        //m_overshootY = EditorGUILayout.Slider("OverShootY", m_overshootY, 0.0f, 25.0f);
        p0 = new Keyframe(originX,         originY);
        p1 = new Keyframe(midGreyX,        midGreyY);
        p2 = new Keyframe(shoulderStartX,  shoulderStartY);
        p3 = new Keyframe(shoulderEndX,    shoulderEndY);

        //AnimationCurve linearCurve01 = AnimationCurve.Linear(p0.time, p0.value, p1.time, p1.value);
        //AnimationCurve linearCurve12 = AnimationCurve.Linear(p1.time, p1.value, p2.time, p2.value);
        //AnimationCurve linearCurve23 = AnimationCurve.Linear(p2.time, p2.value, p3.time, p3.value);

        keyframes = new Keyframe[]{ p0, p1, p2, p3 };
        //keyframes[0].outTangent = linearCurve01[0].outTangent;
        //keyframes[1].inTangent = linearCurve01[0].outTangent;
        //keyframes[1].outTangent = linearCurve12[0].outTangent;
        //keyframes[2].inTangent = linearCurve12[0].outTangent;
        //keyframes[2].outTangent = linearCurve23[0].outTangent;

        filmicCurve = new AnimationCurve(keyframes);
        filmicCurve = EditorGUILayout.CurveField("Filmic Curve", filmicCurve);

        writeBackValues(filmicCurve.keys[0].time, filmicCurve.keys[0].value,
                        filmicCurve.keys[1].time, filmicCurve.keys[1].value,
                        filmicCurve.keys[2].time, filmicCurve.keys[2].value,
                        filmicCurve.keys[3].time, filmicCurve.keys[3].value);

        //m_x0 = filmicCurve.keys[1].time;
        //m_y0 = filmicCurve.keys[1].value;
        //m_x1 = filmicCurve.keys[2].time;
        //m_y1 = filmicCurve.keys[2].value;
        //m_W = filmicCurve.keys[3].time;

        //colorGamut.setCurveValues(m_x0, m_y0, m_x1, m_y1, m_W, m_overshootX, m_overshootY);
        colorGamut.setHDRIIndex(hdriIndex);
        colorGamut.setAnimationCurve(filmicCurve);

        base.serializedObject.ApplyModifiedProperties();
    }
}
