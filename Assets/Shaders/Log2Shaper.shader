Shader "Custom/Log2Shaper"
{
     Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _MaxExposureValue ("Max Exposure", float) = 6.0
        _MinExposureValue ("Min Exposure", float) = -8.0
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
        
            half4 frag(v2f i) : SV_Target
            {
                half4 col = tex2D(_MainTex, i.uv);
                col.r = calculateLinearToLog(col.r, _MidGreyX, _MinExposureValue, _MaxExposureValue);
                col.g = calculateLinearToLog(col.g, _MidGreyX, _MinExposureValue, _MaxExposureValue);
                col.b = calculateLinearToLog(col.b, _MidGreyX, _MinExposureValue, _MaxExposureValue);
                
                return col;
            }
            ENDCG
        }
    }
}
