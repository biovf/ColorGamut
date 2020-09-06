Shader "Custom/ColorGradeLUT3D_HDR"
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

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D_half _MainTex;
            sampler3D_half _LUT;

            // SMPTE ST.2084 (PQ) transfer functions
            // 1.0 = 100nits, 100.0 = 10knits
            #define DEFAULT_MAX_PQ 100.0

            struct ParamsPQ
            {
                float N, M;
                float C1, C2, C3;
            };

            static const ParamsPQ PQ =
            {
                2610.0 / 4096.0 / 4.0, // N
                2523.0 / 4096.0 * 128.0, // M
                3424.0 / 4096.0, // C1
                2413.0 / 4096.0 * 32.0, // C2
                2392.0 / 4096.0 * 32.0, // C3
            };

            float3 LinearToPQ(float3 x, float maxPQValue)
            {
                x = pow(x / maxPQValue, PQ.N);
                float3 nd = (PQ.C1 + PQ.C2 * x) / (1.0 + PQ.C3 * x);
                return pow(nd, PQ.M);
            }

            float3 PQToLinear(float3 x, float maxPQValue)
            {
                x = pow(x, rcp(PQ.M));
                float3 nd = max(x - PQ.C1, 0.0) / (PQ.C2 - (PQ.C3 * x));
                return pow(nd, rcp(PQ.N)) * maxPQValue;
            }

            half4 frag(v2f i) : SV_Target
            {
                half3 col = (tex2D(_MainTex, i.uv).rgb);
                col = LinearToPQ(col, 100.0);
                
                half3 scale = (33.0 - 1.0) / 33.0;
                half3 offset = 1.0 / (2.0 * 33.0);
                // half3 uvw = col * half3(32.0, 32.0, 32.0) * half3(1.0/33.0,1.0/33.0,1.0/33.0)  + half3(1.0/33.0,1.0/33.0,1.0/33.0) * 0.5;
                half4 finalCol = tex3D(_LUT, scale * col + offset);

                finalCol.rgb = PQToLinear(finalCol, 100.0);

                // half srgbEotf = 1.0 / 2.2;
                // finalCol.rgb = pow(finalCol.rgb, half3(srgbEotf, srgbEotf, srgbEotf));
                return finalCol;
            }
            ENDCG
        }
    }
}