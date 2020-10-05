using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Shaper 
{
// # Convert scene referred linear value to normalised log value.
    public static float calculateLinearToLog(float linearRadValue, float midGreyX, float minExposureValue, float maxExposureValue)
    {
        if (linearRadValue < 0.0f)
            linearRadValue = minExposureValue;

        float dynamicRange = maxExposureValue - minExposureValue;

        float logRadiometricVal = Mathf.Clamp(Mathf.Log(linearRadValue / midGreyX), minExposureValue, maxExposureValue);
        return (logRadiometricVal - minExposureValue) / dynamicRange;
    }


// # Convert normalised log value to scene referred linear value.
    public static float caculateLogToLinear(float logRadValue, float midGreyX, float minExposureValue, float maxExposureValue)
    {
        float logNormalisedValue = Mathf.Clamp01(logRadValue) * (maxExposureValue - minExposureValue) + minExposureValue;
        return Mathf.Pow(2.0f, logNormalisedValue) * midGreyX;
    }
}
