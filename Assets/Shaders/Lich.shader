Shader "Custom/Lich"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        exposure("Exposure Value(EV)", Float) = 0.0
        greyPoint("Middle Grey Value(XY)", Vector) = (0.18, 0.18, 0.0, 0.0)
        minRadiometricExposure("Minimum Radiometric Exposure Value(EV)", Float) = -6.0
        maxRadiometricExposure("Maximum Radiometric Exposure Value(EV)", Float) = 6.0
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
            float exposure;
            float4 greyPoint;
            float minRadiometricExposure;
            float maxRadiometricExposure;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.vertex);
                o.uv = v.uv;
                return o;
            }

            //Note that luminance here is the closed domain range, and relative to
            //the closed domain of the working domain.For example, 1.0 would be maximal
            //luminance in both SDR and EDR cases, but care must be taken to assert that
            //the meaning of the luminance chosen matches assumptions.
            float3 luminanceScaling(float3 inputColour, float luminanceRelativeTarget)
            {
                float3 luminanceWeights = float3(0.2126, 0.7152, 0.0722);
                // Troy Sobotka, 2021, "EVILS - Exposure Value Invariant Luminance Scaling"
                // https://colab.research.google.com/drive/1iPJzNNKR7PynFmsqSnQm3bCZmQ3CvAJ-#scrollTo=psU43hb-BLzB

                float maximalChroma = max(inputColour.x, max(inputColour.y, inputColour.z));
                float luminanceIn = dot(inputColour, luminanceWeights);

                float3 chromaRatio = max(inputColour / maximalChroma, 0.0f);
                float chromaRatioLuminance = dot(chromaRatio, luminanceWeights);

                float3 maxReserves = 1.0f - chromaRatio;
                float maxReservesLuminance = dot(maxReserves, luminanceWeights);

                float luminanceDifference = max(luminanceRelativeTarget - chromaRatioLuminance, 0.0f);
                float scaledLuminanceDifference = luminanceDifference / max(maxReservesLuminance, 0.0001);

                float chromaScale = (luminanceRelativeTarget - luminanceDifference) / max(chromaRatioLuminance, 0.0001);

                return chromaScale * chromaRatio + scaledLuminanceDifference * maxReserves;

                //// Troy Sobotka, 2021, "EVILS - Exposure Value Invariant Luminance Scaling"
                //// https://colab.research.google.com/drive/1iPJzNNKR7PynFmsqSnQm3bCZmQ3CvAJ-#scrollTo=psU43hb-BLzB

                //float luminanceIn = dot(inputColour, luminanceWeights);

                //// TODO: We could optimize for the case of single-channel luminance
                //float luminanceOut = toneMapper(luminanceIn).x;

                //float peak = max(x);
                //float3 chromaRatio = max(x / peak, 0.0f);

                //float chromaRatioLuminance = dot(chromaRatio, luminanceWeights);

                //float3 maxReserves = 1.0f - chromaRatio;
                //float maxReservesLuminance = dot(maxReserves, luminanceWeights);

                //float luminanceDifference = max(luminanceRelativeTarget - chromaRatioLuminance, 0.0f);
                //float scaledLuminanceDifference = luminanceDifference / max(maxReservesLuminance, 0.0001);


                //float chromaScale = (luminanceRelativeTarget - luminanceDifference) / max(chromaRatioLuminance, 0.0001);


                //return chromaScale * chromaRatio + scaledLuminanceDifference * maxReserves;

            }

            float calculateLinearToLog2(float linearRadValue, float midGreyX, float minExposureValue, float maxExposureValue)
            {
                if (linearRadValue < 0.0f)
                    linearRadValue = minExposureValue;

                float dynamicRange = maxExposureValue - minExposureValue;
                float logRadiometricVal = clamp(log2(linearRadValue / midGreyX), minExposureValue, maxExposureValue);

                return saturate((logRadiometricVal - minExposureValue) / dynamicRange);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float4 col = tex2D(_MainTex, i.uv);
                // expose luminanceRelativeTarget
                col.r = calculateLinearToLog2(col.r, greyPoint.x, minRadiometricExposure, maxRadiometricExposure);
                col.g = calculateLinearToLog2(col.r, greyPoint.x, minRadiometricExposure, maxRadiometricExposure);
                col.b = calculateLinearToLog2(col.r, greyPoint.x, minRadiometricExposure, maxRadiometricExposure);

                return float4(luminanceScaling(col.rgb, 0.5), 1.0);
            }
            ENDCG
        }
    }
}
