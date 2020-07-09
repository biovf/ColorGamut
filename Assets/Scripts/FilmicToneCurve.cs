using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CurveParamsUser
{
    public CurveParamsUser()
    {
        m_toeStrength = 0.0f;
        m_toeLength = 0.5f;
        m_shoulderStrength = 0.0f; // white point
        m_shoulderLength = 0.5f;

        m_shoulderAngle = 0.0f;
        m_gamma = 1.0f;
    }
    public float m_toeStrength; // as a ratio
    public float m_toeLength; // as a ratio
    public float m_shoulderStrength; // as a ratio
    public float m_shoulderLength; // in F stops
    public float m_shoulderAngle; // as a ratio

    public float m_gamma;
};

public class CurveParamsDirect
{
    public CurveParamsDirect()
    {
        Reset();
    }

    public void Reset()
    {
        m_x0 = .25f;    // 0.0225
        m_y0 = .25f;    // 0.005625
        m_x1 = .75f;    // 2-3   
        m_y1 = .75f;    // 0.8
        m_W = 1.0f;

        m_gamma = 1.0f;

        m_overshootX = 0.0f;
        m_overshootY = 0.0f;
    }

    public float m_x0;
    public float m_y0;
    public float m_x1;
    public float m_y1;
    public float m_W;

    public float m_overshootX;
    public float m_overshootY;

    public float m_gamma;
};

public class CurveSegment
{
    public CurveSegment()
    {
        Reset();
    }

    public void Reset()
    {
        m_offsetX = 0.0f;
        m_offsetY = 0.0f;
        m_scaleX = 1.0f; // always 1 or -1
        m_lnA = 0.0f;
        m_B = 1.0f;
    }

    public float Eval(float x)
    {
        float x0 = (x - m_offsetX) * m_scaleX;
        float y0 = 0.0f;

        // log(0) is undefined but our function should evaluate to 0. There are better ways to handle this,
        // but it's doing it the slow way here for clarity.
        if (x0 > 0)
        {
            y0 = Mathf.Exp(m_lnA + m_B * Mathf.Log(x0));
        }

        return y0 * m_scaleY + m_offsetY;
    }
    public float EvalInv(float y)
    {
        float y0 = (y - m_offsetY) / m_scaleY;
        float x0 = 0.0f;

        // watch out for log(0) again
        if (y0 > 0)
        {
            x0 = Mathf.Exp((Mathf.Log(y0) - m_lnA) / m_B);
        }
        float x = x0 / m_scaleX + m_offsetX;

        return x;
    }

    public float m_offsetX;
    public float m_offsetY;
    public float m_scaleX; // always 1 or -1
    public float m_scaleY;
    public float m_lnA;
    public float m_B;
};


public class FullCurve
{
    public FullCurve()
    {
        m_segments    = new CurveSegment[3];
        m_invSegments = new CurveSegment[3];
        for (int i = 0; i < 3; i++)
        {
            m_segments[i] = new CurveSegment();
            m_invSegments[i] = new CurveSegment();

        }
        Reset();
    }

    public void Reset()
    {
        m_W = 1.0f;
        m_invW = 1.0f;

        m_x0 = .25f;
        m_y0 = .25f;
        m_x1 = .75f;
        m_y1 = .75f;


        for (int i = 0; i < 3; i++)
        {
            m_segments[i].Reset();
            m_invSegments[i].Reset();
        }
    }

    public float Eval(float srcX)
    {
        float normX = srcX * m_invW;
        int index = (normX < m_x0) ? 0 : ((normX < m_x1) ? 1 : 2);
        CurveSegment segment = m_segments[index];
        float ret = segment.Eval(normX);
        return ret;
    }
    public float EvalInv(float y)
    {
        int index = (y < m_y0) ? 0 : ((y < m_y1) ? 1 : 2);
        CurveSegment segment = m_segments[index];

        float normX = segment.EvalInv(y);
        return normX * m_W;
    }

    public float m_W;
    public float m_invW;

    public float m_x0;
    public float m_x1;
    public float m_y0;
    public float m_y1;


    public CurveSegment[] m_segments;   
    public CurveSegment[] m_invSegments;
};


