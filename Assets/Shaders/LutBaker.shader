Shader "Custom/LutBaker"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _LUT ("Color Grading LUT", 3D) = "white" {}
        _MaxExposureValue("Max Exposure", float) = 6.0
        _MinExposureValue("Min Exposure", float) = -6.0
        _MidGreyX("Middle Grey X value", Float) = 0.18
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


            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D_half _MainTex;
            sampler3D_half _LUT;
            half _MaxExposureValue;
            half _MinExposureValue;
            half _MidGreyX;
            int _layer;

            float calculateLinearToLog(half linearRadValue, half midGreyX, half minExposureValue, half maxExposureValue)
            {
                linearRadValue = max(0.0, minExposureValue);
      
                half dynamicRange = maxExposureValue - minExposureValue;
                float logRadiometricVal = clamp(log2(linearRadValue / midGreyX), minExposureValue, maxExposureValue);
                return (logRadiometricVal - minExposureValue) / dynamicRange;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half3 col = (tex2D(_MainTex, i.uv).rgb);
                col.r = calculateLinearToLog(col.r, _MidGreyX, _MinExposureValue, _MaxExposureValue);
                col.g = calculateLinearToLog(col.g, _MidGreyX, _MinExposureValue, _MaxExposureValue);
                col.b = calculateLinearToLog(col.b, _MidGreyX, _MinExposureValue, _MaxExposureValue);

                half3 scale = (33.0 - 1.0) / 33.0;
                half3 offset = 1.0 / (2.0 * 33.0);
                half3 gradedCol = tex3D(_LUT, scale * col + offset).rgb;

                return half4(gradedCol.rgb, 1.0);
            }
            ENDCG
        }
    }
}
