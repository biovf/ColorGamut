Shader "Custom/ChromaticityCompression"
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
        inputArraySize("Number of curve array elements", Int) = 1024

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
                float4 screenPos : TEXCOORD1;
            };

            sampler2D_half _MainTex;
            float4 greyPoint;
            float minRadiometricExposure;
            float maxRadiometricExposure;
            float maxRadiometricValue;
            float chromaticityMaxLowerBoundLatitude;
            float gamutCompressionRatioPowerLowerBound;
            float exposure;
            int inputArraySize;

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
                o.screenPos = ComputeScreenPos(o.vertex);
                o.uv = v.uv;
                return o;
            }

            float easeOutExpo(float x)
            {
                return x == 1 ? 1 : 1 - pow(2, -10 * x);
            }

            float easeOutCirc(float x)
            {
                return sqrt(1 - pow(x - 1, 2));
            }
# define PI           3.14159265358979323846  /* pi */

            float easeOutSine(float x)
            {
                return sin((x * PI) / 2);
            }

            float easeOutQuad(float x)
            {
                return 1 - (1 - x) * (1 - x);
            }

            float easeOutCubic(float x)
            {
                return 1 - pow(1 - x, 3);
            }

            float easeInCubic(float x)
            {
                return x * x * x;
            }

            float easeInQuad(float x)
            {
                return x * x;
            }

            float easeInSin(float x)
            {
                return 1 - cos((x * 3.14) / 2);
            }

            float easeOutQuart(float x)
            {
                return 1 - pow(1 - x, 4);
            }

            float easeInOutSine(float x)
            {
                return -(cos(PI * x) - 1) / 2;
            }

            float easeInOutExpo(float x)
            {
                return x == 0
                    ? 0
                    : x == 1
                    ? 1
                    : x < 0.5 ? pow(2, 20 * x - 10) / 2
                    : (2 - pow(2, -20 * x + 10)) / 2;
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

            float3 calculateGamutCompression(float4 inRadiometricLinearColor, float3 inputRatio)
            {

                float3 luminanceWeights = float3(0.2126, 0.7152, 0.0722);
                float3 ratio = inputRatio;
                // Calculate gamut compression values by iterating through the Y values array and returning the closest x coord
                float gamutCompressionXCoordLinearLowerBound = calculateLog2ToLinear(chromaticityMaxLowerBoundLatitude, greyPoint.x,
                    minRadiometricExposure, maxRadiometricExposure);

                    if (inRadiometricLinearColor.r > gamutCompressionXCoordLinearLowerBound ||
                        inRadiometricLinearColor.g > gamutCompressionXCoordLinearLowerBound ||
                        inRadiometricLinearColor.b > gamutCompressionXCoordLinearLowerBound)
                {
                    float maxRadiometricLinearChannel = max(inRadiometricLinearColor.r, max(inRadiometricLinearColor.g, inRadiometricLinearColor.b));

                    float gamutCompressionRange = maxRadiometricValue - gamutCompressionXCoordLinearLowerBound;
                    float gamutCompressionRatio = (maxRadiometricLinearChannel - gamutCompressionXCoordLinearLowerBound) /
                                            gamutCompressionRange;
                  
                    if (false) {
                        ratio.r = lerp(inputRatio.r, 1.0,  pow(gamutCompressionRatio, gamutCompressionRatioPowerLowerBound));
                        ratio.g = lerp(inputRatio.g, 1.0,  pow(gamutCompressionRatio, gamutCompressionRatioPowerLowerBound));
                        ratio.b = lerp(inputRatio.b, 1.0,  pow(gamutCompressionRatio, gamutCompressionRatioPowerLowerBound));
                    }
                    else {
                        
                        // TODO: expose luminance ratios because these ones are for sRGB
                        float dechroma = dot(inputRatio, float3(0.28, 0.65, 0.07));
                        float3 dechromaRate = float3(gamutCompressionRatio, gamutCompressionRatio, gamutCompressionRatio);
                        dechromaRate = dechromaRate * float3(0.2126, 0.7152, 0.0722);

                        if (true)
                        {
                            ratio.r = lerp(inputRatio.r, 1.0, (pow(gamutCompressionRatio, 1.0 - dechroma)));
                            ratio.g = lerp(inputRatio.g, 1.0, (pow(gamutCompressionRatio, 1.0 - dechroma)));
                            ratio.b = lerp(inputRatio.b, 1.0, (pow(gamutCompressionRatio, 1.0 - dechroma)));
                        }
                        else {
                            ratio.r = lerp(inputRatio.r, dechromaRate, pow(gamutCompressionRatio, gamutCompressionRatioPowerLowerBound));
                            ratio.g = lerp(inputRatio.g, dechromaRate, pow(gamutCompressionRatio, gamutCompressionRatioPowerLowerBound));
                            ratio.b = lerp(inputRatio.b, dechromaRate,  pow(gamutCompressionRatio, gamutCompressionRatioPowerLowerBound));
                        }
                    
                    }
                }
                 
                return ratio;
            }

            bool Approximately(float a, float b) 
            {
                if (a > b - 0.0001 && a <= b + 0.0001)
                    return true;
                
                return false;
            }


            float3 calculateLuminanceGamutCompression(float4 inRadiometricLinearColor, float3 inputRatio, float minCameraLuminance, float maxCameraLuminance)
            {
                float3 luminanceWeights = float3(0.2126, 0.7152, 0.0722);
                float3 ratio = float3(0.0, 0.0, 0.0);

                float maxInputRatio = max(inputRatio.r, max(inputRatio.g, inputRatio.b));
                float totalChromaRange = (1.0 - chromaticityMaxLowerBoundLatitude);
                float gamutCompressionRatio = (maxInputRatio - chromaticityMaxLowerBoundLatitude) ;

                float luminanceRatioValue = dot(luminanceWeights, inputRatio);
                float3 luminanceRatio = float3(luminanceRatioValue, luminanceRatioValue, luminanceRatioValue);
                ratio = ((1.0 - gamutCompressionRatio) * inputRatio) + ((gamutCompressionRatio)*luminanceRatio);

                return ratio;
            }

            // Input is assumed to be radiometric linear
            // Output is encoded as Log2 camera intrinsic
            float4 luminanceCompression(float4 inRadiometricLinearColor)
            {
                float4 hdriPixelColor = inRadiometricLinearColor * pow(2.0, exposure);

                float maxLinearPixelColor = max(hdriPixelColor.r, max(hdriPixelColor.g, hdriPixelColor.b));
                float3 ratio = hdriPixelColor / maxLinearPixelColor;

                float minCameraLuminance = calculateLog2ToLinear(minRadiometricExposure, greyPoint.x, minRadiometricExposure, maxRadiometricExposure);
                float maxCameraLuminance = calculateLog2ToLinear(maxRadiometricExposure, greyPoint.x, minRadiometricExposure, maxRadiometricExposure);

                ratio = calculateGamutCompression(hdriPixelColor, ratio);
                hdriPixelColor.rgb = maxLinearPixelColor * ratio;
                
                hdriPixelColor.r = calculateLinearToLog2(hdriPixelColor.r, greyPoint.x, minRadiometricExposure, maxRadiometricExposure);
                hdriPixelColor.g = calculateLinearToLog2(hdriPixelColor.g, greyPoint.x, minRadiometricExposure, maxRadiometricExposure);
                hdriPixelColor.b = calculateLinearToLog2(hdriPixelColor.b, greyPoint.x, minRadiometricExposure, maxRadiometricExposure);
                hdriPixelColor.a = 1.0f;

                return hdriPixelColor;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float4 col = tex2D(_MainTex, i.uv);
                return luminanceCompression(col);
            }
            ENDCG
        }
    }
}
