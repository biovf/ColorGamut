Shader "Custom/LutBaker"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _LUT ("Color Grading LUT", 3D) = "white" {}
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

            int _layer;

            fixed4 frag (v2f i) : SV_Target
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
