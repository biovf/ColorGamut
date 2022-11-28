using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Class contains functionality to convert data from Linear to Log2 and vice-versa
/// This process allows linear data to be shaped and effectively compressed using a more perceptual friendly distribution
/// </summary>
public static class Shaper
{
    // # Convert scene referred linear value to normalised log value.
    public static float CalculateLinearToLog2(float linearRadValue, float greyPointX, float minExposureValue,
        float maxExposureValue)
    {
        if (linearRadValue < 0.0f)
            linearRadValue = minExposureValue;

        float dynamicRange = maxExposureValue - minExposureValue;
        float logRadiometricVal = Mathf.Clamp(Mathf.Log(linearRadValue / greyPointX, 2.0f), minExposureValue,
            maxExposureValue);

        return Mathf.Clamp01((logRadiometricVal - minExposureValue) / dynamicRange);
    }


    // # Convert normalised log value to scene referred linear value.
    public static float CalculateLog2ToLinear(float logRadValue, float greyPointX, float minExposureValue,
        float maxExposureValue)
    {
        float logNormalisedValue =
            Mathf.Clamp01(logRadValue) * (maxExposureValue - minExposureValue) + minExposureValue;
        return Mathf.Pow(2.0f, logNormalisedValue) * greyPointX;
    }


}
