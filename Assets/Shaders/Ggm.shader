Shader "Custom/Ggm"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _DoGamutMap("Activate Gamut Mapping", Float) = 0.0
        [Toggle(ENABLE_EXPOSURE)]
        _ExposureControl("Exposure Value", Float) = 1.0
    //[Toggle (ENABLE_TANHCOMPRESSION)]
        _TanHCompression ("TanH Compression Function", Float) = 0.0
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
            #pragma shader_feature ENABLE_EXPOSURE
            //#pragma shader_feature ENABLE_TANHCOMPRESSION

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

            sampler2D _MainTex;
            float _DoGamutMap;
            float _ExposureControl;
            float _TanHCompression;

            float3 tanhCompressionFunction(float3 x, float a, float b)
            {
                return (a + b * tanh((x - a) / b));
            }

            float tanhCompressionFunction(float x, float a, float b)
            {
                return (a + b * tanh((x - a) / b));
            }

            float remap(float value, float min0, float max0, float min1, float max1)
            {
                return min1 + (value - min0) * ((max1 - min1) / (max0 - min0));
            }


            float3 colorRemap(float3 col, float channel) 
            {
                float red = col.r;
                float green = col.g;
                float blue = col.b;
                float3 outColor = float3(0.0, 0.0, 0.0);
                if (channel == 0.0) // 0.0 corresponds to red
                {
                    float newRangeMin = remap(clamp(col.r, 0.0, 1.0), 1.0, 0.85, 1.0, 0.0); // how far between 0.85 and 1 are we? Remap it to 1.0 to 0.0

                    if (_TanHCompression > 0.1)
                    {                                           
                        newRangeMin = tanhCompressionFunction(newRangeMin, 0.0, 1.0);
                    }
                    green = (col.r != col.g)  ? lerp(green, col.r, newRangeMin) : col.g;
                    blue  = (col.r != col.b)  ? lerp(blue, col.r, newRangeMin) : col.b;

                    outColor = float3(col.r, green, blue);
                }
                else if (channel == 1.0) // 1.0 corresponds to green
                {
                    float newRangeMin = remap(clamp(col.g, 0.0, 1.0), 1.0, 0.85, 1.0, 0.0); // how far between 0.85 and 1 are we? Remap it to 1.0 to 0.0
                    if (_TanHCompression > 0.1)
                    {
                        newRangeMin = tanhCompressionFunction(newRangeMin, 0.0, 1.0);
                    }

                    red =  (col.g != col.r) ? lerp(red, col.g, newRangeMin) : col.r;
                    blue = (col.g != col.b) ? lerp(blue, col.g, newRangeMin) : col.b;

                    outColor = float3(red, col.g, blue);
                }
                else if (channel == 2.0) // 2.0 corresponds to blue
                {
                    float newRangeMin = remap(clamp(col.b, 0.0, 1.0), 1.0, 0.85, 1.0, 0.0); // how far between 0.85 and 1 are we? Remap it to 1.0 to 0.0


                    if (_TanHCompression > 0.1)
                    {
                        newRangeMin = tanhCompressionFunction(newRangeMin, 0.0, 1.0);
                    }

                    red   = (col.b != col.r) ? lerp(red, col.b, newRangeMin)   : col.r;
                    green = (col.b != col.g) ? lerp(green, col.b, newRangeMin) : col.g;
                    outColor = float3(red, green, col.b);
                }

                return float3(clamp(outColor.r, 0.0, 1.0), clamp(outColor.g, 0.0, 1.0), clamp(outColor.b, 0.0, 1.0));
            }
            


            fixed4 frag (v2f i) : SV_Target
            {
                  half4 col = tex2D(_MainTex, i.uv);
              
#ifdef ENABLE_EXPOSURE
                  col *= _ExposureControl;
#else
                  col = col;
#endif
                  if (_DoGamutMap == 0.0)
                      return col;

                  // We're saturating so clip and go to white
            /*      if (col.r > 0.99 || col.g > 0.99 || col.b > 0.99)
                  {
                      return half4(1.0, 1.0, 1.0, 1.0);
                  }*/

                  //float aboveThreshold[3];
                  float channelMask[3];
                  float val = max(col.r, max(col.g, col.b));
                  float3 aboveThreshold = float3(0.0, 0.0, 0.0);
                  aboveThreshold[0] = step(0.85, col.r);
                  aboveThreshold[1] = step(0.85, col.g);
                  aboveThreshold[2] = step(0.85, col.b);

                  //for (int i = 0; i < 3; ++i) 
                  //{
                  //    if (aboveThreshold[i] == 0.0)
                  //        continue;

                  //    if (col[i] == val) // Red is above 0.85
                  //    {
                  //        // Check if Green and Blue are also
                  //        if (aboveThreshold[(i + 1) % 2] == 1) 
                  //        {
                  //          
                  //        }
                  //    }
                  //}

          /*        if (col.r == 1.0 && col.g == 1.0 && col.b == 1.0)
                      return col;*/

                  // check if any other channels are the same
                  if (val > 0.85) 
                  {
                      if (val == col.r) 
                      {
                          col = float4(colorRemap(col, 0.0), 1.0);
                      }
                      else if (val == col.g) 
                      {
                          col = float4(colorRemap(col, 1.0), 1.0);
                      }
                      else if (val == col.b) 
                      {
                          col = float4(colorRemap(col, 2.0), 1.0);
                      }
                  }


                  // Brutal gamut mapper - Dumb edition
                /*  if (col.r > 0.85)
                  {
                      float value = 0.0;
                      value = remap(col.r, 1.0, 0.85, 1.0, 0.0);
                      if (_TanHCompression > 0.1)
                      {
                          value = tanhCompressionFunction(value, 0.0, 1.0);
                      }

                      col.rgb = half3(col.r, (col.g < value, value);
                  }
                  else if (col.g > 0.85)
                  {
                      float value = 0.0;
                      value = remap(col.g, 1.0, 0.85, 1.0, 0.0);
                      if (_TanHCompression > 0.1) 
                      {
                          value = tanhCompressionFunction(value, 0.0, 1.0);
                      }

                      col.rgb = half3(value, col.g, value);
                  }
                  else if (col.b > 0.85)
                  {
                      float value = 0.0;
                      value = remap(col.b, 1.0, 0.85, 1.0, 0.0);
                      if (_TanHCompression > 0.1)
                      {
                          value = tanhCompressionFunction(value, 0.0, 1.0);
                      }
                      col.rgb = half3(value, value, col.b);
                  }*/

                  return col;//pow(col, 1.0 / 2.2);
            }
            ENDCG
        }
    }
}
