Shader "Custom/GLSL_Ggm"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ExposureControl("Exposure Value", Float) = 1.0
        _uControlA("Control Point A", Range(0, 1)) = 0.0
        _uControlB("Control Point B", Range(0, 1)) = 0.0
        _uControlC("Control Point C", Range(0, 1)) = 1.0
        _uControlD("Control Point D", Range(0, 1)) = 1.0
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            GLSLPROGRAM

            #include "UnityCG.glslinc"

            uniform sampler2D _MainTex;
            in vec4 uv;
            out vec2 vsUV;
    

            #ifdef VERTEX
            void main()
            {
                vsUV = uv.xy;
                gl_Position = gl_ModelViewProjectionMatrix * gl_Vertex;
            }

            #endif

            #ifdef FRAGMENT


            uniform float _ExposureControl;
            uniform float _uControlA;
            uniform float _uControlB;
            uniform float _uControlC;
            uniform float _uControlD;

            out vec4 outColor;

            // Helper functions:
            float slopeFromT(float t, float A, float B, float C) {
                float dtdx = 1.0 / (3.0 * A * t * t + 2.0 * B * t + C);
                return dtdx;
            }

            float xFromT(float t, float A, float B, float C, float D) {
                float x = A * (t * t * t) + B * (t * t) + C * t + D;
                return x;
            }

            float yFromT(float t, float E, float F, float G, float H) {
                float y = E * (t * t * t) + F * (t * t) + G * t + H;
                return y;
            }

            float cubicBezier(float x, float a, float b, float c, float d) {

                float y0a = 0.00; // initial y
                float x0a = 0.00; // initial x 
                float y1a = b;    // 1st influence y   
                float x1a = a;    // 1st influence x 
                float y2a = d;    // 2nd influence y
                float x2a = c;    // 2nd influence x
                float y3a = 1.00; // final y 
                float x3a = 1.00; // final x 

                float A = x3a - 3 * x2a + 3 * x1a - x0a;
                float B = 3 * x2a - 6 * x1a + 3 * x0a;
                float C = 3 * x1a - 3 * x0a;
                float D = x0a;

                float E = y3a - 3 * y2a + 3 * y1a - y0a;
                float F = 3 * y2a - 6 * y1a + 3 * y0a;
                float G = 3 * y1a - 3 * y0a;
                float H = y0a;

                // Solve for t given x (using Newton-Raphelson), then solve for y given t.
                // Assume for the first guess that t = x.
                float currentt = x;
                int nRefinementIterations = 5;
                for (int i = 0; i < nRefinementIterations; i++) {
                    float currentx = xFromT(currentt, A, B, C, D);
                    float currentslope = slopeFromT(currentt, A, B, C);
                    currentt -= (currentx - x) * (currentslope);
                    currentt = clamp(currentt, 0, 1);;
                }

                float y = yFromT(currentt, E, F, G, H);
                return clamp(y, 0.0, 1.0);
            }

            vec3 sum_vec3(vec3 input_vector)
            {
                return vec3(input_vector.x + input_vector.y +
                    input_vector.z);
            }

            vec3 max_vec3(vec3 input_vector)
            {
                return vec3(
                    max(
                        max(input_vector.x, input_vector.y),
                        input_vector.z
                    )
                );
            }

            // The dumbest and most direct approach to volumetric gamut
            // mapping you will ever see. Basically let a channel reach
            // peak value and spill over in a completely ignorant fashion
            // holding ratios.
            vec3 the_ggm(vec3 input_rgb, vec3 threshold)
            {
                // Escape out if all values are beyond the GGM threshold
                // to prevent negative value checks and clamps below.
                if (all(greaterThan(input_rgb, threshold))) {
                    return input_rgb;
                }

                // Skip any values that are within the GGM threshold range.
                if (any(greaterThan(input_rgb, threshold))) {
                    // Generate a 0.0 or 1.0 mask for every channel over the
                    // GGM threshold. These are the channels that will require
                    // having energy removed
                    vec3 over_energy_mask = vec3(greaterThan(input_rgb, threshold));

                    // Generate a 0.0 or 1.0 mask for every channel under the
                    // GGM threshold. These are the channels that will require
                    // receiving the excess energy.
                    vec3 under_energy_mask = vec3(lessThanEqual(input_rgb, threshold));

                    // Sum how much energy we are redistributing. This is
                    // any energy over and above the threshold. Negative
                    // energies result for subtraction.
                    vec3 over_energies = over_energy_mask * (threshold - input_rgb);

                    // Sum the under energy wells. This is where the excess energy
                    // will be distributed to. Positive energies result for
                    // addition.
                    vec3 under_energies = under_energy_mask * (threshold - input_rgb);

                    // Calculate the total of energy beyond the threshold.
                    vec3 total_over = sum_vec3(abs(over_energies));
                    vec3 over_ratio = over_energies / total_over;

                    // Calculate the total of energy that can be redistributed to.
                    vec3 total_under = sum_vec3(under_energies);
                    vec3 under_ratio = under_energies / total_under;

                    // Merge the masked values into a single vector.
                    vec3 under_over_merged = over_ratio + under_ratio;

                    // Given that the energy over the threshold may sum to
                    // more than the display volume, use a 0% and 100% clip.
                    // Rather brute force and unnecessary given that the display
                    // will clip the values implicitly as it cannot display greater
                    // than 100% output for example, it's worth noting and
                    // being explicit for the case of demonstration.
                    input_rgb += clamp(total_over * under_over_merged, 0.0, 1.0);
                }

                return input_rgb;
            }

            void main()
            {
                vec2 uv = gl_FragCoord.xy / _ScreenParams.xy;
                vec4 col = texture(_MainTex, uv);
                col *= _ExposureControl;
              /*  col.rgb = clamp(vec3(cubicBezier(col.r, _uControlA, _uControlB, _uControlC, _uControlD),
                                     cubicBezier(col.g, _uControlA, _uControlB, _uControlC, _uControlD),
                                     cubicBezier(col.b, _uControlA, _uControlB, _uControlC, _uControlD)), 
                                0.0, 1.0);*/
				col.rgb = the_ggm(col.rgb, vec3(1.0));

                outColor = col;
            }

            #endif

            ENDGLSL
        }
    }
}
