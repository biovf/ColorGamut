using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
[CustomEditor(typeof(GamutCurve))]


public class CurveTestEditor : Editor
{

    public void OnEnable()
    {
        //p0 = new Keyframe(originX, originY);
        //p1 = new Keyframe(midGreyX, midGreyY);
        //p2 = new Keyframe(shoulderStartX, shoulderStartY);
        //p3 = new Keyframe(shoulderEndX, shoulderEndY);

    }

    public override void OnInspectorGUI()
    {
        AnimationCurve animationCurve = AnimationCurve.EaseInOut(0.0f, 0.0f, 1.0f, 1.0f);
        animationCurve = EditorGUILayout.CurveField(" Curve", animationCurve);


        //animationCurve = 
        //EditorGUILayout.CurveField(" Curve2", animationCurve);


    }
}
