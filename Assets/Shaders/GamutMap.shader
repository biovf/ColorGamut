Shader "Custom/GamutMap"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        exposure ("Exposure Value(EV)", Float) = 0.0
        greyPoint ("Middle Grey Value(XY)", Vector) = (0.18, 0.18, 0.0, 0.0)
        minRadiometricExposure ("Minimum Radiometric Exposure Value(EV)", Float) = -6.0
        maxRadiometricExposure ("Maximum Radiometric Exposure Value(EV)", Float) = 6.0
        minDisplayExposure ("Min Display Exposure (unit agnostic)", Float) = 0.0
        maxDisplayExposure ("Max Display Exposure (unit agnostic)", Float) = 1.0
        minDisplayValue("Min Display Value (unit agnostic)", Float) = 0.0005
        maxDisplayValue("Max Display Value (unit agnostic)", Float) = 1.0
        maxRadiometricValue ("Maximum Radiometric Value", Float) = 12.0
        inputArraySize ("Number of curve array elements", Int) = 1024
        usePerChannel ("Use per channel gamut mapping", Int) = 0
        heatmap ("HeatMap", Int) = 0
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
            float exposure;
            half4 greyPoint;
            half minRadiometricExposure;
            half maxRadiometricExposure;
            half minDisplayExposure;
            half maxDisplayExposure;
            half minDisplayValue;
            half maxDisplayValue;
            half maxRadiometricValue;

            int inputArraySize;
            int usePerChannel;
            int heatmap;

            struct GamutCurveCoords
            {
                float curveCoord;
            };
            StructuredBuffer<GamutCurveCoords> xCurveCoordsCBuffer;
            StructuredBuffer<GamutCurveCoords> yCurveCoordsCBuffer;

            half4 controlPoints[7];

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

                return saturate((logRadiometricVal - minExposureValue) / dynamicRange);
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

            void BilinearClosestTo(float target, out int arrayIndex, out int arrayIndex2)
            {
                int outIndex = 0;
                int outIndex2 = 0;

                int maxArrayIndex = inputArraySize - 1;
                outIndex = clamp(round(target * maxArrayIndex), 0, maxArrayIndex);

                int indexBefore = clamp((outIndex - 1), 0, maxArrayIndex);
                int indexAfter = clamp((outIndex + 1), 0, maxArrayIndex);
                float currentDiffBefore = abs((float)xCurveCoordsCBuffer[indexBefore].curveCoord - target);
                float currentDiffAfter = abs((float)xCurveCoordsCBuffer[indexAfter].curveCoord - target);
                outIndex2 = (currentDiffBefore < currentDiffAfter) ? indexBefore : indexAfter;

                arrayIndex = outIndex;
                arrayIndex2 = outIndex2;
            }

            float ClosestTo(float target, out int arrayIndex)
            {
                float closest = 999999.0f;
                float minDifference = 999999.0f;
                float prevDifference = 999999.0f;

                int outIndex = 0;
                for (int i = 0; i < inputArraySize; i++)
                {
                    float currentDifference = abs((float)yCurveCoordsCBuffer[i].curveCoord - target);

                    // Early exit because the array is always ordered from smallest to largest
                    if (prevDifference < currentDifference)
                        break;

                    if (minDifference > currentDifference)
                    {
                        minDifference = currentDifference;
                        closest = yCurveCoordsCBuffer[i].curveCoord;
                        outIndex = i;
                    }

                    prevDifference = currentDifference;
                }

                arrayIndex = outIndex;
                return closest;
            }

            float getXCoordinate(float inputYCoord)
            {
                int idx = 0;
                ClosestTo(inputYCoord, idx);
                return xCurveCoordsCBuffer[idx].curveCoord;
            }

            half getYCoordinateLogXInput(float inputXCoord)
            {
                float logInputXCoord = inputXCoord;

                int idx = 0;
                int idx2 = 0;
                BilinearClosestTo(logInputXCoord, idx, idx2);
                float linearInputXCoord =
                    calculateLog2ToLinear(logInputXCoord, greyPoint.x, minRadiometricExposure, maxRadiometricExposure);
                float linearXCoordIdx = calculateLog2ToLinear(
                    xCurveCoordsCBuffer[idx].curveCoord, greyPoint.x, minRadiometricExposure, maxRadiometricExposure);
                float linearXCoordIdx2 = calculateLog2ToLinear(
                    xCurveCoordsCBuffer[idx2].curveCoord, greyPoint.x, minRadiometricExposure, maxRadiometricExposure);

                // Calculate interpolation factor
                if (idx == idx2)
                {
                    return yCurveCoordsCBuffer[idx].curveCoord;
                }
                else if (idx < idx2)
                {
                    float lerpValue = (linearInputXCoord - linearXCoordIdx) / (linearXCoordIdx2 - linearXCoordIdx);
                    return lerp(yCurveCoordsCBuffer[idx].curveCoord, yCurveCoordsCBuffer[idx2].curveCoord, lerpValue);
                }
                else
                {
                    float lerpValue = (linearInputXCoord - linearXCoordIdx2) / (linearXCoordIdx - linearXCoordIdx2);
                    return lerp(yCurveCoordsCBuffer[idx2].curveCoord, yCurveCoordsCBuffer[idx].curveCoord, lerpValue);
                }
            }

            half remap(half inputValue, half min0, half max0, half min1, half max1)
            {
                return min1 + (inputValue - min0) * ((max1 - min1) / (max0 - min0));
            }

            float4 gamutMap(half4 inRadiometricLinearColor)
            {
                half4 hdriPixelColor = half4(0.0, 0.0, 0.0, 1.0);

                float3 linearHdriPixelColor = float3(
                calculateLog2ToLinear(inRadiometricLinearColor.r, greyPoint.x, minRadiometricExposure, maxRadiometricExposure),
                calculateLog2ToLinear(inRadiometricLinearColor.g, greyPoint.x, minRadiometricExposure, maxRadiometricExposure),
                calculateLog2ToLinear(inRadiometricLinearColor.b, greyPoint.x, minRadiometricExposure, maxRadiometricExposure));


                // Shape image
                float3 log2HdriPixelArray = half3(0.0, 0.0, 0.0);
                log2HdriPixelArray.r = calculateLinearToLog2(max(0.0f, linearHdriPixelColor.r), greyPoint.x, minRadiometricExposure,
                                                             maxRadiometricExposure);
                log2HdriPixelArray.g = calculateLinearToLog2(max(0.0f, linearHdriPixelColor.g), greyPoint.x, minRadiometricExposure,
                                                             maxRadiometricExposure);
                log2HdriPixelArray.b = calculateLinearToLog2(max(0.0f, linearHdriPixelColor.b), greyPoint.x, minRadiometricExposure,
                                                             maxRadiometricExposure);

                // Calculate Pixel max color and ratio
                half logHdriMaxRGBChannel = max(max(log2HdriPixelArray.r, log2HdriPixelArray.g), log2HdriPixelArray.b);
                linearHdriPixelColor = half3(
                    calculateLog2ToLinear(log2HdriPixelArray.r, greyPoint.x, minRadiometricExposure, maxRadiometricExposure),
                    calculateLog2ToLinear(log2HdriPixelArray.g, greyPoint.x, minRadiometricExposure, maxRadiometricExposure),
                    calculateLog2ToLinear(log2HdriPixelArray.b, greyPoint.x, minRadiometricExposure, maxRadiometricExposure));

                if (usePerChannel == 0)
                {
                    // Retrieve the maximum RGB value but in linear space
                    half linearHdriMaxRGBChannel = calculateLog2ToLinear(logHdriMaxRGBChannel, greyPoint.x,
                                                                         minRadiometricExposure, maxRadiometricExposure);
                    // Calculate the ratio in linear space
                    half3 ratio = linearHdriPixelColor / linearHdriMaxRGBChannel;

                    half yValue = getYCoordinateLogXInput(logHdriMaxRGBChannel);
                    yValue = calculateLog2ToLinear(yValue, greyPoint.y, minDisplayExposure, maxDisplayExposure);
                    half hdriYMaxValue = min(yValue, 1.0f);
                    hdriPixelColor.rgb = hdriYMaxValue * ratio;
                }
                else
                {
                    hdriPixelColor.r = getYCoordinateLogXInput(log2HdriPixelArray.r);
                    hdriPixelColor.g = getYCoordinateLogXInput(log2HdriPixelArray.g);
                    hdriPixelColor.b = getYCoordinateLogXInput(log2HdriPixelArray.b);
                }

                hdriPixelColor.r = remap(hdriPixelColor.r, minDisplayValue, maxDisplayValue, 0.0f, 1.0f);
                hdriPixelColor.g = remap(hdriPixelColor.g, minDisplayValue, maxDisplayValue, 0.0f, 1.0f);
                hdriPixelColor.b = remap(hdriPixelColor.b, minDisplayValue, maxDisplayValue, 0.0f, 1.0f);
                hdriPixelColor.a = 1.0f;

                return hdriPixelColor;
            }

            float max3(float3 xyz) 
            {
                return max(xyz.x, max(xyz.y, xyz.z));
            }


            fixed4 frag(v2f i) : SV_Target
            {
                half4 col = tex2D(_MainTex, i.uv);

                col = gamutMap(col);
                float maxCol = max3(col.rgb);
                if(heatmap == 0)
                {
                    return col;
                }
                else {
                    if (maxCol < controlPoints[2].y)
                    {
                        float outCol = (maxCol - controlPoints[0].y) / (controlPoints[2].y - controlPoints[0].y);
                        return float4(outCol, 0.0, 0.0, 1.0);
                    } else  if (maxCol < controlPoints[4].y)
                    {
                        float outCol = (maxCol - controlPoints[2].y) / (controlPoints[4].y - controlPoints[2].y);
                        return float4(0.0, outCol, 0.0, 1.0);
                    } else 
                    {
                        float outCol = (maxCol - controlPoints[4].y) / (controlPoints[6].y - controlPoints[4].y);
                        return float4(0.0, 0.0, outCol, 1.0);
                    }
                }
            }
            ENDCG
        }
    }
}
