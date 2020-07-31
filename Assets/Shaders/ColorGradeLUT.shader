Shader "Custom/ColorGradeLUT"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _LUT("LUT", 2D) = "white" {}
        _Contribution("Contribution", Range(0, 1)) = 1
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

            #define SLICE_DIM 32.0

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
            fixed4 frag (v2f i) : SV_Target
            {
                float maxColor = SLICE_DIM - 1.0;
                fixed4 col = (tex2D(_MainTex, i.uv));

                float halfColX = 0.5 / 1024.0;//_LUT_TexelSize.z;
                float halfColY = 0.5 / 32.0;//_LUT_TexelSize.w;
                float threshold = maxColor / SLICE_DIM;
 
                float xOffset = halfColX + (col.r / SLICE_DIM) * threshold ;
                float yOffset = halfColY + col.g * threshold;

                float cell = col.b * maxColor;
                float cellLeft = floor(cell);
                float cellRight = ceil(cell);

                float2 lutPosLeft = float2(cellLeft/SLICE_DIM + xOffset, yOffset);
                float2 lutPosRight = float2(cellRight/SLICE_DIM + xOffset, yOffset);
 
                // float2 lutPos = float2(cell / SLICE_DIM + xOffset, yOffset);
                float4 gradedColRight = tex2D(_LUT, lutPosLeft);
                float4 gradedColLeft = tex2D(_LUT, lutPosRight);

                float4 gradedCol = lerp(gradedColLeft, gradedColRight, frac(cell));
                gradedCol.a = 1.0;
                return lerp(col, gradedCol, _Contribution);
                 //return float4(lutPosRight.x, lutPosRight.y, 0.0, 1.0);
            }
            ENDCG
        }
    }
}