public class FilmicToneCurve
{
    public static FullCurve CreateCurve(CurveParamsDirect srcParams)
    {
        CurveParamsDirect curveParams = srcParams;
        FullCurve dstCurve = new FullCurve();
        dstCurve.Reset();
        dstCurve.m_W = srcParams.m_W;
        dstCurve.m_invW = 1.0f / srcParams.m_W;

        // normalize curveParams to 1.0 range
        curveParams.m_W = 1.0f;
        curveParams.m_x0 /= srcParams.m_W;
        curveParams.m_x1 /= srcParams.m_W;
        curveParams.m_overshootX = srcParams.m_overshootX / srcParams.m_W;

        float toeM = 0.0f;
        float shoulderM = 0.0f;
        float endpointM = 0.0f;
        {
            float m = 0.0f, b = 0.0f;
            AsSlopeIntercept(ref m, ref b, curveParams.m_x0, curveParams.m_x1, curveParams.m_y0, curveParams.m_y1);

            float g = srcParams.m_gamma;

            // base function of linear section plus gamma is
            // y = (mx+b)^g

            // which we can rewrite as
            // y = exp(g*ln(m) + g*ln(x+b/m))

            // and our evaluation function is (skipping the if parts):
            /*
				float x0 = (x - m_offsetX)*m_scaleX;
				y0 = Math.Exp(m_lnA + m_B*Math.Log(x0));
				return y0*m_scaleY + m_offsetY;
			*/

            CurveSegment midSegment = new CurveSegment();
            midSegment.m_offsetX = -(b / m);
            midSegment.m_offsetY = 0.0f;
            midSegment.m_scaleX = 1.0f;
            midSegment.m_scaleY = 1.0f;
            midSegment.m_lnA = g * Mathf.Log(m);
            midSegment.m_B = g;

            dstCurve.m_segments[1] = midSegment;

            toeM = EvalDerivativeLinearGamma(m, b, g, curveParams.m_x0);
            shoulderM = EvalDerivativeLinearGamma(m, b, g, curveParams.m_x1);

            // apply gamma to endpoints
            curveParams.m_y0 = Mathf.Max(1e-5f, Mathf.Pow(curveParams.m_y0, curveParams.m_gamma));
            curveParams.m_y1 = Mathf.Max(1e-5f, Mathf.Pow(curveParams.m_y1, curveParams.m_gamma));

            curveParams.m_overshootY = Mathf.Pow(1.0f + curveParams.m_overshootY, curveParams.m_gamma) - 1.0f;
        }

        dstCurve.m_x0 = curveParams.m_x0;
        dstCurve.m_x1 = curveParams.m_x1;
        dstCurve.m_y0 = curveParams.m_y0;
        dstCurve.m_y1 = curveParams.m_y1;

        // toe section
        {
            CurveSegment toeSegment = new CurveSegment();
            toeSegment.m_offsetX = 0;
            toeSegment.m_offsetY = 0.0f;
            toeSegment.m_scaleX = 1.0f;
            toeSegment.m_scaleY = 1.0f;

            SolveAB(ref toeSegment.m_lnA, ref toeSegment.m_B, curveParams.m_x0, curveParams.m_y0, toeM);
            dstCurve.m_segments[0] = toeSegment;
        }

        // shoulder section
        {
            // use the simple version that is usually too flat 
            CurveSegment shoulderSegment = new CurveSegment();

            float x0 = (1.0f + curveParams.m_overshootX) - curveParams.m_x1;
            float y0 = (1.0f + curveParams.m_overshootY) - curveParams.m_y1;

            float lnA = 0.0f;
            float B = 0.0f;
            SolveAB(ref lnA, ref B, x0, y0, shoulderM);

            shoulderSegment.m_offsetX = (1.0f + curveParams.m_overshootX);
            shoulderSegment.m_offsetY = (1.0f + curveParams.m_overshootY);

            shoulderSegment.m_scaleX = -1.0f;
            shoulderSegment.m_scaleY = -1.0f;
            shoulderSegment.m_lnA = lnA;
            shoulderSegment.m_B = B;

            dstCurve.m_segments[2] = shoulderSegment;
        }

        // Normalize so that we hit 1.0 at our white point. We wouldn't have do this if we 
        // skipped the overshoot part.
        {
            // evaluate shoulder at the end of the curve
            float scale = dstCurve.m_segments[2].Eval(1.0f);
            float invScale = 1.0f / scale;

            dstCurve.m_segments[0].m_offsetY *= invScale;
            dstCurve.m_segments[0].m_scaleY *= invScale;

            dstCurve.m_segments[1].m_offsetY *= invScale;
            dstCurve.m_segments[1].m_scaleY *= invScale;

            dstCurve.m_segments[2].m_offsetY *= invScale;
            dstCurve.m_segments[2].m_scaleY *= invScale;
        }

        return dstCurve;
    }


