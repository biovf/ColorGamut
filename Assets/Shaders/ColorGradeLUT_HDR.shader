Shader "Custom/ColorGradeLUT_HDR"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _LUT("LUT", 2D) = "white" {}
    }
    SubShader
    {
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

            #define CELL_SIZE 32.0
            
            sampler2D _MainTex;
            sampler2D_half _LUT;
            float4 _LUT_TexelSize;
            half _Contribution;

            half4 frag (v2f i) : SV_Target
            {
                //half CELL_SIZE = 32.0;

                half maxColor = (CELL_SIZE - 1.0);
                half4 col = (tex2D(_MainTex, i.uv));            

                float halfTexelOffsetX = 0.5 / _LUT_TexelSize.z;
                float halfTexelOffsetY = 0.5 / _LUT_TexelSize.w;
                half cellIndex = maxColor / CELL_SIZE;
 
                float xOffset = halfTexelOffsetX + ((col.r / CELL_SIZE) * cellIndex);
                float yOffset = halfTexelOffsetY + (col.g * cellIndex);

                half cell = col.b * maxColor;
                half cellLeft = floor(cell);
                half cellRight = ceil(cell);

                float2 leftUV  = float2(cellLeft /CELL_SIZE + xOffset, yOffset);
                float2 rightUV = float2(cellRight/CELL_SIZE + xOffset, yOffset);
 
                half4 gradedTexelLeft   = tex2D(_LUT, leftUV);
                half4 gradedTexelRight  = tex2D(_LUT, rightUV);

                half4 gradedTexel = lerp(gradedTexelLeft, gradedTexelRight, frac(cell));
                gradedTexel.a = col.a;

                return gradedTexel;

            }
            ENDCG
        }
    }
}
