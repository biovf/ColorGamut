//using System;
//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;

//public static class Shaper2
//{
    
//// # Convert scene referred linear value to normalised log value.
//    public static float calculateLinearToLog2(float linearRadValue, float midGreyX, float minExposureValue, float maxExposureValue)
//    {
//        if (linearRadValue < 0.0f)
//            linearRadValue = minExposureValue;

//        float dynamicRange = maxExposureValue - minExposureValue;
//        float logRadiometricVal = Mathf.Clamp(Mathf.Log(linearRadValue / midGreyX, 2.0f), minExposureValue, maxExposureValue);

//        return (logRadiometricVal - minExposureValue) / dynamicRange;
//    }


//    public static Color calculateLinearToLog2(Color linearRadValue, float midGreyX, float minExposureValue, float maxExposureValue)
//    {
//        if (linearRadValue.r < 0.0f)
//            linearRadValue.r = minExposureValue;
        
//        if (linearRadValue.g < 0.0f)
//            linearRadValue.g = minExposureValue;

//        if (linearRadValue.b < 0.0f)
//            linearRadValue.b = minExposureValue;

//        float dynamicRange = maxExposureValue - minExposureValue;
//        float logRadiometricValR = Mathf.Clamp(Mathf.Log(linearRadValue.r / midGreyX, 2.0f), minExposureValue, maxExposureValue);
//        float logRadiometricValG = Mathf.Clamp(Mathf.Log(linearRadValue.g / midGreyX, 2.0f), minExposureValue, maxExposureValue);
//        float logRadiometricValB = Mathf.Clamp(Mathf.Log(linearRadValue.b / midGreyX, 2.0f), minExposureValue, maxExposureValue);

//        return new Color((logRadiometricValR - minExposureValue) / dynamicRange, 
//                         (logRadiometricValG - minExposureValue) / dynamicRange,
//                         (logRadiometricValB - minExposureValue) / dynamicRange);
//    }


//    // # Convert normalised log value to scene referred linear value.
//    public static float calculateLog2ToLinear(float logRadValue, float midGreyX, float minExposureValue, float maxExposureValue)
//    {
//        float logNormalisedValue = Mathf.Clamp01(logRadValue) * (maxExposureValue - minExposureValue) + minExposureValue;
//        return Mathf.Pow(2.0f, logNormalisedValue) * midGreyX;
//    }
    
    
//    // Log 10 Shaper Code
//    public static float calculateLinearToLog10(float linearRadValue, float midGreyX, float minExposureValue, float maxExposureValue)
//    {
//        if (linearRadValue < 0.0f)
//            linearRadValue = minExposureValue;

//        float dynamicRange = maxExposureValue - minExposureValue;
//        float logRadiometricVal = Mathf.Clamp(Mathf.Log10(linearRadValue / midGreyX), minExposureValue, maxExposureValue);
//        if (true)
//        {
//            return (logRadiometricVal - minExposureValue) / dynamicRange;
//        }
//        else
//        {
//            return linearRadValue;
//        }
//    }


//// # Convert normalised log value to scene referred linear value.
//    public static float calculateLog10ToLinear(float logRadValue, float midGreyX, float minExposureValue, float maxExposureValue)
//    {
//        float logNormalisedValue = Mathf.Clamp01(logRadValue) * (maxExposureValue - minExposureValue) + minExposureValue;
//        return Mathf.Pow(10.0f, logNormalisedValue) * midGreyX;
//    }
    
//}
