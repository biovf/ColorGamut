using MathNet.Numerics;
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
    private Vector2[] controlPoints;

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
        animationCurve = createAnimationCurve();

        CurveTest curve = new CurveTest();
        controlPoints = curve.createCurveControlPoints(new Vector2(0.18f, 0.18f), 1.5f);
        List<float> xValues = new List<float>() { controlPoints[2].x - float.Epsilon, controlPoints[6].x/ 2.0f, controlPoints[6].x};
        List<Vector2> controlPs = new List<Vector2>(controlPoints);
        List<float> results = curve.calcTfromXquadratic(xValues, controlPs);
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

    
    void OnSceneGUI()
    {
        //Debug.Log("Drawing?");

        Vector2 p0 = controlPoints[0];
        Vector2 p1 = controlPoints[1];
        Vector2 p2 = controlPoints[2];
        Vector2 p3 = controlPoints[3];
        Vector2 p4 = controlPoints[4];     
        Vector2 p5 = controlPoints[5];
        Vector2 p6 = controlPoints[6];
        
        //Handles.DrawLine(new Vector3(p0.x, p0.y), new Vector3(p1.x, p1.y));
        //Handles.DrawLine(new Vector3(p1.x, p1.y), new Vector3(p2.x, p2.y));
        //Handles.DrawLine(new Vector3(p2.x, p2.y), new Vector3(p3.x, p3.y));
        //Handles.DrawLine(new Vector3(p3.x, p3.y), new Vector3(p4.x, p4.y));
        //Handles.DrawLine(new Vector3(p4.x, p4.y), new Vector3(p5.x, p5.y));
        //Handles.DrawLine(new Vector3(p5.x, p5.y), new Vector3(p6.x, p6.y));

        Handles.DrawLine(new Vector3(p0.x, p0.y), new Vector3(p2.x, p2.y));
        Handles.DrawLine(new Vector3(p2.x, p2.y), new Vector3(p4.x, p4.y));
        Handles.DrawLine(new Vector3(p4.x, p4.y), new Vector3(p6.x, p6.y));
        Handles.DrawWireCube(new Vector3(rootValue, p0.y), new Vector3(0.001f, 0.001f));

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

        // drawCurve();


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
    void CheckCurveRT(int width, int height)
    {
        if (m_CurveTex == null || !m_CurveTex.IsCreated() || m_CurveTex.width != width || m_CurveTex.height != height)
        {
            //CoreUtils.Destroy(m_CurveTex);
            m_CurveTex = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            m_CurveTex.hideFlags = HideFlags.HideAndDontSave;
        }
    }
    Rect m_CurveRect;
    RenderTexture m_CurveTex;
    public Material curveWidgetMat;

    void drawCurve()
    {
         EditorGUILayout.Space();

        // Reserve GUI space
        using (new GUILayout.HorizontalScope())
        {
            GUILayout.Space(EditorGUI.indentLevel * 15f);
            m_CurveRect = GUILayoutUtility.GetRect(128, 80);
        }

        if (Event.current.type == EventType.Repaint)
        {
            // Prepare curve data
            // curveWidgetMat.SetVector(HDShaderIDs._Variants, new Vector4(alpha, m_HableCurve.whitePoint, 0f, 0f));

            CheckCurveRT((int)m_CurveRect.width, (int)m_CurveRect.height);

            var oldRt = RenderTexture.active;
            Graphics.Blit(null, m_CurveTex, curveWidgetMat);
            RenderTexture.active = oldRt;

            GUI.DrawTexture(m_CurveRect, m_CurveTex);

            Handles.DrawSolidRectangleWithOutline(m_CurveRect, Color.clear, Color.white * 0.4f);
        }
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
