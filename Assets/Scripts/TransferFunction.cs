using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class TransferFunction
{
    public enum TransferFunctionType
    {
        sRGB,
        sRGB_2PartFunction
    }

    public static float ApplyTransferFunction(float inputValue, TransferFunctionType transferFunctionType)
    {
        float outValue = 0.0f;
        switch (transferFunctionType)
        {
            case TransferFunctionType.sRGB:
            {
                outValue = forwardSrgbEOTF(inputValue);
            } break;
            case TransferFunctionType.sRGB_2PartFunction:
            {
                outValue = forwardSrgb2PartEOTF(inputValue);
            } break;
        }

        return outValue;
    }
    
    public static float ApplyInverseTransferFunction(float inputValue, TransferFunctionType transferFunctionType)
    {
        float outValue = 0.0f;
        switch (transferFunctionType)
        {
            case TransferFunctionType.sRGB:
            {
                outValue = inverseSrgbEOTF(inputValue);
            } break;
            case TransferFunctionType.sRGB_2PartFunction:
            {
                outValue = inverseSrgb2PartEOTF(inputValue);
            } break;
        }

        return outValue;
    }
    
    
    public static float inverseSrgb2PartEOTF(float inputValue) 
    {
        return  (inputValue <= 0.0031308) ? inputValue * 12.92f : 1.055f * Mathf.Pow(inputValue, 1.0f / 2.4f) - 0.055f;
    }

    public static float forwardSrgb2PartEOTF(float inputValue)
    {
        return inputValue <= 0.04045f ? inputValue / 12.92f : Mathf.Pow((inputValue + 0.055f) / 1.055f, 2.4f);
    }

    public static float inverseSrgbEOTF(float inputValue) 
    {
        return Mathf.Pow(inputValue, 1.0f / 2.2f);
    }
    
    public static float forwardSrgbEOTF(float inputValue)
    {
        return Mathf.Pow(inputValue, 2.2f);
    }
    
}
