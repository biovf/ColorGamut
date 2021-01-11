using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Shaper
{
    
// # Convert scene referred linear value to normalised log value.
    public static float calculateLinearToLog2(float linearRadValue, float greyPointX, float minExposureValue, float maxExposureValue)
    {
        if (linearRadValue < 0.0f)
            linearRadValue = minExposureValue;

        float dynamicRange = maxExposureValue - minExposureValue;
        float logRadiometricVal = Mathf.Clamp(Mathf.Log(linearRadValue / greyPointX, 2.0f), minExposureValue, maxExposureValue);

        return Mathf.Clamp01((logRadiometricVal - minExposureValue) / dynamicRange);
    }


// # Convert normalised log value to scene referred linear value.
    public static float calculateLog2ToLinear(float logRadValue, float greyPointX, float minExposureValue, float maxExposureValue)
    {
        float logNormalisedValue = Mathf.Clamp01(logRadValue) * (maxExposureValue - minExposureValue) + minExposureValue;
        return Mathf.Pow(2.0f, logNormalisedValue) * greyPointX;
    }
    
    
    // Log 10 Shaper Code
    public static float calculateLinearToLog10(float linearRadValue, float greyPointX, float minExposureValue, float maxExposureValue)
    {
        if (linearRadValue < 0.0f)
            linearRadValue = minExposureValue;

        float dynamicRange = maxExposureValue - minExposureValue;
        float logRadiometricVal = Mathf.Clamp(Mathf.Log10(linearRadValue / greyPointX), minExposureValue, maxExposureValue);

        return (logRadiometricVal - minExposureValue) / dynamicRange;
    }


// # Convert normalised log value to scene referred linear value.
    public static float calculateLog10ToLinear(float logRadValue, float greyPointX, float minExposureValue, float maxExposureValue)
    {
        float logNormalisedValue = Mathf.Clamp01(logRadValue) * (maxExposureValue - minExposureValue) + minExposureValue;
        return Mathf.Pow(10.0f, logNormalisedValue) * greyPointX;
    }
    
}
