Shader "Custom/DrawCurve"
{
    Properties
    {
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


            float4 controlPoints[7];

            // Reference: https://www.shadertoy.com/view/ltXSDB
            //
            // Test if point p crosses line (a, b), returns sign of result
            half testCross(half2 a, half2 b, half2 p)
            {
                return sign((b.y - a.y) * (p.x - a.x) - (b.x - a.x) * (p.y - a.y));
            }

            // Determine which side we're on (using barycentric parameterization)
            half signBezier(half2 A, half2 B, half2 C, half2 p)
            {
                half2 a = C - A, b = B - A, c = p - A;
                half2 bary = half2(c.x * b.y - b.x * c.y, a.x * c.y - c.x * a.y) / (a.x * b.y - b.x * a.y);
                half2 d = half2(bary.y * 0.5, 0.0) + 1.0 - bary.x - bary.y;
                return lerp(sign(d.x * d.x - d.y), lerp(-1.0, 1.0,
                                                        step(testCross(A, B, p) * testCross(B, C, p), 0.0)),
                            step((d.x - d.y), 0.0)) * testCross(A, C, B);
            }

            float3 solveCubic(float a, float b, float c)
            {
                float p = b - a * a / 3.0, p3 = p * p * p;
                float q = a * (2.0 * a * a - 9.0 * b) / 27.0 + c;
                float d = q * q + 4.0 * p3 / 27.0;
                float offset = -a / 3.0;
                if (d >= 0.0)
                {
                    float z = sqrt(d);
                    half2 x = (half2(z, -z) - q) / 2.0;
                    half oneThird = 1.0 / 3.0;
                    half2 uv = sign(x) * pow(abs(x), half2(oneThird, oneThird));
                    float res = offset + uv.x + uv.y;
                    return half3(res, res, res);
                }
                float v = acos(-sqrt(-27.0 / p3) * q / 2.0) / 3.0;
                float m = cos(v), n = sin(v) * 1.732050808;
                return half3(m + m, -n - m, n - m) * sqrt(-p / 3.0) + offset;
            }

            half sdBezier(half2 A, half2 B, half2 C, half2 p)
            {
                B = lerp(B + half2(0.0001, 0.0001), B, abs(sign(B * 2.0 - A - C)));
                half2 a = B - A, b = A - B * 2.0 + C, c = a * 2.0, d = A - p;
                half3 k = half3(3. * dot(a, b), 2. * dot(a, a) + dot(d, b), dot(d, a)) / dot(b, b);
                half3 t = clamp(solveCubic(k.x, k.y, k.z), 0.0, 1.0);
                half2 pos = A + (c + b * t.x) * t.x;
                half dis = length(pos - p);
                pos = A + (c + b * t.y) * t.y;
                dis = min(dis, length(pos - p));
                pos = A + (c + b * t.z) * t.z;
                dis = min(dis, length(pos - p));
                return dis * signBezier(A, B, C, p);
            }

            float remap(half value, half min0, half max0, half min1, half max1)
            {
                return min1 + (value - min0) * ((max1 - min1) / (max0 - min0));
            }

            float4 mixColor(float4 baseCol, float4 color, float alpha)
            {
                return float4(lerp(baseCol.rgb, color.rgb, alpha * color.a), 1.0);
            }

            float4 drawGrid(float4 baseCol, float stepSize, float4 gridCol, float2 uv)
            {
                float2 dxy = float2(0.002, 0.002);
                float mul = 1.0 / stepSize;
                float2 g = abs(float2(-0.5, -0.5) + frac((uv + float2(stepSize, stepSize) * 0.5) * mul)); // g passes 0 at stepSize intervals
                g = float2(1.0, 1.0) - smoothstep(float2(0.0, 0.0), dxy * mul * 1.5, g);
                return mixColor(baseCol, gridCol, max(g.x, g.y));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = (i.uv * 1.4) - 0.2;

                half2 p0 = controlPoints[0].xy;
                half2 p1 = controlPoints[1].xy;
                half2 p2 = controlPoints[2].xy;
                half2 p3 = controlPoints[3].xy;
                half2 p4 = controlPoints[4].xy;
                half2 p5 = controlPoints[5].xy;
                half2 p6 = controlPoints[6].xy;

                half2 origin = half2(0.0f, 0.0f);
                half2 yAxisMax = half2(0.0f, 2.0f);
                half2 xAxisMax = half2(2.0f, 0.0f);
                //p0.y = remap(p0.y, 0.0, 1.5, 0.0, 1.0);
                //p1.y = remap(p1.y, 0.0, 1.5, 0.0, 1.0);
                //p2.y = remap(p2.y, 0.0, 1.5, 0.0, 1.0);
                //p3.y = remap(p3.y, 0.0, 1.5, 0.0, 1.0);
                //p4.y = remap(p4.y, 0.0, 1.5, 0.0, 1.0);
                //p5.y = remap(p5.y, 0.0, 1.5, 0.0, 1.0);
                //p6.y = remap(p6.y, 0.0, 1.5, 0.0, 1.0);

                half3 color = half3(1.0, 1.0, 1.0);
                half3 axisColor = half3(1.0, 0.0, 0.0);
                half3 curveColor = half3(0, 0.831, 1);
                float dist = sdBezier(p0, p1, p2, uv);
                color = lerp(color, curveColor, 1.0-smoothstep(0.0,0.02,abs(dist)) );
                dist = sdBezier(p2, p3, p4, uv);
                color = lerp(color, curveColor, 1.0-smoothstep(0.0,0.02,abs(dist)) );
                dist = sdBezier(p4, p5, p6, uv);
                color = lerp(color, curveColor, 1.0-smoothstep(0.0,0.02,abs(dist)) );

                /*dist = sdBezier(origin, (xAxisMax - origin)/2.0, xAxisMax, uv);
                color = lerp(color, half3(0.0, 0.0, 0.0), 1.0 - smoothstep(0.0, 0.02, abs(dist)));
                dist = sdBezier(origin, (yAxisMax - origin) / 2.0, yAxisMax, uv);
                color = lerp(color, half3(0.0, 0.0, 0.0), 1.0 - smoothstep(0.0, 0.015, abs(dist)));*/
                // From https://www.shadertoy.com/view/ltjcWW
                color = drawGrid(half4(color, 1.0), 0.1, float4(0.0, 0.0, 0.0, 0.2), uv).rgb;
                color = drawGrid(half4(color, 1.0), 1.0, float4(0.0, 0.0, 0.0, 0.8), uv).rgb;

                return half4(color, 1.0);
            }
            ENDCG
        }
    }
}
