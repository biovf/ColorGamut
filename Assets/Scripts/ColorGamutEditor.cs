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

    private int hdriIndex = 0;
    private bool enableSliders = false;
    private bool enableBleaching = true;
    public void OnEnable()
    {
        originX = 0.0f;
        originY = 0.0f;
        midGreyX = 0.18f;
        midGreyY = 0.18f;
        shoulderStartX = 0.75f;
        shoulderStartY = 0.75f;
        shoulderEndX = 15.0f;
        shoulderEndY = 1.25f;

        p0 = new Keyframe(originX, originY);
        p1 = new Keyframe(midGreyX, midGreyY);
        p2 = new Keyframe(shoulderStartX, shoulderStartY);
        p3 = new Keyframe(shoulderEndX, shoulderEndY);

        keyframes = new Keyframe[] { p0, p1, p2, p3 };
        filmicCurve = new AnimationCurve(keyframes);

        hdriNames = new List<string>();
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

        if(hdriNames != null && hdriNames.Count > 0)
            hdriIndex = EditorGUILayout.Popup("HDRI to use", hdriIndex, hdriNames.ToArray());

        bool showSweep = EditorGUILayout.Toggle("Enable Color Sweep", colorGamut.getShowSweep());
        enableBleaching = EditorGUILayout.Toggle("Enable Bleaching", enableBleaching);

        enableSliders = EditorGUILayout.Toggle("Enable Sliders", enableSliders);
        originX     = EditorGUILayout.Slider("x0", originX, 0.0f, 10.0f);
        originY     = EditorGUILayout.Slider("y0", originY, 0.0f, 10.0f);
        midGreyX    = EditorGUILayout.Slider("Mid Grey X", midGreyX, 0.0f, 1.5f);
        midGreyY    = EditorGUILayout.Slider("Mid Grey Y", midGreyY, 0.0f, 1.5f);
        shoulderStartX  = EditorGUILayout.Slider("Shoulder Start X", shoulderStartX, 0.0f, 10.5f);
        shoulderStartY  = EditorGUILayout.Slider("Shoulder Start Y", shoulderStartY, 0.0f, 10.5f);
        shoulderEndX    = EditorGUILayout.Slider("Shoulder End X", shoulderEndX, 0.0f, 40.5f);
        shoulderEndY    = EditorGUILayout.Slider("Shoulder End Y", shoulderEndY, 0.0f, 40.5f);
        //Debug.Log("Origin X " + originX);
        //writeBackValues(originX, originY, midGreyX, midGreyY, shoulderStartX, shoulderStartY,
        //    shoulderEndX, shoulderEndY);

        //p0 = new Keyframe(originX, originY);
        //p1 = new Keyframe(midGreyX, midGreyY);
        //p2 = new Keyframe(shoulderStartX, shoulderStartY);
        //p3 = new Keyframe(shoulderEndX, shoulderEndY);

        //AnimationCurve linearCurve01 = AnimationCurve.Linear(p0.time, p0.value, p1.time, p1.value);
        //AnimationCurve linearCurve12 = AnimationCurve.Linear(p1.time, p1.value, p2.time, p2.value);
        //AnimationCurve linearCurve23 = AnimationCurve.Linear(p2.time, p2.value, p3.time, p3.value);
        //AnimationCurve linearCurve34 = AnimationCurve.Linear(p3.time, p3.value, p3.time * 2.0f, p3.value * 2.0f);

        //keyframes = new Keyframe[] { p0, p1, p2, p3 };
        //keyframes[0].outTangent = linearCurve01[0].outTangent;
        //keyframes[1].inTangent = linearCurve01[0].outTangent;
        //keyframes[1].outTangent = linearCurve12[0].outTangent;
        //keyframes[2].inTangent = linearCurve12[0].outTangent;
        //keyframes[2].outTangent = linearCurve23[0].outTangent;
        //keyframes[3].inTangent = linearCurve23[0].outTangent;

    
        if (enableSliders)
        {
            Keyframe[] keys = filmicCurve.keys;
            keys[0].time = originX;
            keys[0].value = originY;
            keys[1].time = midGreyX;
            keys[1].value = midGreyY;
            keys[2].time = shoulderStartX;
            keys[2].value = shoulderStartY;
            keys[3].time = shoulderEndX;
            keys[3].value = shoulderEndY;

            filmicCurve.keys = keys;
        }
        filmicCurve = EditorGUILayout.CurveField("Filmic Curve", filmicCurve);

        //AnimationCurve animationCurve = AnimationCurve.EaseInOut(0.0f, 0.0f, 1.0f, 1.0f);
        //EditorGUILayout.CurveField(" Curve", animationCurve);

        writeBackValues(filmicCurve.keys[0].time, filmicCurve.keys[0].value,
                        filmicCurve.keys[1].time, filmicCurve.keys[1].value,
                        filmicCurve.keys[2].time, filmicCurve.keys[2].value,
                        filmicCurve.keys[3].time, filmicCurve.keys[3].value);
        //Debug.Log(filmicCurve.keys[3].time);
        colorGamut.setHDRIIndex(hdriIndex);
        colorGamut.setAnimationCurve(filmicCurve);
        colorGamut.setShowSweep(showSweep);
        colorGamut.setBleaching(enableBleaching);

        base.serializedObject.ApplyModifiedProperties();
    }

    void setSliderValues(float inTmpOriginX, float inTmpOriginY, float inTmpMidGreyX, float inTmpMidGreyY, float inTmpShoulderStartX,
      float inTmpShoulderStartY, float inShoulderEndX, float inShoulderEndY)
    { 
        
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

}
