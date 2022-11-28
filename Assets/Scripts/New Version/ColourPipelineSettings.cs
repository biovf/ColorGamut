using System;
using UnityEngine;

public enum ActiveColourPipeline
{
    DynamicPipeline,
    BakedPipeline
};

[Serializable]
public class ColourPipelineSettings
{
    public bool Enabled = true;

    [Header("Dynamic Pipeline - Colour Grading LUT (Editor Only)")]
    public Texture3D DynColourPipelineLUT;

    [Header("Baked Pipeline - Final Baked Colour LUT")]
    public Texture3D BakedColourPipelineLUT;

    [Header("Curve Slope")]
    public float CurveSlope = 1.7f;

    [Header("HDR Colour Pipeline")]
#if UNITY_EDITOR
    public ActiveColourPipeline ActiveColourPipeline = ActiveColourPipeline.DynamicPipeline;
#else
        public ActiveColourPipeline ActiveColourPipeline = ActiveColourPipeline.BakedPipeline;
#endif
}
