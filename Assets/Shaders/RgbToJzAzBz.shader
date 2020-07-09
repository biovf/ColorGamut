Shader "Custom/RgbToJzAzBz"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ExposureControl("Exposure Control", Range(0.0, 30.0)) = 1.0
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

            
            sampler2D _MainTex;
            float _ExposureControl;

            // Used to convert from linear RGB to XYZ space
            const float3x3 RGB_2_XYZ = (float3x3(

                0.5767309 ,0.1855540 , 0.1881852,    //Adobe RGB
                0.2973769 ,0.6273491 , 0.0752741,
                0.0270343 ,0.0706872 , 0.9911085
                /*0.4124564, 0.2126729, 0.0193339,  // sRGB
                0.3575761, 0.7151522, 0.1191920,
                0.1804375, 0.0721750, 0.9503041*/
            ));
            // Converts a color from linear RGB to XYZ space
            float3 rgb_to_xyz(float3 rgb) {
                return mul(RGB_2_XYZ, rgb);
            }


            // Used to convert from XYZ to linear RGB space
            const float3x3 XYZ_2_RGB = (float3x3(
                2.0413690  , -0.5649464, -0.3446944,    //Adobe RGB
                -0.9692660 , 1.8760108 , 0.0415560 ,    // sRGB
                0.0134474  ,-0.1183897 , 1.0154096
       /*         3.2404542, -0.9692660, 0.0556434,
                -1.5371385, 1.8760108, -0.2040259,
                -0.4985314, 0.0415560, 1.0572252*/
            ));
            // Converts a color from XYZ to linear RGB space
            float3 XYZ_to_RBG(float3 xyz) {
                return mul(XYZ_2_RGB, xyz);
            }

            //Conversion matrices
            float3 RGBtoXYZ(float3 RGB)
            {
                float3x3 m = float3x3(
                    /*    0.6068909, 0.1735011, 0.2003480,
                        0.2989164, 0.5865990, 0.1144845,
                        0.0000000, 0.0660957, 1.1162243);*/
                    0.5767309, 0.1855540, 0.1881852,    //Adobe RGB
                    0.2973769, 0.6273491, 0.0752741,
                    0.0270343, 0.0706872, 0.9911085);
                //return RGB * m;
                return mul(m, RGB);

            }

           
            float lms(float t) {
                if (t > 0.) {
                    float r = pow(t, 0.007460772656268214);
                    float s = (0.8359375 - r) / (18.6875*r + -18.8515625);
                    return pow(s, 6.277394636015326);
                } else {
                    return 0.;
                }
            }

            float srgb(float c) {
                if (c <= 0.0031308049535603713) {
                    return c * 12.92;
                } else {
                    float c_ = pow(c, 0.41666666666666666);
                    return c_ * 1.055 + -0.055;
                }
            }

            float3 jchz2srgb(float3 jchz) {
                float jz = jchz.x*0.16717463120366200 + 1.6295499532821566e-11;
                float cz = jchz.y*0.16717463120366200;
                float hz = jchz.z*6.28318530717958647 + -3.14159265358979323;
                
                float iz = jz / (0.56*jz + 0.44);
                float az = cz * cos(hz);
                float bz = cz * sin(hz);
                
                float l_ = iz + az* +0.13860504327153930 + bz* +0.058047316156118830;
                float m_ = iz + az* -0.13860504327153927 + bz* -0.058047316156118904;
                float s_ = iz + az* -0.09601924202631895 + bz* -0.811891896056039000;
                
                float l = lms(l_);
                float m = lms(m_);
                float s = lms(s_);
                
                float lr = l* +0.0592896375540425100e4 + m* -0.052239474257975140e4 + s* +0.003259644233339027e4;
                float lg = l* -0.0222329579044572220e4 + m* +0.038215274736946150e4 + s* -0.005703433147128812e4;
                float lb = l* +0.0006270913830078808e4 + m* -0.007021906556220012e4 + s* +0.016669756032437408e4;
                
                return float3(srgb(lr), srgb(lg), srgb(lb));
            }

            float PQ (float col) 
            {
                float XX = pow(col*1e-4, 0.1593017578125);
                return pow((0.8359375 + 18.8515625*XX) / (1 + 18.6875*XX),134.034375);
            }

            float3 rgb2jzazbz (float3 inRGB)
            {
              /*  float Lp = PQ(0.674207838*inRGB.x + 0.382799340*inRGB.y - 0.047570458*inRGB.z);
                float Mp = PQ(0.149284160*inRGB.x + 0.739628340*inRGB.y + 0.083327300*inRGB.z);
                float Sp = PQ(0.070941080*inRGB.x + 0.174768000*inRGB.y + 0.670970020*inRGB.z);
                float Iz = 0.5 * (Lp + Mp);
                float az = 3.524000*Lp - 4.066708*Mp + 0.542708*Sp;
                float bz = 0.199076*Lp + 1.096799*Mp - 1.295875*Sp;
                float Jz = (0.44 * Iz) / (1 - 0.56*Iz) - 1.6295499532821566e-11;
                return float3(Jz, az, bz);*/

            }

            //  ---  Jzazbz  ---  //{
            float3 XYZ_to_Jzazbz(float3 XYZ) {
                float b = 1.15;
                float g = 0.66;
                float3 XYZprime = XYZ;
                XYZprime.x = XYZ.x * b - (b - 1) * XYZ.z;
                XYZprime.y = XYZ.y * g - (g - 1) * XYZ.x;
                XYZprime.z = XYZ.z;
                //float3 LMS = XYZprime * mat3x3(0.41478972, 0.579999, 0.0146480, -0.2015100, 1.120649, 0.0531008, -0.0166008, 0.264800, 0.6684799);
                float3 LMS = mul(float3x3(0.41478972, 0.579999, 0.0146480, -0.2015100, 1.120649, 0.0531008, -0.0166008, 0.264800, 0.6684799), XYZprime);

                float c1 = 3424 / pow(2.0, 12.0);
                float c2 = 2413 / pow(2.0, 7.0);
                float c3 = 2392 / pow(2.0, 7.0);
                float n = 2610 / pow(2.0, 14.0);
                float p = 1.7 * 2523 / pow(2.0, 5.0);
                float3 LMSprime = pow((c1 + c2 * pow(LMS / 10000, float3(n, n, n))) / (1 + c3 * pow(LMS / 10000, float3(n, n, n))), float3(p, p, p));
                //float3 Izazbz = LMSprime * mat3x3(0.5, 0.5, 0.0, 3.524000, -4.066708, 0.542708, 0.199076, 1.096799, -1.295875);
                float3 Izazbz =  mul(float3x3(0.5, 0.5, 0.0, 3.524000, -4.066708, 0.542708, 0.199076, 1.096799, -1.295875), LMSprime);

                float d = -0.56;
                float d0 = 1.6295499532821566 * pow(10.0, -11.0);
                float3 Jzazbz = Izazbz;
                Jzazbz.x = ((1 + d) * Izazbz.x) / (1 + d * Izazbz.x) - d0;
                return Jzazbz;
            }


            float3 Jzazbz_to_XYZ(float3 Jzazbz) {
                float d0 = 1.6295499532821566 * pow(10.0, -11.0);
                float d = -0.56;
                float Iz = (Jzazbz.x + d0) / (1 + d - d * (Jzazbz.x + d0));
                float3 Izazbz = float3(Iz, Jzazbz.y, Jzazbz.z);
                //float3 LMSprime = Izazbz * mat3x3(1.0, 0.138605043271539, 0.0580473161561189, 1.0, -0.138605043271539, -0.0580473161561189, 1.0, -0.0960192420263189, -0.811891896056039);
                float3 LMSprime = mul(float3x3(1.0, 0.138605043271539, 0.0580473161561189, 1.0, -0.138605043271539, -0.0580473161561189, 1.0, -0.0960192420263189, -0.811891896056039), Izazbz);
                float c1 = 3424 / pow(2.0, 12.0);
                float c2 = 2413 / pow(2.0, 7.0);
                float c3 = 2392 / pow(2.0, 7.0);
                float n = 2610 / pow(2.0, 14.0);
                float p = 1.7 * 2523 / pow(2.0, 5.0);
                float3 LMS = 10000 * pow((c1 - pow(LMSprime, float3(1.0 / p, 1.0 / p, 1.0 / p ))) / (c3 * pow(LMSprime, float3(1.0 / p, 1.0 / p , 1.0 / p)) - c2), float3(1.0 / n, 1.0 / n, 1.0 / n));
                //float3 XYZprime = LMS * mat3x3(1.92422643578761, -1.00479231259537, 0.037651404030618, 0.350316762094999, 0.726481193931655, -0.065384422948085, -0.0909828109828476, -0.312728290523074, 1.52276656130526);
                float3 XYZprime = mul(float3x3(1.92422643578761, -1.00479231259537, 0.037651404030618, 0.350316762094999, 0.726481193931655, -0.065384422948085, -0.0909828109828476, -0.312728290523074, 1.52276656130526), LMS);
                float b = 1.15;
                float g = 0.66;
                float3 XYZ = XYZprime;
                XYZ.x = (XYZprime.x + (b - 1.0) * XYZprime.z) / b;
                XYZ.y = (XYZprime.y + (g - 1.0) * XYZ.x) / g;
                XYZ.z = XYZprime.z;
                return XYZ;
            }
            //  ---  sRGB  ---  //
            float3 XYZ_to_sRGB(float3 x) {
                //x = mul(x * mat3x3(3.2404542, -1.5371385, -0.4985314, -0.9692660, 1.8760108, 0.0415560, 0.0556434, -0.2040259, 1.0572252);
                x = mul(float3x3(3.2404542, -1.5371385, -0.4985314, -0.9692660, 1.8760108, 0.0415560, 0.0556434, -0.2040259, 1.0572252), x );
                x = lerp(1.055 * pow(x, float3(1. / 2.4, 1. / 2.4, 1. / 2.4 )) - 0.055, 12.92 * x, step(x, float3(0.0031308, 0.0031308, 0.0031308)));
                return x;
            }

            float3 XYZtoSRGB(float3 XYZ)
            {
                const float3x3 m = float3x3(
                    3.2404542, -1.5371385, -0.4985314,
                    -0.9692660, 1.8760108, 0.0415560,
                    0.0556434, -0.2040259, 1.0572252);

                return mul(m, XYZ);
            }

            float3 DecodeGamma(float3 color, float gamma)
            {
                color = clamp(color, 0.0, 1.0);
                color.r = (color.r <= 0.00313066844250063) ?
                    color.r * 12.92 : 1.055 * pow(color.r, 1.0 / gamma) - 0.055;
                color.g = (color.g <= 0.00313066844250063) ?
                    color.g * 12.92 : 1.055 * pow(color.g, 1.0 / gamma) - 0.055;
                color.b = (color.b <= 0.00313066844250063) ?
                    color.b * 12.92 : 1.055 * pow(color.b, 1.0 / gamma) - 0.055;

                return color;
            }

            // conversion from XYZ to sRGB Reference White D65 ( color space used by windows ) 
            float3 sRGB(float3 c)
            {
                float3 v = XYZtoSRGB(c);
                v = DecodeGamma(v, 2.2); //Companding

                return v;
            }

            float remap(float value, float min0, float max0, float min1, float max1) 
            {
                return min1 + (value - min0) * ((max1 - min1) / (max0 - min0));
            }

            float3 tanh_compression_function(float3 x, float a, float b)
            {
                return (a + b * tanh((x - a) / b));
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half4 col = tex2D(_MainTex, i.uv);

                // We're saturating so clip and go to white
                if (col.r > 0.99 || col.g > 0.99 || col.b > 0.99) 
                {
                    return half4(1.0, 1.0, 1.0, 1.0);
                }

                // Brutal gamut mapper - Dumb edition
                if (col.r > 0.85)
                {
                    float value = 0.0;
                    value = remap(col.r, 1.0, 0.85, 1.0, 0.0);
                    //value = remap(col.r, 1.0, 0.85, 0.925, 0.85);

                    col.rgb = half3(col.r, value, value);
                }
                else if (col.g > 0.85) 
                {
                    float value = 0.0;
                    value = remap(col.g, 1.0, 0.85, 1.0, 0.0);
                    //value = remap(col.g, 1.0, 0.85, 0.925, 0.85);

                    col.rgb = half3(value, col.g, value);
                }
                else if (col.b > 0.85) 
                {
                    float value = 0.0;
                    value = remap(col.b, 1.0, 0.85, 1.0, 0.0);
                    //value = remap(col.b, 1.0, 0.85, 0.925, 0.85);

                    col.rgb = half3(value, value, col.b);
                }
              

                // 1st experiment
 /*               col.rgb = RGBtoXYZ(col.rgb);
                col.rgb = XYZ_to_Jzazbz(col.rgb);
                col.rgb = Jzazbz_to_XYZ(col.rgb);
                col.rgb = sRGB(col.rgb);*/


              
                return pow(col, 1.0/2.2);
            }
            ENDCG
        }
    }
}
