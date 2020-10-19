Shader "Custom/ColorGradeLUT3D_HDR"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _LUT ("Color Grading LUT", 3D) = "white" {}
        _MaxExposureValue ("Max Exposure", Int) = 6
        _MinExposureValue ("Min Exposure", Int) = -8
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
        
            half4 frag(v2f i) : SV_Target
            {
                half3 col = (tex2D(_MainTex, i.uv).rgb);
                col.rgb = clamp(col.rgb, 0.0, 1.0) * (_MaxExposureValue - _MinExposureValue) + _MinExposureValue;
                col.rgb = pow(2.0f, col.rgb) * _MidGreyX;
                
                half3 scale = (33.0 - 1.0) / 33.0;
                half3 offset = 1.0 / (2.0 * 33.0);
                // half3 uvw = col * half3(32.0, 32.0, 32.0) * half3(1.0/33.0,1.0/33.0,1.0/33.0)  + half3(1.0/33.0,1.0/33.0,1.0/33.0) * 0.5;
                col = tex3D(_LUT, scale * col + offset).rgb;

                // half srgbEotf = 1.0 / 2.2;
                // finalCol.rgb = pow(finalCol.rgb, half3(srgbEotf, srgbEotf, srgbEotf));
                return half4(col, 1.0);
            }
            ENDCG
        }
    }
}