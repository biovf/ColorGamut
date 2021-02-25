Shader "Custom/ColorGradeLUT3D_HDR"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _LUT ("Color Grading LUT", 3D) = "white" {}
        _MaxExposureValue ("Max Exposure", float) = 6.0
        _MinExposureValue ("Min Exposure", float) = -6.0
        _MidGreyX ("Middle Grey X value", Float) = 0.18
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
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

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D_half _MainTex;
            sampler3D_half _LUT;
            float _MaxExposureValue;
            float _MinExposureValue;
            float _MidGreyX;

             float calculateLinearToLog(float linearRadValue, float midGreyX, float minExposureValue, float maxExposureValue)
            {
                if (linearRadValue < 0.0f)
                    linearRadValue = minExposureValue;

                float dynamicRange = maxExposureValue - minExposureValue;
                float logRadiometricVal = clamp(log2(linearRadValue / midGreyX), minExposureValue, maxExposureValue);
                return (logRadiometricVal - minExposureValue) / dynamicRange;
            }

             float inverseSrgbEOTF(float inputValue)
             {
                 return  (inputValue <= 0.0031308) ? inputValue * 12.92f : 1.055f * pow(inputValue, 1.0f / 2.4f) - 0.055f;
             }

             float sRgbEOTF(float inputValue) 
             {
                 return (inputValue <= inverseSrgbEOTF(0.0031308)) ?  inputValue / 12.92 :
                                                                      pow((inputValue + 0.055) / 1.055, 2.4);
             }

            float forwardSrgb2PartEOTF(float inputValue)
            {
                return inputValue <= 0.04045f ? inputValue / 12.92f : pow((inputValue + 0.055f) / 1.055f, 2.4f);
            }

            half4 frag(v2f i) : SV_Target
            {
                half3 col = (tex2D(_MainTex, i.uv).rgb);
                half3 scale = (33.0 - 1.0) / 33.0;
                half3 offset = 1.0 / (2.0 * 33.0);
                half3 gradedCol = tex3D(_LUT, scale * col + offset).rgb;

                return half4(gradedCol.rgb, 1.0);
            }
            ENDCG
        }
    }
}
