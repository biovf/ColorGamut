using UnityEngine;

/// <summary>
/// This class implements a number of transfer functions(EOTF)  and their inverse transformations(OETF)
/// EOTF stands for Electro-Optical Transfer Function and OETF for Opto-electronic Transfer Function
/// </summary>
/// <remarks> Use this class instead of manually performing transfer function calculations via operations like <c>Pow(colorValue, 1.0f/2.2f)</c> or its inverse
/// </remarks>
public static class TransferFunction
{
    /// <summary>
    /// Value type that defines which set transfer function operations to use.
    /// </summary>
    /// <remarks>Note: for HDR display output, add here support for any future transfer functions like PQ </remarks>
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
                    outValue = ForwardSrgbEOTF(inputValue);
                }
                break;
            case TransferFunctionType.sRGB_2PartFunction:
                {
                    outValue = ForwardSrgb2PartEOTF(inputValue);
                }
                break;
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
                    outValue = InverseSrgbEOTF(inputValue);
                }
                break;
            case TransferFunctionType.sRGB_2PartFunction:
                {
                    outValue = InverseSrgb2PartEOTF(inputValue);
                }
                break;
        }

        return outValue;
    }


    private static float InverseSrgb2PartEOTF(float inputValue)
    {
        return (inputValue <= 0.0031308) ? inputValue * 12.92f : 1.055f * Mathf.Pow(inputValue, 1.0f / 2.4f) - 0.055f;
    }

    private static float ForwardSrgb2PartEOTF(float inputValue)
    {
        return inputValue <= 0.04045f ? inputValue / 12.92f : Mathf.Pow((inputValue + 0.055f) / 1.055f, 2.4f);
    }

    private static float InverseSrgbEOTF(float inputValue)
    {
        return Mathf.Pow(inputValue, 1.0f / 2.2f);
    }

    private static float ForwardSrgbEOTF(float inputValue)
    {
        return Mathf.Pow(inputValue, 2.2f);
    }
}

