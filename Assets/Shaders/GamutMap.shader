Shader "Custom/GamutMap"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        exposure ("Exposure Value(EV)", Float) = 0.0
        greyPoint ("Middle Grey Value(XY)", Vector) = (0.18, 0.18, 0.0, 0.0)
        minRadiometricExposure ("Minimum Radiometric Exposure Value(EV)", Float) = -6.0
        maxRadiometricExposure ("Maximum Radiometric Exposure Value(EV)", Float) = 6.0
        minDisplayValue ("Min Display Value (unit agnostic)", Float) = 0.0
        maxDisplayValue ("Max Display Value (unit agnostic)", Float) = 1.0
        maxRadiometricValue ("Maximum Radiometric Value", Float) = 12.0
        maxLatitudeLimit ("Start value for Gamut compression", Float) = 0.8
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
            half minRadiometricExposure;
            half maxRadiometricExposure;
            half minDisplayValue;
            half maxDisplayValue;
            half maxRadiometricValue;
            half maxLatitudeLimit;

            int inputArraySize;
            int usePerChannel;

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
                // Terrible horrible hack since there is not MaxValue for a float variable
                float closest = 9999999.0;
                float minDifference = 9999999.0;
                float prevDifference = 9999999.0;

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

            half3 calculateGamutCompression(half3 linearHdriPixelColor, half3 ratio, half linearHdriMaxRGBChannel)
            {
                half3 newRatio = ratio;
                float gamutCompressionXCoordLinear = 0.0f; // Intersect of x on Y = 1

                // Calculate gamut compression values by iterating through the Y values array and returning the closest x coord
                gamutCompressionXCoordLinear = calculateLog2ToLinear(
                    getXCoordinate(maxLatitudeLimit), greyPoint.x, minRadiometricExposure, maxRadiometricExposure);

                if (linearHdriPixelColor.r > gamutCompressionXCoordLinear ||
                    linearHdriPixelColor.g > gamutCompressionXCoordLinear ||
                    linearHdriPixelColor.b > gamutCompressionXCoordLinear)
                {
                    half gamutCompressionRange = maxRadiometricValue - gamutCompressionXCoordLinear;
                    half gamutCompressionRatio = (max(linearHdriPixelColor.r,
                                                      max(linearHdriPixelColor.g, linearHdriPixelColor.b)) -
                            gamutCompressionXCoordLinear) /
                        gamutCompressionRange;

                   ratio = lerp(ratio, half3(1.0, 1.0, 1.0), gamutCompressionRatio);
                }
                return newRatio;
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

            float4 gamutMap(half4 inColor)
            {
                half4 hdriPixelColor = half4(0.0, 0.0, 0.0, 1.0);
                half4 colorExposed = inColor * pow(2.0, exposure);
                // Shape image
                half3 log2HdriPixelArray = half3(0.0, 0.0, 0.0);
                log2HdriPixelArray.r = calculateLinearToLog2(max(0.0f, colorExposed.r), greyPoint.x, minRadiometricExposure,
                                                             maxRadiometricExposure);
                log2HdriPixelArray.g = calculateLinearToLog2(max(0.0f, colorExposed.g), greyPoint.x, minRadiometricExposure,
                                                             maxRadiometricExposure);
                log2HdriPixelArray.b = calculateLinearToLog2(max(0.0f, colorExposed.b), greyPoint.x, minRadiometricExposure,
                                                             maxRadiometricExposure);

                // Calculate Pixel max color and ratio
                half logHdriMaxRGBChannel = max(max(log2HdriPixelArray.r, log2HdriPixelArray.g), log2HdriPixelArray.b);
                half3 linearHdriPixelColor = half3(
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
                    half minDisplayExposure = log2(minDisplayValue / greyPoint.y);
                    half maxDisplayExposure = log2(maxDisplayValue / greyPoint.y);
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

            fixed4 frag(v2f i) : SV_Target
            {
                half4 col = tex2D(_MainTex, i.uv);
                return gamutMap(col);
            }
            ENDCG
        }
    }
}
