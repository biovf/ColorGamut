﻿Shader "Custom/GamutMap"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        exposure ("Exposure Value(EV)", Float) = 0.0
        greyPoint ("Middle Grey Value(XY)", Vector) = (0.18, 0.18, 0.0, 0.0)
        minExposure ("Minimum Exposure Value(EV)", Float) = -6.0
        maxExposure ("Maximum Exposure Value(EV)", Float) = 6.0
        maxRadiometricValue ("Maximum Radiometric Value", Float) = 12.0
        inputArraySize ("Number of curve array elements", Int) = 1024
        usePerChannel ("Use per channel gamut mapping", Int) = 0
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            // Upgrade NOTE: excluded shader from DX11, OpenGL ES 2.0 because it uses unsized arrays
            #pragma exclude_renderers d3d11 gles
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
            float exposure;
            half4 greyPoint;
            half minExposure;
            half maxExposure;
            half maxRadiometricValue;
            int inputArraySize;
            int usePerChannel;
            float xCoords[1024];
            float yCoords[1024];

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }


            half calculateLinearToLog2(half linearRadValue, half midGreyX, half minExposureValue, half maxExposureValue)
            {
                if (linearRadValue < 0.0f)
                    linearRadValue = minExposureValue;

                half dynamicRange = maxExposureValue - minExposureValue;
                half logRadiometricVal = clamp(log2(linearRadValue / midGreyX), minExposureValue, maxExposureValue);

                return (logRadiometricVal - minExposureValue) / dynamicRange;
            }

            static float calculateLog2ToLinear(float logRadValue, float midGreyX, float minExposureValue,
                                               float maxExposureValue)
            {
                float logNormalisedValue = clamp(logRadValue, 0.0, 1.0) * (maxExposureValue - minExposureValue) +
                    minExposureValue;
                return pow(2.0f, logNormalisedValue) * midGreyX;
            }

            float inverseSrgbEOTF(float inputValue)
            {
                return pow(inputValue, 1.0f / 2.2f);
            }

            float srgbEOTF(float inputValue)
            {
                return pow(inputValue, 2.2f);
            }

            float BilinearClosestTo(float inputArray[1024], float target, out int arrayIndex, out int arrayIndex2)
            {
                // Terrible horrible hack since there is not MaxValue for a float variable
                float closest = 9999999.0;
                float minDifference = 9999999.0;
                float prevDifference = 9999999.0;

                int outIndex = 0;
                int outIndex2 = 0;

                for (int i = 0; i < inputArraySize; i++)
                {
                    float currentDifference = abs((float)inputArray[i] - target);

                    // Early exit because the array is always ordered from smallest to largest
                    if (prevDifference < currentDifference)
                        break;

                    if (minDifference > currentDifference)
                    {
                        // Check which of the values, before or after this one, are closer to the target value
                        int indexBefore = clamp((i - 1), 0, inputArraySize - 1);
                        int indexAfter = clamp((i + 1), 0, inputArraySize - 1);
                        float currentDiffBefore = abs((float)inputArray[indexBefore] - target);
                        float currentDiffAfter = abs((float)inputArray[indexAfter] - target);

                        minDifference = currentDifference;
                        closest = inputArray[i];
                        outIndex = i;
                        outIndex2 = (currentDiffBefore < currentDiffAfter) ? indexBefore : indexAfter;
                    }

                    prevDifference = currentDifference;
                }

                arrayIndex = outIndex;
                arrayIndex2 = outIndex2;
                return closest;
            }

            float ClosestTo(float inputArray[1024], float target, out int arrayIndex)
            {
                float closest = 999999.0f;
                float minDifference = 999999.0f;
                float prevDifference = 999999.0f;

                int outIndex = 0;
                for (int i = 0; i < inputArraySize; i++)
                {
                    float currentDifference = abs((float)inputArray[i] - target);

                    // Early exit because the array is always ordered from smallest to largest
                    if (prevDifference < currentDifference)
                        break;

                    if (minDifference > currentDifference)
                    {
                        minDifference = currentDifference;
                        closest = inputArray[i];
                        outIndex = i;
                    }

                    prevDifference = currentDifference;
                }

                arrayIndex = outIndex;
                return closest;
            }

            float getXCoordinate(float inputYCoord, float xCoords[1024], float YCoords[1024])
            {
                int idx = 0;
                ClosestTo(YCoords, inputYCoord, idx);
                return xCoords[idx];
            }

            half3 calculateGamutCompression(half3 linearHdriPixelColor, half3 ratio, half linearHdriMaxRGBChannel)
            {
                half3 newRatio = ratio;
                  float gamutCompressionXCoordLinear = 0.0f; // Intersect of x on Y = 1

                    // Calculate gamut compression values by iterating through the Y values array and returning the closest x coord
                    gamutCompressionXCoordLinear = calculateLog2ToLinear(
                        getXCoordinate(1.0f, xCoords, yCoords), greyPoint.x, minExposure, maxExposure);

                    if (linearHdriPixelColor.r > gamutCompressionXCoordLinear ||
                        linearHdriPixelColor.g > gamutCompressionXCoordLinear ||
                        linearHdriPixelColor.b > gamutCompressionXCoordLinear)
                    {
                        half gamutCompressionRange = maxRadiometricValue - gamutCompressionXCoordLinear;
                        half gamutCompressionRatio = (max(linearHdriPixelColor.r,
                                                          max(linearHdriPixelColor.g, linearHdriPixelColor.b)) -
                                gamutCompressionXCoordLinear) /
                            gamutCompressionRange;


                        half3 maxDynamicRangeVec = half3(maxRadiometricValue, maxRadiometricValue, maxRadiometricValue);
                        linearHdriPixelColor = lerp(linearHdriPixelColor, maxDynamicRangeVec,
                                                    smoothstep(0.0f, 1.0f, gamutCompressionRatio));

                        newRatio = linearHdriPixelColor / linearHdriMaxRGBChannel;
                    }
                return newRatio;
            }

            half getYCoordinateLogXInput(float inputXCoord)
            {
                float logInputXCoord = inputXCoord;

                int idx = 0;
                int idx2 = 0;
                BilinearClosestTo(xCoords, logInputXCoord, idx, idx2);
                float linearInputXCoord =
                    calculateLog2ToLinear(logInputXCoord, greyPoint.x, minExposure, maxExposure);
                float linearXCoordIdx = calculateLog2ToLinear(
                    xCoords[idx], greyPoint.x, minExposure, maxExposure);
                float linearXCoordIdx2 = calculateLog2ToLinear(
                    xCoords[idx2], greyPoint.x, minExposure, maxExposure);

                // Calculate interpolation factor
                if (idx == idx2)
                {
                    return yCoords[idx];
                }
                else if (idx < idx2)
                {
                    float lerpValue = (linearInputXCoord - linearXCoordIdx) / (linearXCoordIdx2 - linearXCoordIdx);
                    return lerp(yCoords[idx], yCoords[idx2], lerpValue);
                }
                else
                {
                    float lerpValue = (linearInputXCoord - linearXCoordIdx2) / (linearXCoordIdx - linearXCoordIdx2);
                    return lerp(yCoords[idx2], yCoords[idx], lerpValue);
                }
            }

            float4 gamutMap(half4 inColor)
            {
                half4 hdriPixelColor = half4(0.0, 0.0, 0.0, 1.0);
                half4 colorExposed = inColor * pow(2.0, exposure);
                // Shape image
                half3 log2HdriPixelArray = half3(0.0, 0.0, 0.0);
                log2HdriPixelArray.r = calculateLinearToLog2(max(0.0f, colorExposed.r), greyPoint.x, minExposure,
                                                             maxExposure);
                log2HdriPixelArray.g = calculateLinearToLog2(max(0.0f, colorExposed.g), greyPoint.x, minExposure,
                                                             maxExposure);
                log2HdriPixelArray.b = calculateLinearToLog2(max(0.0f, colorExposed.b), greyPoint.x, minExposure,
                                                             maxExposure);

                // Calculate Pixel max color and ratio
                half logHdriMaxRGBChannel = max(max(log2HdriPixelArray.r, log2HdriPixelArray.g), log2HdriPixelArray.b);
                half3 linearHdriPixelColor = half3(
                    calculateLog2ToLinear(log2HdriPixelArray.r, greyPoint.x, minExposure, maxExposure),
                    calculateLog2ToLinear(log2HdriPixelArray.g, greyPoint.x, minExposure, maxExposure),
                    calculateLog2ToLinear(log2HdriPixelArray.b, greyPoint.x, minExposure, maxExposure));

                if(usePerChannel == 0)
                {
                    // Retrieve the maximum RGB value but in linear space
                    half linearHdriMaxRGBChannel = calculateLog2ToLinear(logHdriMaxRGBChannel, greyPoint.x,
                                                                         minExposure, maxExposure);
                    // Calculate the ratio in linear space
                    half3 ratio = linearHdriPixelColor / linearHdriMaxRGBChannel;
                    half rawMaxPixelValue = linearHdriMaxRGBChannel;

                    // Secondary Nuance Grade, guardrails
                    if (linearHdriPixelColor.r > maxRadiometricValue ||
                        linearHdriPixelColor.g > maxRadiometricValue ||
                        linearHdriPixelColor.b > maxRadiometricValue)
                    {
                        linearHdriPixelColor.r = maxRadiometricValue;
                        linearHdriPixelColor.g = maxRadiometricValue;
                        linearHdriPixelColor.b = maxRadiometricValue;
                    }

                    ratio = calculateGamutCompression(linearHdriPixelColor, ratio, linearHdriMaxRGBChannel);
                    
                    half yValue = getYCoordinateLogXInput(logHdriMaxRGBChannel);
                    yValue = srgbEOTF(yValue);

                    half hdriYMaxValue = min(yValue, 1.0f);
                    hdriPixelColor.rgb = hdriYMaxValue * ratio;
                }
                else
                {
                    hdriPixelColor.r = getYCoordinateLogXInput(log2HdriPixelArray.r);
                    hdriPixelColor.g = getYCoordinateLogXInput(log2HdriPixelArray.g);
                    hdriPixelColor.b = getYCoordinateLogXInput(log2HdriPixelArray.b);
                } 
                
                hdriPixelColor.r = inverseSrgbEOTF(hdriPixelColor.r);
                hdriPixelColor.g = inverseSrgbEOTF(hdriPixelColor.g);
                hdriPixelColor.b = inverseSrgbEOTF(hdriPixelColor.b);
                hdriPixelColor.a = 1.0f;

                return hdriPixelColor;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half4 col = tex2D(_MainTex, i.uv);
                return gamutMap(col);
            }
            ENDCG
        }
    }
}