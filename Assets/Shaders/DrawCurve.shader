Shader "Custom/DrawCurve"
{
    Properties
    {
        //scaleFactor("Curve Scale", Float) = 1.0
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

 /*           float xCoords[1024];
            float yCoords[1024];*/
            float4 controlPoints[7];
            //float scaleFactor;

            float det(half2 a, half2 b)
            {
                return a.x * b.y - b.x * a.y;
            }

            half2 closestPointInSegment( half2 a, half2 b )
            {
              half2 ba = b - a;
              return a + ba*clamp( -dot(a,ba)/dot(ba,ba), 0.0, 1.0 );
            }
            
            half2 get_distance_vector(half2 b0, half2 b1, half2 b2)
            {
                float a = det(b0, b2), b = 2.0 * det(b1, b0), d = 2.0 * det(b2, b1);
                float f = b * d - a * a;
                half2 d21 = b2 - b1, d10 = b1 - b0, d20 = b2 - b0;
                half2 gf = 2.0 * (b * d21 + d * d10 + a * d20);
                gf = half2(gf.y, -gf.x);
                half2 pp = -f * gf / dot(gf, gf);
                half2 d0p = b0 - pp;
                float ap = det(d0p, d20), bp = 2.0 * det(d10, d0p);
                // (note that 2*ap+bp+dp=2*a+b+d=4*area(b0,b1,b2))
                float t = clamp((ap + bp) / (2.0 * a + b + d), 0.0, 1.0);
                return lerp(lerp(b0, b1, t), lerp(b1, b2, t), t);
            }

            float approx_distance(half2 p, half2 b0, half2 b1, half2 b2)
            {
                return length(get_distance_vector(b0 - p, b1 - p, b2 - p));
            }

            float remap(float value, float min0, float max0, float min1, float max1)
            {
                return min1 + (value - min0) * ((max1 - min1) / (max0 - min0));
            }

            float SDFCircle(float2 coords, float2 offset)
            {
                float EDGE = 0.005;

                coords -= offset;
                float v = coords.x * coords.x + coords.y * coords.y - EDGE * EDGE;
                float2 g = float2(2.0 * coords.x, 2.0 * coords.y);
                return v / length(g);
            }

            half3 drawControlPoints(half3 currentPixelColor, half2 controlPoint, half2 uv, half3 controlPtColor)
            {
                float EDGE = 0.005;
                float SMOOTH = 0.0025;
                float3 color = currentPixelColor;
                float dist = SDFCircle(uv, controlPoint);
                if (dist < EDGE + SMOOTH)
                {
                    dist = max(dist, 0.0);
                    dist = smoothstep(EDGE,EDGE + SMOOTH,dist);
                    color *= lerp(controlPtColor,float3(1.0,1.0,1.0),dist);
                }
                return color;
            }

            
            
            fixed4 frag(v2f i) : SV_Target
            {
                half2 p0 = controlPoints[0].xy;
                p0.y = remap(p0.y, 0.0, 1.5, 0.0, 1.0);
                half2 p1 = controlPoints[1].xy;
                p1.y = remap(p1.y, 0.0, 1.5, 0.0, 1.0);
                half2 p2 = controlPoints[2].xy;
                p2.y = remap(p2.y, 0.0, 1.5, 0.0, 1.0);
                half2 p3 = controlPoints[3].xy;
                p3.y = remap(p3.y, 0.0, 1.5, 0.0, 1.0);
                half2 p4 = controlPoints[4].xy;
                p4.y = remap(p4.y, 0.0, 1.5, 0.0, 1.0);
                half2 p5 = controlPoints[5].xy;
                p5.y = remap(p5.y, 0.0, 1.5, 0.0, 1.0);
                half2 p6 = controlPoints[6].xy;
                p6.y = remap(p6.y, 0.0, 1.5, 0.0, 1.0);

                half3 color = half3(1.0, 1.0, 1.0);
                
                float EDGE = 0.005;
                float SMOOTH = 0.0025;
                float dist = approx_distance(i.uv, p0, p1, p2);
                if (dist < EDGE + SMOOTH)
                {
                    dist = smoothstep(EDGE - SMOOTH, EDGE + SMOOTH, dist);
                    color *= half3(dist, dist, dist);
                }
                dist = approx_distance(i.uv, p2, p3, p4);
                if (dist < EDGE + SMOOTH)
                {
                    dist = smoothstep(EDGE - SMOOTH, EDGE + SMOOTH, dist);
                    if(dist < 1.0)
                        dist = 1.0;
                    color *= half3(dist, dist, dist);
                }
                dist = approx_distance(i.uv, p4, p5, p6);
                if (dist < EDGE + SMOOTH)
                {
                    dist = smoothstep(EDGE - SMOOTH, EDGE + SMOOTH, dist);
                    color *= half3(dist, dist, dist);
                }

            color = drawControlPoints(color, p2, i.uv, half3(1.0, 0.0, 0.0));
            color = drawControlPoints(color, p3, i.uv, half3(0.0, 1.0, 0.0));
            color = drawControlPoints(color, p4, i.uv, half3(0.0, 0.0, 1.0));



                
                return half4(color, 1.0);
            }
            ENDCG
        }
    }
}