    public static CurveParamsDirect CalcDirectParamsFromUser(CurveParamsUser srcParams)
    {
        CurveParamsDirect dstParams = new CurveParamsDirect();

        float toeStrength = srcParams.m_toeStrength;
        float toeLength = srcParams.m_toeLength;
        float shoulderStrength = srcParams.m_shoulderStrength;
        float shoulderLength = srcParams.m_shoulderLength;

        float shoulderAngle = srcParams.m_shoulderAngle;
        float gamma = srcParams.m_gamma;

        // This is not actually the display gamma. It's just a UI space to avoid having to 
        // enter small numbers for the input.
        float perceptualGamma = 2.2f;

        // constraints
        {
            toeLength = Mathf.Pow(Mathf.Clamp01(toeLength), perceptualGamma);
            toeStrength = Mathf.Clamp01(toeStrength);
            shoulderAngle = Mathf.Clamp01(shoulderAngle);
            shoulderLength = Math.Max(1e-5f, Mathf.Clamp01(shoulderLength));

            shoulderStrength = Math.Max(0.0f, shoulderStrength);
        }

        // apply base params
        {
            // toe goes from 0 to 0.5
            float x0 = toeLength * .5f;
            float y0 = (1.0f - toeStrength) * x0; // lerp from 0 to x0

            float remainingY = 1.0f - y0;

            float initialW = x0 + remainingY;

            float y1_offset = (1.0f - shoulderLength) * remainingY;
            float x1 = x0 + y1_offset;
            float y1 = y0 + y1_offset;

            // filmic shoulder strength is in F stops
            float extraW = Mathf.Pow(2.0f, shoulderStrength) - 1.0f;

            float W = initialW + extraW;

            dstParams.m_x0 = x0;
            dstParams.m_y0 = y0;
            dstParams.m_x1 = x1;
            dstParams.m_y1 = y1;
            dstParams.m_W = W;

            // bake the linear to gamma space conversion
            dstParams.m_gamma = gamma;
        }

        dstParams.m_overshootX = (dstParams.m_W * 2.0f) * shoulderAngle * shoulderStrength;
        dstParams.m_overshootY = 0.5f * shoulderAngle * shoulderStrength;

        //Debug.Log("dstParams.m_x0 " + dstParams.m_x0);
        //Debug.Log("dstParams.m_y0 " + dstParams.m_y0);
        //Debug.Log("dstParams.m_x1 " + dstParams.m_x1);
        //Debug.Log("dstParams.m_y1 " + dstParams.m_y1);
        //Debug.Log("dstParams.m_W  " + dstParams.m_W);

        return dstParams;
    }

    // find a function of the form:
    //   f(x) = e^(lnA + Bln(x))
    // where
    //   f(0)   = 0; not really a constraint
    //   f(x0)  = y0
    //   f'(x0) = m
    public static void SolveAB(ref float lnA, ref float B, float x0, float y0, float m)
    {
        B = (m * x0) / y0;
        lnA = Mathf.Log(y0) - B * Mathf.Log(x0);
    }

    // convert to y=mx+b
    public static void AsSlopeIntercept(ref float m, ref float b, float x0, float x1, float y0, float y1)
    {
        float dy = (y1 - y0);
        float dx = (x1 - x0);
        if (dx == 0)
            m = 1.0f;
        else
            m = dy / dx;

        b = y0 - x0 * m;
    }

    // f(x) = (mx+b)^g
    // f'(x) = gm(mx+b)^(g-1)
    public static float EvalDerivativeLinearGamma(float m, float b, float g, float x)
    {
        float ret = g * m * Mathf.Pow(m * x + b, g - 1.0f);
        return ret;
    }


}
