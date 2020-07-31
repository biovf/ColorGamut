//#define GT_SHOULDER

using UnityEngine;

public class LottesToneMapper
{
    private float CONTRAST = 1.4f;
    private float SHOULDER = 1.0f;
    private float HDR_MAX  = 64.0f;
    private float MID_IN   = 0.18f;
    private float MID_OUT  = 0.18f;
    private Vector3 SATURATION  = Vector3.zero;
    private Vector3 CROSSTALK   = new Vector3(64.0f, 32.0f, 128.0f);
    private Vector3 CROSSTALK_SATURATION = new Vector3(4.0f, 1.0f, 16.0f);

    public void GtConstants(out Vector4 tone0, out Vector4 tone1, out Vector4 tone2, out Vector4 tone3, 
                     float contrast,  float shoulder, float hdrMax, float midIn, float midOut,
                     Vector3 saturation,Vector3 crosstalk,Vector3 crosstalkSaturation)
    {
        tone0.x = contrast;
        tone0.y = shoulder;
        float cs = contrast * shoulder;
        //--------------------------------------------------------------
        // TODO: Better factor and clean this up!!!!!!!!!!!!!!!!!!!!!!
        float z0 = -Mathf.Pow(midIn, contrast);
        float z1 = Mathf.Pow(hdrMax, cs) * Mathf.Pow(midIn, contrast);
        float z2 = Mathf.Pow(hdrMax, contrast) * Mathf.Pow(midIn, cs) * midOut;
        float z3 = Mathf.Pow(hdrMax, cs) * midOut;
        float z4 = Mathf.Pow(midIn, cs) * midOut;
        tone0.z = -((z0 + (midOut * (z1 - z2)) / (z3 - z4)) / z4);
        //--------------------------------------------------------------
        float w0 = Mathf.Pow(hdrMax, cs) * Mathf.Pow(midIn, contrast);
        float w1 = Mathf.Pow(hdrMax, contrast) * Mathf.Pow(midIn, cs) * midOut;
        float w2 = Mathf.Pow(hdrMax, cs) * midOut;
        float w3 = Mathf.Pow(midIn, cs) * midOut;
        tone0.w = (w0 - w1) / (w2 - w3);
        //--------------------------------------------------------------
        // Saturation base is contrast
        saturation += new Vector3(contrast, contrast, contrast);
        //tone1 = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);
        tone1 = new Vector3(saturation.x / crosstalkSaturation.x, saturation.y / crosstalkSaturation.y, saturation.z / crosstalkSaturation.z); //saturation / crosstalkSaturation;
        tone2 = crosstalk;
        tone3 = crosstalkSaturation;

    }

    public Vector3 GtFilter(  Vector3 color,// Linear input 
                    Vector4 tone0, Vector4 tone1, Vector4 tone2, Vector4 tone3)// Tonemapper constants
    {
        //--------------------------------------------------------------
        // Peak of all channels
        float peak = Mathf.Max(color.x, Mathf.Max(color.y, color.z));
        // Protect against /0
        peak = Mathf.Max(peak, 1.0f / (256.0f * 65536.0f));
        // Color ratio
        Vector3 ratio = color * (1.0f/peak);
        //--------------------------------------------------------------
        // Apply tonemapper to peak
        // Contrast adjustment
        peak = Mathf.Pow(peak, tone0.x);
        //--------------------------------------------------------------
        // Highlight compression
#if GT_SHOULDER
        peak = peak / (Mathf.Pow(peak, tone0.y) * tone0.z + tone0.w);
#else
        // No shoulder adjustment avoids extra pow
        peak = peak / (peak * tone0.z + tone0.w);
#endif
        //--------------------------------------------------------------
        // Convert to non-linear space and saturate
        // Saturation is folded into first transform
        ratio = new Vector3(Mathf.Pow(ratio.x, tone1.x), Mathf.Pow(ratio.y, tone1.y), Mathf.Pow(ratio.z, tone1.z));
        // Move towards white on overexposure
        Vector3 white = new Vector3(1.0f, 1.0f, 1.0f);
        //ratio = Mathf.Lerp(ratio, white, new Vector3(Mathf.Pow(peak, tone2.x), Mathf.Pow(peak, tone2.y), Mathf.Pow(peak, tone2.z)));
        ratio.x = Mathf.Lerp(ratio.x, white.x, Mathf.Pow(peak, tone2.x));
        ratio.y = Mathf.Lerp(ratio.y, white.y, Mathf.Pow(peak, tone2.y));
        ratio.z = Mathf.Lerp(ratio.z, white.z, Mathf.Pow(peak, tone2.z));

        // Convert back to linear
        ratio = new Vector3(Mathf.Pow(ratio.x, tone3.x), Mathf.Pow(ratio.y, tone3.y), Mathf.Pow(ratio.z, tone3.z));
        //--------------------------------------------------------------
        return ratio * peak;
    }
    //==============================================================


}


