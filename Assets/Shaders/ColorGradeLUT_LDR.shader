Shader "Custom/ColorGradeLUT_LDR"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _LUT("LUT", 2D) = "white" {}
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

            // #define CELL_SIZE 16.0

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

            sampler2D _MainTex;
            sampler2D _LUT;
            half4 _LUT_TexelSize;
            half _Contribution;
            float4 frag (v2f i) : SV_Target
            {
                float CELL_SIZE = 16.0;
                float maxColor = (CELL_SIZE - 1.0);
                float4 col = saturate(tex2D(_MainTex, i.uv));            

                float halfTexelOffsetX = 0.5 / _LUT_TexelSize.z;
                float halfTexelOffsetY = 0.5 / _LUT_TexelSize.w;
                float cellIndex = maxColor / CELL_SIZE;
 
                float xOffset = halfTexelOffsetX + ((col.r / CELL_SIZE) * cellIndex);
                float yOffset = halfTexelOffsetY + (col.g * cellIndex);

                float cell = col.b * maxColor;
                float cellLeft = floor(cell);
                float cellRight = ceil(cell);

                float2 leftUV  = float2(cellLeft /CELL_SIZE + xOffset, yOffset);
                float2 rightUV = float2(cellRight/CELL_SIZE + xOffset, yOffset);
 
                float4 gradedTexelLeft = tex2D(_LUT, leftUV);
                float4 gradedTexelRight = tex2D(_LUT, rightUV);

                float4 gradedTexel = lerp(gradedTexelLeft, gradedTexelRight, frac(cell));
                gradedTexel.a = col.a;
                return gradedTexel;

            }
            ENDCG
        }
    }
}
