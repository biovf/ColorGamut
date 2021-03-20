﻿Shader "Custom/ChromaticityCompression"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        exposure("Exposure Value(EV)", Float) = 0.0
        greyPoint ("Middle Grey Value(XY)", Vector) = (0.18, 0.18, 0.0, 0.0)
        minRadiometricExposure ("Minimum Radiometric Exposure Value(EV)", Float) = -6.0
        maxRadiometricExposure ("Maximum Radiometric Exposure Value(EV)", Float) = 6.0
        maxRadiometricValue ("Maximum Radiometric Value", Float) = 12.0
        chromaticityMaxLowerBoundLatitude   ("Max Chromaticity Lower Bound value", Float) = 0.85
        gamutCompressionRatioPowerLowerBound("Gamut Compression Lower Bound Ratio", Float) = 1.0
        chromaticityMaxHigherBoundLatitude ("Max Chromaticity Higher Bound value", Float) = 0.95
        gamutCompressionRatioPowerHigherBound("Gamut Compression Higher Bound Ratio", Float) = 1.0
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma target 5.0
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D_half _MainTex;
            float4 greyPoint;
            float minRadiometricExposure;
            float maxRadiometricExposure;
            float maxRadiometricValue;
            float chromaticityMaxLowerBoundLatitude;
            float gamutCompressionRatioPowerLowerBound;

            float chromaticityMaxHigherBoundLatitude;
            float gamutCompressionRatioPowerHigherBound;
            float exposure;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }


            float calculateLinearToLog2(float linearRadValue, float midGreyX, float minExposureValue, float maxExposureValue)
            {
                if (linearRadValue < 0.0f)
                    linearRadValue = minExposureValue;

                float dynamicRange = maxExposureValue - minExposureValue;
                float logRadiometricVal = clamp(log2(linearRadValue / midGreyX), minExposureValue, maxExposureValue);

                return saturate((logRadiometricVal - minExposureValue) / dynamicRange);
            }

            static float calculateLog2ToLinear(float logRadValue, float midGreyX, float minExposureValue,
                                               float maxExposureValue)
            {
                float logNormalisedValue = clamp(logRadValue, 0.0, 1.0) * (maxExposureValue - minExposureValue) +
                    minExposureValue;
                return pow(2.0f, logNormalisedValue) * midGreyX;
            }

            float3 calculateGamutCompression(float4 inRadiometricLinearColor, float3 inputRatio)
            {
                return inputRatio;
                float3 ratio = inputRatio;
                // Calculate gamut compression values by iterating through the Y values array and returning the closest x coord
                float gamutCompressionXCoordLinearLowerBound = calculateLog2ToLinear(chromaticityMaxLowerBoundLatitude, greyPoint.x,
                    minRadiometricExposure, maxRadiometricExposure);
                float gamutCompressionXCoordLinearHigherBound = calculateLog2ToLinear(chromaticityMaxHigherBoundLatitude, greyPoint.x,
                    minRadiometricExposure, maxRadiometricExposure);

                    if (inRadiometricLinearColor.r > gamutCompressionXCoordLinearLowerBound ||
                    inRadiometricLinearColor.g > gamutCompressionXCoordLinearLowerBound ||
                    inRadiometricLinearColor.b > gamutCompressionXCoordLinearLowerBound)
                {
                    float maxRadiometricLinearChannel = max(inRadiometricLinearColor.r, max(inRadiometricLinearColor.g, inRadiometricLinearColor.b));
                    float gamutCompressionRange = maxRadiometricValue - gamutCompressionXCoordLinearLowerBound;
                    float gamutCompressionRatio = (maxRadiometricLinearChannel - gamutCompressionXCoordLinearLowerBound) /
                                            gamutCompressionRange;
                    float powerValue = 1.0f;
                   /* if (inRadiometricLinearColor.r > gamutCompressionXCoordLinearHigherBound ||
                        inRadiometricLinearColor.g > gamutCompressionXCoordLinearHigherBound ||
                        inRadiometricLinearColor.b > gamutCompressionXCoordLinearHigherBound) 
                    {
                        powerValue = pow(gamutCompressionRatio, gamutCompressionRatioPowerHigherBound);
                    }
                    else {
                        powerValue = pow(gamutCompressionRatio, gamutCompressionRatioPowerLowerBound);
                    }
                    if(true)
                        ratio = lerp(inputRatio, float3(1.0f, 1.0f, 1.0f), powerValue);
                    else
                        ratio = lerp(inputRatio, float3(1.0f, 1.0f, 1.0f), pow(gamutCompressionRatio, gamutCompressionRatioPowerLowerBound));*/
                    //{\displaystyle Y = 0.2126R + 0.7152G + 0.0722B.}

                    if (false) {
                        ratio.r = lerp(inputRatio.r, 1.0,  pow(gamutCompressionRatio, gamutCompressionRatioPowerLowerBound));
                        ratio.g = lerp(inputRatio.g, 1.0,  pow(gamutCompressionRatio, gamutCompressionRatioPowerLowerBound));
                        ratio.b = lerp(inputRatio.b, 1.0,  pow(gamutCompressionRatio, gamutCompressionRatioPowerLowerBound));
                    }
                    else {
                        // TODO: expose luminance ratios because these ones are for sRGB
                        float dechroma = dot(ratio, float3(0.2126, 0.7152, 0.0722));
                        ratio.r = lerp(inputRatio.r, dechroma,  pow(gamutCompressionRatio, gamutCompressionRatioPowerLowerBound));
                        ratio.g = lerp(inputRatio.g, dechroma,  pow(gamutCompressionRatio, gamutCompressionRatioPowerLowerBound));
                        ratio.b = lerp(inputRatio.b, dechroma,  pow(gamutCompressionRatio, gamutCompressionRatioPowerLowerBound));
                    }
                }
                 
                return ratio;
            }

            // Input is assumed to be radiometric linear
            // Output is encoded as Log2 camera intrinsic
            float4 chromaticityCompression(float4 inRadiometricLinearColor)
            {
                //float4 hdriPixelColor = float4(0.0, 0.0, 0.0, 1.0);
                float4 hdriPixelColor = inRadiometricLinearColor * pow(2.0, exposure);

                hdriPixelColor.r = max(0.0f, hdriPixelColor.r);
                hdriPixelColor.g = max(0.0f, hdriPixelColor.g);
                hdriPixelColor.b = max(0.0f, hdriPixelColor.b);

                hdriPixelColor.r = min(maxRadiometricValue, hdriPixelColor.r);
                hdriPixelColor.g = min(maxRadiometricValue, hdriPixelColor.g);
                hdriPixelColor.b = min(maxRadiometricValue, hdriPixelColor.b);
             
                float maxLinearPixelColor = max(hdriPixelColor.r, max(hdriPixelColor.g, hdriPixelColor.b));
                float3 ratio = hdriPixelColor / maxLinearPixelColor;

                if (maxLinearPixelColor >= 0.0f)
                {
                    ratio = calculateGamutCompression(hdriPixelColor, ratio);
                    hdriPixelColor.rgb = maxLinearPixelColor * ratio;
                }

                hdriPixelColor.r = calculateLinearToLog2(hdriPixelColor.r, greyPoint.x, minRadiometricExposure, maxRadiometricExposure);
                hdriPixelColor.g = calculateLinearToLog2(hdriPixelColor.g, greyPoint.x, minRadiometricExposure, maxRadiometricExposure);
                hdriPixelColor.b = calculateLinearToLog2(hdriPixelColor.b, greyPoint.x, minRadiometricExposure, maxRadiometricExposure);
                hdriPixelColor.a = 1.0f;

                return hdriPixelColor;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float4 col = tex2D(_MainTex, i.uv);
                return chromaticityCompression(col);
            }
            ENDCG
        }
    }
}
