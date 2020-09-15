    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using UnityEngine;
    using UnityEngine.UI;
    using MathNet.Numerics;
    using Unity.Collections;
    using Unity.Jobs;
    using UnityEngine.Assertions.Comparers;


    struct GamutMapJob : IJobParallelFor
    {
        public NativeArray<Color> hdriPixelArray;
        [ReadOnly] public NativeArray<Vector2> animationCurve;

        [ReadOnly] public float exposure;
        [ReadOnly] public Vector2 finalKeyframe;
        [ReadOnly] public ColorGamut.TransferFunction activeTransferFunction;
        [ReadOnly] public bool isBleachingActive;
        [ReadOnly] public int lutLength;
        [ReadOnly] public int yIndexIntersect;
        
        private float hdriMaxRGBChannel;
        private float maxDynamicRange;
        private float bleachStartPoint;
        private float bleachingRange;
        private float bleachingRatio;
        private float hdriYMaxValue;
        private float inverseSrgbEOTF;

        private Color ratio;
        private Color hdriPixelColor;
        private Color tempResult;

        private Vector3 hdriPixelColorVec;
        private Vector3 maxDynamicRangeVec;
        
        public void Execute(int index)
        {
            hdriPixelColor = hdriPixelArray[index] * exposure;
            ratio = Color.black;
            inverseSrgbEOTF = 1.0f / 2.2f;
            if (animationCurve.Length != 0)
            {
                // Secondary nuance grade, guardrails
                if (hdriPixelColor.r > finalKeyframe.x || 
                    hdriPixelColor.g > finalKeyframe.x || 
                    hdriPixelColor.b > finalKeyframe.x)
                {
                    hdriPixelColor.r = finalKeyframe.x;
                    hdriPixelColor.g = finalKeyframe.x;
                    hdriPixelColor.b = finalKeyframe.x;
                }
                
                // Calculate Pixel max color and ratio
                hdriMaxRGBChannel = hdriPixelColor.maxColorComponent; 
                ratio = hdriPixelColor / hdriMaxRGBChannel;

                // Transfer function
                if (activeTransferFunction == ColorGamut.TransferFunction.Max_RGB)
                {
                    // New approach
                    maxDynamicRange = finalKeyframe.x; // The x axis max value on the curve
                    bleachStartPoint = animationCurve[yIndexIntersect].x;   // Intersect of x on Y = 1
                    
                    if (isBleachingActive)
                    {
                        if (hdriPixelColor.r > bleachStartPoint || hdriPixelColor.g > bleachStartPoint || hdriPixelColor.b > bleachStartPoint)
                        {
                            bleachingRange = maxDynamicRange - bleachStartPoint;
                            bleachingRatio = (hdriPixelColor.maxColorComponent - bleachStartPoint) / bleachingRange;
                            
                            hdriPixelColorVec.Set(hdriPixelColor.r, hdriPixelColor.g, hdriPixelColor.b);
                            maxDynamicRangeVec.Set(maxDynamicRange, maxDynamicRange, maxDynamicRange);
                            Vector3 outputColor = Vector3.Lerp( hdriPixelColorVec,maxDynamicRangeVec , bleachingRatio);
                            
                            hdriPixelColor.r = outputColor.x;
                            hdriPixelColor.g = outputColor.y;
                            hdriPixelColor.b = outputColor.z;

                            ratio = hdriPixelColor / hdriMaxRGBChannel;
                        }
                    }
                    // Get Y curve value
                    int maxRGBIndex = Mathf.Clamp(Convert.ToInt32((hdriMaxRGBChannel / finalKeyframe.x) * lutLength) - 1, 0, lutLength - 1);
                    hdriYMaxValue = Mathf.Min(animationCurve[maxRGBIndex].y, 1.0f);

                    hdriPixelColor = hdriYMaxValue * ratio;
                    activeTransferFunction = ColorGamut.TransferFunction.Max_RGB;
                }
                else
                {
                    activeTransferFunction = ColorGamut.TransferFunction.Per_Channel;
                    int rLutIndex = Mathf.Clamp(Convert.ToInt32((hdriPixelColor.r/finalKeyframe.x) * lutLength) - 1, 0, lutLength - 1);
                    int gLutIndex = Mathf.Clamp(Convert.ToInt32((hdriPixelColor.g/finalKeyframe.x) * lutLength) - 1, 0, lutLength - 1);
                    int bLutIndex = Mathf.Clamp(Convert.ToInt32((hdriPixelColor.b/finalKeyframe.x) * lutLength) - 1, 0, lutLength - 1);
                    hdriPixelColor.r = animationCurve[rLutIndex].y;
                    hdriPixelColor.g = animationCurve[gLutIndex].y;
                    hdriPixelColor.b = animationCurve[bLutIndex].y;
                }

                tempResult.r = Mathf.Pow(hdriPixelColor.r, inverseSrgbEOTF);
                tempResult.g = Mathf.Pow(hdriPixelColor.g, inverseSrgbEOTF);
                tempResult.b = Mathf.Pow(hdriPixelColor.b, inverseSrgbEOTF);
                tempResult.a = 1.0f;
                
                hdriPixelArray[index] = tempResult;
            }
        }
    }


    public class ColorGamut : MonoBehaviour
    {
        public Material colorGamutMat;
        public Texture2D inputTexture;
        public Material fullScreenTextureMat;
        public GameObject hdriPlane;
        public GameObject sweepPlane;
        public Texture2D sweepTexture;
        public List<Texture2D> HDRIList;

        public enum ShoulderLength
        {
            F_2_8,
            F_4_0,
            F_5_6,
            F_8_0,
            F_11_0
        }
        public enum LerpRatio
        {
            Aesthetic,
            Radiometric
        }
        [Header("Dye Bleaching")]
        public bool enableDyeBleaching;
        public LerpRatio lerpRatio;
        public float dye_bleach_x = 1.0f;
        public float dye_bleach_y = 1.0f;
        public enum TransferFunction
        {
            Per_Channel,
            Max_RGB
        }
        [Space]
        [Range(0.01f, 20.0f)] public float exposure;
        [Range(0.01f, 20.0f)] public float sweepExposure;
        [Space] [Header("Aesthetic Function")]
        public TransferFunction activeTransferFunction;
        [Space]
        
        private bool isSweepActive;
        private bool isBleachingActive;
        private bool KeyIsUp = false;
        private bool ApplyTexture = false;
        private bool isMultiThreaded = true;

        private int hdriIndex;
        private int inputTextureIdx = 0;
        private const int maxIterationsPerFrame = 100000;
        private int lutLength = 4096;
        private int yIndexIntersect = 0;
        
        private Texture2D hdriTextureTransformed;
        private Texture2D sweepTextureTransformed;
        private Texture2D textureToSave;
        private RenderTexture screenGrab;

        // private Ggm_troyedition ggm;
        private AnimationCurve animationCurve;
        private Color[] hdriPixelArray;
        private Vector2[] animationCurveLUT;
        private string logOutput = "";
        
        // Parametric curve variables
        private CurveTest parametricCurve;
        private Vector2[] controlPoints;
        private List<float> tValues;
        private float minDynamicRange;
        private float maxDynamicRange;
        private Vector2 origin;
        private Vector2 greyPoint;
        private float slope;

        private void Awake()
        {
            lerpRatio = LerpRatio.Aesthetic;
            activeTransferFunction = TransferFunction.Max_RGB;
        }
        
        void Start()
        {
            exposure = 1.0f;
            sweepExposure = 1.0f;
            hdriIndex = 0;

            isBleachingActive = true;
            isSweepActive     = false;
            enableDyeBleaching = false;
            
            // ggm = new Ggm_troyedition();

            // Parametric curve
            greyPoint = new Vector2(0.18f, 0.18f);
            origin = new Vector2(Mathf.Pow(2.0f, -6.0f) * 0.18f, 0.0f);
            slope = 2.2f;
            minDynamicRange = Mathf.Pow(2.0f, -6.0f) * greyPoint.x;
            maxDynamicRange = Mathf.Pow(2.0f, 6.0f) * greyPoint.x;
            
            createParametricCurve(greyPoint, origin);
            // parametricCurve = new CurveTest();
            // controlPoints = parametricCurve.createCurveControlPoints(greyPoint, 2.2f, origin);
            //
            // List<float> xValues = new List<float>(1024);
            // for (int i = 0; i < 1024; i++)
            // {
            //     xValues.Add(i * 0.01171f);
            // }
            // tValues = parametricCurve.calcTfromXquadratic(xValues, new List<Vector2>(controlPoints));
                
            if (HDRIList == null)
                Debug.LogError("HDRIs list is empty");

            inputTexture  = HDRIList[hdriIndex];
            
            hdriPixelArray          = new Color[inputTexture.width * inputTexture.height];
            hdriTextureTransformed  = new Texture2D(inputTexture.width, inputTexture.height, TextureFormat.RGBAHalf, false);
            textureToSave           = new Texture2D(inputTexture.width, inputTexture.height, TextureFormat.RGBAHalf, false);
            sweepTextureTransformed = new Texture2D(sweepTexture.width, sweepTexture.height);
            screenGrab = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            screenGrab.Create();
            
            StartCoroutine("CpuGGMIterative");
        }

        void createParametricCurve(Vector2 greyPoint, Vector2 origin)
        {
            parametricCurve = new CurveTest();
            controlPoints = parametricCurve.createCurveControlPoints(greyPoint, slope, origin);
            
            List<float> xValues = new List<float>(1024);
            for (int i = 0; i < 1024; i++)
            {
                xValues.Add(i * 0.01171f);
            }
            tValues = parametricCurve.calcTfromXquadratic(xValues, new List<Vector2>(controlPoints));

        //float[] temp = new float[1024];
        //xValues.CopyTo(temp, 1);
        List<float> xVals = new List<float>(xValues);
        xVals.RemoveAt(xVals.Count - 1);
        List<float> yValues = parametricCurve.calcYfromXQuadratic(xVals, tValues, new List<Vector2>(controlPoints));
        for (int i = 0; i < yValues.Count; i++)
        {
            Debug.Log("index " + i + "\t X: " + xVals[i].ToString() + "\t Y: " + yValues[i].ToString() + "\t T: " + tValues[i].ToString());
        }

    }
        public void setParametricCurveValues( float inSlope, float originPointX, float originPointY, 
                                               float greyPointX, float greyPointY)
        {
            this.slope = inSlope;
            this.origin.x = originPointX;
            this.origin.y = originPointY;
            this.greyPoint.x = greyPointX;
            this.greyPoint.y = greyPointY;

            createParametricCurve(greyPoint, origin);
        }
        
        void Update()
        {
            if (Input.GetKeyUp(KeyCode.T))
                KeyIsUp = true;

            if (Input.GetKeyUp(KeyCode.Y))
                KeyIsUp = false;

            if (Input.GetMouseButtonDown(0))
            {
                int xCoord = (int)Input.mousePosition.x;
                int yCoord = (int)Input.mousePosition.y;
                Color initialHDRIColor  = inputTexture.GetPixel(xCoord, yCoord);
                Color finalHDRIColor    = hdriTextureTransformed.GetPixel(xCoord, yCoord);

                Debug.Log("Inital \tEXR color: " + initialHDRIColor.ToString());
                Debug.Log("Exposed\tEXR color: " + (initialHDRIColor * exposure).ToString());
                Debug.Log("Final  \tEXR color: " + finalHDRIColor.ToString());
                Debug.Log("--------------------------------------------------------------------------------");
            }
        }

        void OnRenderImage(RenderTexture src, RenderTexture dest)
        {
            Graphics.Blit(hdriTextureTransformed, screenGrab, fullScreenTextureMat);
            colorGamutMat.SetTexture("_MainTex", screenGrab);
            Graphics.Blit(screenGrab, dest, fullScreenTextureMat);
        }

   
        private IEnumerator CpuGGMIterative()
        {
            int counter = maxIterationsPerFrame;
            int hdriPixelArrayLen = 0;

            float hdriMaxRGBChannel = 0.0f;
            float maxDynamicRange = 0.0f;
            float bleachStartPoint = 0.0f;
            float bleachingRange = 0.0f;
            float bleachingRatio = 0.0f;
            float hdriYMaxValue = 0.0f;
            float inverseSrgbEOTF = (1.0f / 2.2f);
            
            Color ratio = Color.black;
            Color hdriPixelColor = Color.black;

            Vector3 hdriPixelColorVec = Vector3.zero;
            Vector3 maxDynamicRangeVec = Vector3.zero;
            
            while (true)
            {
                if (inputTextureIdx != hdriIndex)
                {
                    inputTextureIdx = hdriIndex;
                    inputTexture = HDRIList[inputTextureIdx];
                }

                hdriPixelArray = inputTexture.GetPixels();
                //Color[] sweepPixelArray = sweepTexture.GetPixels();
                hdriPixelArrayLen = hdriPixelArray.Length;

                if (isMultiThreaded && animationCurveLUT != null)
                {
                    NativeArray<Color> pixels = new NativeArray<Color>(hdriPixelArrayLen, Allocator.TempJob);
                    pixels.CopyFrom(hdriPixelArray);
                    GamutMapJob job = new GamutMapJob();
                    job.hdriPixelArray = pixels;
                    job.exposure = exposure;
                    NativeArray<Vector2> animCurveLut = new NativeArray<Vector2>(animationCurveLUT.Length, Allocator.TempJob);
                    animCurveLut.CopyFrom(animationCurveLUT);
                    job.animationCurve = animCurveLut;
                    job.activeTransferFunction = activeTransferFunction;
                    job.isBleachingActive = isBleachingActive;
                    job.finalKeyframe = new Vector2(animationCurve[3].time, animationCurve[3].value);
                    job.lutLength = lutLength;
                    job.yIndexIntersect = yIndexIntersect;
                    
                    JobHandle handle = job.Schedule(hdriPixelArrayLen, 1);
                    handle.Complete();
                    for (int i = 0; i < hdriPixelArrayLen; i++)
                    {
                        hdriPixelArray[i] = pixels[i];
                    }
                    hdriTextureTransformed.SetPixels(hdriPixelArray);
                    hdriTextureTransformed.Apply();
                    pixels.Dispose();
                    animCurveLut.Dispose();
                    yield return new WaitForEndOfFrame();

                }
                else
                {
                    counter = maxIterationsPerFrame;
                    
                    for (int i = 0; i < hdriPixelArrayLen; i++, counter--)
                    {
                        if (counter <= 0)
                        {
                            counter = maxIterationsPerFrame;
                            yield return new WaitForEndOfFrame();
                        }

                        // Full dynamic range of image
                        hdriPixelColor = hdriPixelArray[i] * exposure;
                        //Color sweepPixelColor = sweepPixelArray[i] * sweepExposure;
                        ratio = Color.black;

                        if (animationCurve != null)
                        {
                            // Secondary Nuance Grade, guardrails
                            if (hdriPixelColor.r > animationCurve[3].time || hdriPixelColor.g > animationCurve[3].time ||
                                hdriPixelColor.b > animationCurve[3].time)
                            {
                                hdriPixelColor.r = animationCurve[3].time;
                                hdriPixelColor.g = animationCurve[3].time;
                                hdriPixelColor.b = animationCurve[3].time;
                            }
                            //if (sweepPixelColor.r > animationCurve[3].time || sweepPixelColor.g > animationCurve[3].time || sweepPixelColor.b > animationCurve[3].time)
                            //{
                            //    sweepPixelColor.r = animationCurve[3].time;
                            //    sweepPixelColor.g = animationCurve[3].time;
                            //    sweepPixelColor.b = animationCurve[3].time;
                            //}

                            // Calculate Pixel max color and ratio
                            hdriMaxRGBChannel = hdriPixelColor.maxColorComponent;
                            ratio = hdriPixelColor / hdriMaxRGBChannel;

                            // Calculate Sweep max color and ratio
                            //float sweepMaxRGBChannel = sweepPixelColor.maxColorComponent;
                            //Color sweepRatio = sweepPixelColor / sweepMaxRGBChannel;

                            // Transfer function
                            if (activeTransferFunction == TransferFunction.Max_RGB)
                            {
                                // New approach
                                maxDynamicRange = animationCurve[3].time; // The x axis max value on the curve
                                bleachStartPoint = TimeFromValue(animationCurve, 1.0f); // Intersect of x on Y = 1

                                if (isBleachingActive)
                                {
                                    if (hdriPixelColor.r > bleachStartPoint || hdriPixelColor.g > bleachStartPoint ||
                                        hdriPixelColor.b > bleachStartPoint)
                                    {
                                        bleachingRange = maxDynamicRange - bleachStartPoint;
                                        bleachingRatio = (hdriPixelColor.maxColorComponent - bleachStartPoint) /
                                                         bleachingRange;

                                        hdriPixelColorVec.Set(hdriPixelColor.r, hdriPixelColor.g, hdriPixelColor.b);
                                        maxDynamicRangeVec.Set(maxDynamicRange, maxDynamicRange, maxDynamicRange);
                                        Vector3 outputColor = Vector3.Lerp(hdriPixelColorVec, maxDynamicRangeVec,
                                            bleachingRatio);

                                        hdriPixelColor.r = outputColor.x;
                                        hdriPixelColor.g = outputColor.y;
                                        hdriPixelColor.b = outputColor.z;

                                        ratio = hdriPixelColor / hdriMaxRGBChannel;
                                    }
                                }

                                // Get Y curve value
                                // hdriYMaxValue = Mathf.Min(animationCurve.Evaluate(hdriMaxRGBChannel), 1.0f);
                                
                                List<float> xVal = new List<float>();
                                xVal.Add(hdriMaxRGBChannel);
                                List<float> yValues = parametricCurve.calcYfromXQuadratic(xVal, tValues, new List<Vector2>(controlPoints));
                                if(yValues.Count > 0)
                                    hdriYMaxValue = Mathf.Min(yValues[0], 1.0f);
                                
                                
                                
                                
                                hdriPixelColor = hdriYMaxValue * ratio;

                                // Sweep texture
                                //sweepMaxRGBChannel = animationCurve.Evaluate(sweepMaxRGBChannel);
                                //sweepPixelColor = sweepMaxRGBChannel * sweepRatio;

                                activeTransferFunction = TransferFunction.Max_RGB;
                            }
                            else
                            {
                                activeTransferFunction = TransferFunction.Per_Channel;
                                hdriPixelColor.r = animationCurve.Evaluate(hdriPixelColor.r);
                                hdriPixelColor.g = animationCurve.Evaluate(hdriPixelColor.g);
                                hdriPixelColor.b = animationCurve.Evaluate(hdriPixelColor.b);

                                //sweepPixelColor.r = animationCurve.Evaluate(sweepPixelColor.r);
                                //sweepPixelColor.g = animationCurve.Evaluate(sweepPixelColor.g);
                                //sweepPixelColor.b = animationCurve.Evaluate(sweepPixelColor.b);
                            }

                            hdriPixelArray[i].r = Mathf.Pow(hdriPixelColor.r, inverseSrgbEOTF);
                            hdriPixelArray[i].g = Mathf.Pow(hdriPixelColor.g, inverseSrgbEOTF);
                            hdriPixelArray[i].b = Mathf.Pow(hdriPixelColor.b, inverseSrgbEOTF);
                            hdriPixelArray[i].a = 1.0f;
                            //sweepPixelArray[i] = new Color(Mathf.Pow(sweepPixelColor.r, 1.0f / 2.2f), Mathf.Pow(sweepPixelColor.g, 1.0f / 2.2f), Mathf.Pow(sweepPixelColor.b, 1.0f / 2.2f), 1.0f);
                        }
                    }
                    hdriTextureTransformed.SetPixels(hdriPixelArray);
                    hdriTextureTransformed.Apply();
                }
                //sweepTextureTransformed.SetPixels(sweepPixelArray);
                //sweepTextureTransformed.Apply();
            }
        }

        public bool getShowSweep()
        {
            return isSweepActive;
        }

        public void setShowSweep(bool isActive) 
        {
            isSweepActive = isActive;
            sweepPlane.SetActive(isSweepActive);

        }

        public bool getIsMultiThreaded()
        {
            return isMultiThreaded;
        }

        public void setIsMultiThreaded(bool isMultiThreaded)
        {
            this.isMultiThreaded = isMultiThreaded;
        }
        
        
        Texture2D toTexture2D(RenderTexture rTex)
        {
            Texture2D tex = new Texture2D(rTex.width, rTex.height, TextureFormat.RGBAHalf, false);
            RenderTexture.active = rTex;
            tex.ReadPixels(new Rect(0, 0, rTex.width, rTex.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            return tex;
        }
        public AnimationCurve getAnimationCurve() 
        {
            return animationCurve;
        }
        public void setAnimationCurve(AnimationCurve curve)
        {
            animationCurve = curve;
            animationCurveLUT = new Vector2[lutLength];
            float tolerance = 0.01f;
            for (int i = 0; i < lutLength; i++)
            {
                float xValue = animationCurve.Evaluate(((float)i / (float)lutLength) * animationCurve[3].time );
                float yValue = TimeFromValue(animationCurve, xValue);
                animationCurveLUT[i] = new Vector2(xValue, yValue);

                if (Mathf.Approximately(yValue, 1.0f) || 
                    (yValue > (1.0f - tolerance) && yValue < (1.0f + tolerance)))
                    yIndexIntersect = i;
            }
        }

        public void setHDRIIndex(int index)
        {
            hdriIndex = index;
        }

        static public Texture2D GetRTPixels(RenderTexture rt)
        {
            // Remember currently active render texture
            RenderTexture currentActiveRT = RenderTexture.active;

            // Set the supplied RenderTexture as the active one
            RenderTexture.active = rt;

            // Create a new Texture2D and read the RenderTexture image into it
            Texture2D tex = new Texture2D(rt.width, rt.height);
            tex.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);

            // Restorie previously active render texture
            RenderTexture.active = currentActiveRT;
            return tex;
        }
       

        
        float remap(float value, float min0, float max0, float min1, float max1)
        {
            return min1 + (value - min0) * ((max1 - min1) / (max0 - min0));
        }

        Color colorRemap(Color col, float channel)
        {
            float red = col.r;
            float green = col.g;
            float blue = col.b;
            Color outColor = new Color();
            if (channel == 0.0) // 0.0 corresponds to red
            {
                float newRangeMin = remap(Mathf.Clamp01(col.r), 1.0f, 0.85f, 1.0f, 0.0f); // how far between 0.85 and 1 are we? Remap it to 1.0 to 0.0

                green = (col.r != col.g) ? Mathf.Lerp(green, col.r, newRangeMin) : col.g;
                blue = (col.r != col.b) ? Mathf.Lerp(blue, col.r, newRangeMin) : col.b;

                outColor = new Color(col.r, green, blue);
            }
            else if (channel == 1.0) // 1.0 corresponds to green
            {
                float newRangeMin = remap(Mathf.Clamp01(col.g), 1.0f, 0.85f, 1.0f, 0.0f); // how far between 0.85 and 1 are we? Remap it to 1.0 to 0.0

                red = (col.g != col.r) ? Mathf.Lerp(red, col.g, newRangeMin) : col.r;
                blue = (col.g != col.b) ? Mathf.Lerp(blue, col.g, newRangeMin) : col.b;

                outColor = new Color(red, col.g, blue);
            }
            else if (channel == 2.0) // 2.0 corresponds to blue
            {
                float newRangeMin = remap(Mathf.Clamp01(col.b), 1.0f, 0.85f, 1.0f, 0.0f); // how far between 0.85 and 1 are we? Remap it to 1.0 to 0.0

                red = (col.b != col.r) ? Mathf.Lerp(red, col.b, newRangeMin) : col.r;
                green = (col.b != col.g) ? Mathf.Lerp(green, col.b, newRangeMin) : col.g;
                outColor = new Color(red, green, col.b);
            }

            return new Color(Mathf.Clamp01(outColor.r), Mathf.Clamp01(outColor.g), Mathf.Clamp01(outColor.b));
        }
        
        public void setBleaching(bool inIsBleachingActive) 
        {
            isBleachingActive = inIsBleachingActive;
        }
        
        public static float TimeFromValue(AnimationCurve c, float value, float precision = 1e-6f)
        {
            float minTime = c.keys[0].time;
            float maxTime = c.keys[c.keys.Length - 1].time;
            float best = (maxTime + minTime) / 2;
            float bestVal = c.Evaluate(best);
            int it = 0;
            const int maxIt = 1000;
            float sign = Mathf.Sign(c.keys[c.keys.Length - 1].value - c.keys[0].value);
            while (it < maxIt && Mathf.Abs(minTime - maxTime) > precision)
            {
                if ((bestVal - value) * sign > 0)
                {
                    maxTime = best;
                }
                else
                {
                    minTime = best;
                }
                best = (maxTime + minTime) / 2;
                bestVal = c.Evaluate(best);
                it++;
            }
            return best;
        }

        // Color newGGM(Color RGB, float compressionThreshold)
        // {
        //     Vector3 result = ggm.the_ggm(new Vector3(RGB.r, RGB.g, RGB.b), new Vector3(compressionThreshold, compressionThreshold, compressionThreshold));
        //     return new Color(result.x, result.y, result.z);
        // }

        bool all(bool[] x)       // bvec can be bvec2, bvec3 or bvec4
        {
            bool result = true;
            int i;
            for (i = 0; i < x.Length; ++i)
            {
                result &= x[i];
            }
            return result;
        }

        bool any(bool[] x)
        {     // bvec can be bvec2, bvec3 or bvec4
            bool result = false;
            int i;
            for (i = 0; i < x.Length; ++i)
            {
                result |= x[i];
            }
            return result;
        }

        Vector3 sum_vec3(Vector3 input_vector)
        {
            float sum = input_vector.x + input_vector.y + input_vector.z;
            return new Vector3(sum, sum, sum);
        }

        Vector3 max_vec3(Vector3 input_vector)
        {
            float max = Mathf.Max(Mathf.Max(input_vector.x, input_vector.y), input_vector.z);
            return new Vector3(max, max, max);
        }

        bool[] greaterThan(Vector3 vecA, Vector3 vecB)
        {
            return new bool[3] { (vecA.x > vecB.x), (vecA.x > vecB.x), (vecA.x > vecB.x) };
        }


        bool[] lessThanEqual(Vector3 vecA, Vector3 vecB)
        {
            return new bool[3] { (vecA.x <= vecB.x), (vecA.x <= vecB.x), (vecA.x <= vecB.x) };
        }
        
    }
