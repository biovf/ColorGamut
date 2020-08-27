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
    AnimationCurve animationCurve;
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

        //Keyframe logP0 = new Keyframe(originX, originY);
        //Keyframe logP1 = new Keyframe((midGreyX),       (midGreyY));
        //Keyframe logP2 = new Keyframe((shoulderStartX), (shoulderStartY));
        //Keyframe logP3 = new Keyframe((shoulderEndX),   (shoulderEndY));

        //Keyframe[] tempKeys = new Keyframe[] { logP0, logP1, logP2, logP3 };
        animationCurve = createAnimationCurve();//new AnimationCurve(tempKeys);//AnimationCurve.EaseInOut(0.0f, 0.0f, 1.0f, 1.0f);
        //animationCurve.SmoothTangents(1, 2.0f);
        CurveTest curve = new CurveTest();
        curve.linearSection(new Vector2(0.18f, 0.18f), 1.5f);
    }

    float TimeFromValue(AnimationCurve curve, float value, float precision = 1e-6f)
    {
        float minTime = curve.keys[0].time;
        float maxTime = curve.keys[curve.keys.Length - 1].time;
        float best = (maxTime + minTime) / 2;
        float bestVal = curve.Evaluate(best);
        int it = 0;
        const int maxIt = 1000;
        float sign = Mathf.Sign(curve.keys[curve.keys.Length - 1].value - curve.keys[0].value);
        while (it < maxIt && Mathf.Abs(minTime - maxTime) > precision)
        {
            if ((bestVal - value) * sign > 0)
            {
                maxTime = best;
            }
            else
            {
                minTime = best;
            }
            best = (maxTime + minTime) / 2;
            bestVal = curve.Evaluate(best);
            it++;
        }
        return best;
    }


    public AnimationCurve createAnimationCurve() 
    {

        //Calculate slope in middle linear section
        //    Create linear curve (-10, -10) to (10, 10)
        //    Evaluate curve and get
 

        Keyframe p0 = new Keyframe(originX, originY);
        Keyframe p1 = new Keyframe(midGreyX - 0.1f, midGreyY - 0.1f);
        Keyframe p2  = new Keyframe(midGreyX, midGreyY); //midGrey
        Keyframe p3 = new Keyframe(shoulderStartX, shoulderStartY);
        Keyframe p4 = new Keyframe(shoulderEndX, shoulderEndY);
        Keyframe[] tempKeys = new Keyframe[] { p0, p1, p2, p3, p4 };

        AnimationCurve linearCurve01 = AnimationCurve.Linear(p0.time, p0.value, p1.time, p1.value);
        AnimationCurve linearCurve12 = AnimationCurve.Linear(p1.time, p1.value, p2.time, p2.value);
        AnimationCurve linearCurve23 = AnimationCurve.Linear(p2.time, p2.value, p3.time, p3.value);

        tempKeys[0].outTangent   = linearCurve01[0].outTangent;
        tempKeys[1].inTangent    = linearCurve01[0].outTangent;
        tempKeys[1].outTangent   = linearCurve12[0].outTangent;
        tempKeys[2].inTangent    = linearCurve12[0].outTangent;
        tempKeys[2].outTangent   = linearCurve23[0].outTangent;
        tempKeys[3].inTangent    = linearCurve23[0].outTangent;

        animationCurve = new AnimationCurve(tempKeys);
        float Yintersect = TimeFromValue(animationCurve, 1.0f);
        animationCurve.AddKey(Yintersect, 1.0f);
        return animationCurve;
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

        //Debug.Log("3 " + animationCurve.keys[3].outTangent + " " + animationCurve.keys[3].outWeight);
        //Debug.Log("4 " + animationCurve.keys[4].outTangent + " " + animationCurve.keys[4].outWeight);


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

        
        EditorGUILayout.CurveField(" Curve", animationCurve);

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
