Shader "Custom/ReinhardToneMap"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
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

            half3 Tonemap_ACES(const half3 x) {
                // Narkowicz 2015, "ACES Filmic Tone Mapping Curve”
                const half a = 2.51;
                const half b = 0.03;
                const half c = 2.43;
                const half d = 0.59;
                const half e = 0.14;
                return (x * (a * x + b)) / (x * (c * x + d) + e);
            }
            
            half4 frag (v2f i) : SV_Target
            {
                half srgbEotf = 1.0 / 2.2;
                half4 col = tex2D(_MainTex, i.uv);
                // col.rgb = Tonemap_ACES(col.rgb);
                col.rgb = col.rgb / (col.rgb + half3(1.0, 1.0, 1.0));
                
                return col;
            }
            ENDCG
        }
    }
}
