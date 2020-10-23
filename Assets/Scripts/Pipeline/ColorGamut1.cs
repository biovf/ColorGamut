using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Debug = UnityEngine.Debug;

public class ColorGamut1
{
    public Material colorGamutMat;
    public Material fullScreenTextureMat;
    public Material fullScreenTextureAndSweepMat;
    public Texture2D sweepTexture;
    public List<Texture2D> HDRIList;

    private TransferFunction activeTransferFunction;
    private float exposure;

    private Texture2D inputTexture;

    private bool isSweepActive;
    private bool isBleachingActive;
    private bool isMultiThreaded = false;
    private bool showPixelsOutOfGamut = false;

    private int hdriIndex;
    private int inputTextureIdx = 0;
    private int yIndexIntersect = 0;
    private const int maxIterationsPerFrame = 100000;

    private Texture2D hdriTextureTransformed;

    public Texture2D HdriTextureTransformed => hdriTextureTransformed;

    private RenderTexture screenGrab;

    public RenderTexture ScreenGrab => screenGrab;

    private AnimationCurve animationCurve;
    private Color[] hdriPixelArray;
    private Vector2[] animationCurveLUT;
    private string logOutput = "";

    // Parametric curve variables
    private float slope;
    private Vector2 origin;
    private CurveTest parametricCurve = null;
    private Vector2[] controlPoints;
    private List<float> tValues;

    public float SlopeMax => slopeMax;
    private float slopeMax;

    public float SlopeMin => slopeMin;
    private float slopeMin;
    public float MinRadiometricValue => minRadiometricValue;
    private float minRadiometricValue;

    public float MINExposureValue => minExposureValue;
    private float minExposureValue;

    public float MAXExposureValue => maxExposureValue;
    private float maxExposureValue;

    public float MaxRadiometricValue => maxRadiometricValue;
    private float maxRadiometricValue;
    private float maxDisplayValue;
    private float minDisplayValue;

    private Vector2 greyPoint;

    public Vector2 GreyPoint => greyPoint;

    private List<float> xValues;
    private List<float> yValues;

    public int CurveLutLength => curveLutLength;
    private int curveLutLength;

    private int bleachingRatioPower;

    private enum ColorRange
    {
        InsideGamut,
        BelowGamut,
        AboveGamut
    };

    private ColorRange colorRange;

    public enum CurveDataState
    {
        NotCalculated,
        Calculated,
        MustRecalculate
    };

    private CurveDataState curveDataState = CurveDataState.NotCalculated;
    public CurveDataState CurveState => curveDataState;
    private Camera mainCamera;

    public ColorGamut1(Material colorGamutMat, Material fullscreenTexMat, List<Texture2D> hdriList)
    {
        this.HDRIList = hdriList;
        this.colorGamutMat = colorGamutMat;
        this.fullScreenTextureMat = fullscreenTexMat;
    }

    public void Start(HDRPipeline pipeline)
    {
        activeTransferFunction = TransferFunction.Max_RGB;

        hdriIndex = 0;
        bleachingRatioPower = 2;
        exposure = 1.0f;

        isBleachingActive = false;
        isSweepActive = false;

        // Parametric curve
        slope = 2.2f;
        slopeMin = 1.02f;
        slopeMax = 4.5f;
        maxDisplayValue = 1.5f;
        minDisplayValue = 0.0f;
        greyPoint = new Vector2(0.18f, 0.18f);
        minExposureValue = -6.0f;
        maxExposureValue = 6.0f;

        minRadiometricValue = Mathf.Pow(2.0f, minExposureValue) * greyPoint.x;
        maxRadiometricValue = Mathf.Pow(2.0f, maxExposureValue) * greyPoint.x;

        Debug.Log("Minimum Radiometric Value: \t " + minRadiometricValue.ToString("F6"));
        Debug.Log("Maximum Radiometric Value: \t " + maxRadiometricValue.ToString("F6"));
        origin = new Vector2(minRadiometricValue, 0.00001f);
        curveLutLength = 1024;
        createParametricCurve(greyPoint, origin);

        if (HDRIList == null)
            Debug.LogError("HDRIs list is empty");

        inputTexture = HDRIList[hdriIndex];
        mainCamera = pipeline.gameObject.GetComponent<Camera>();

        hdriPixelArray = new Color[inputTexture.width * inputTexture.height];
        hdriTextureTransformed = new Texture2D(inputTexture.width, inputTexture.height, TextureFormat.RGBAHalf, false, true);
        screenGrab = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBHalf,
            RenderTextureReadWrite.Linear);
        screenGrab.Create();
        mainCamera = pipeline.gameObject.GetComponent<Camera>();
        curveDataState = CurveDataState.NotCalculated;
    }

    private void createParametricCurve(Vector2 greyPoint, Vector2 origin)
    {
        if (parametricCurve == null)
            parametricCurve = new CurveTest(minExposureValue, maxExposureValue, maxRadiometricValue, maxDisplayValue);

        controlPoints = parametricCurve.createControlPoints(origin, greyPoint, slope);
        xValues = initialiseXCoordsInRange(curveLutLength, maxRadiometricValue);
        tValues = parametricCurve.calcTfromXquadratic(xValues.ToArray(), controlPoints);
        yValues = parametricCurve.calcYfromXQuadratic(xValues, tValues, new List<Vector2>(controlPoints));
    }

    public void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            int xCoord = (int) Input.mousePosition.x;
            int yCoord = (int) Input.mousePosition.y;
            // Mouse position gives us the coordinates based on any resolution we have
            // On the other hand our textures have a fixed resolution so we're going to have to remap the mouse coordinates
            // into the texture width/height range
            float normalisedXCoord = (float) xCoord / (float) Screen.width;
            float normalisedYCoord = (float) yCoord / (float) Screen.height;
            xCoord = (int) (normalisedXCoord * (float) inputTexture.width);
            yCoord = (int) (normalisedYCoord * (float) inputTexture.height);

            Color initialHDRIColor = inputTexture.GetPixel(xCoord, yCoord);
            Color finalHDRIColor = hdriTextureTransformed.GetPixel(xCoord, yCoord);

            Vector2 shapedHDRIColor = new Vector2(initialHDRIColor.maxColorComponent, 0.0f);
            shapedHDRIColor.y = parametricCurve.getYCoordinateLogXInput(shapedHDRIColor.x, xValues.ToArray(), yValues.ToArray(),
                tValues.ToArray(), controlPoints);

            Debug.Log("Coordinates \t \t " + "x: " + xCoord + " y: " + yCoord);
            Debug.Log("HDRI pixel color: \t \t" + initialHDRIColor.ToString("F6"));
            Debug.Log("HDRI shaper pixel color: \t \t" + shapedHDRIColor.ToString("F6"));
            Debug.Log("HDRI pixel color * exposure: \t" + (initialHDRIColor * exposure).ToString("F6"));
            Debug.Log("Gamut mapped Color:  \t \t" + finalHDRIColor.ToString("F6"));
            Debug.Log("--------------------------------------------------------------------------------");
        }
    }

    public Texture2D getHDRITexture()
    {
        return hdriTextureTransformed;
    }

    // public void OnRenderImage(RenderTexture src, RenderTexture dest)
    // {
    //     if (hdriTextureTransformed == null || screenGrab == null || fullScreenTextureMat == null)
    //     {
    //         Debug.LogError("Invalid data on ColorGamut1.OnRenderImage()");
    //         return;
    //     }
    //
    //     // Graphics.Blit(hdriTextureTransformed, screenGrab, fullScreenTextureMat);
    //     colorGamutMat.SetTexture("_MainTex", hdriTextureTransformed);
    //     Graphics.Blit(hdriTextureTransformed, dest, fullScreenTextureMat);
    // }

    public IEnumerator ApplyTransferFunction(RenderTexture inputRenderTexture)
    {
        int counter = maxIterationsPerFrame;
        int hdriPixelArrayLen = 0;

        float hdriMaxRGBChannel = 0.0f;
        float bleachingXCoord = 0.0f;
        float bleachingRange = 0.0f;

        float bleachingRatio = 0.0f;
        float hdriYMaxValue = 0.0f;
        float inverseSrgbEOTF = (1.0f / 2.2f);
        float rawMaxPixelValue = 0.0f;

        Color ratio = Color.black;
        Color hdriPixelColor = Color.black;

        Vector3 hdriPixelColorVec = Vector3.zero;
        Vector3 maxDynamicRangeVec = Vector3.zero;

        float[] xCoordsArray;
        float[] yCoordsArray;
        float[] tValuesArray;

        int completionStatus = 0;

        Texture2D textureToProcess = toTexture2D(inputRenderTexture);

        hdriPixelArray = textureToProcess.GetPixels();
        File.WriteAllBytes("PreTransferFunctionImage.exr", textureToProcess.EncodeToEXR());        

        hdriPixelArrayLen = hdriPixelArray.Length;
        int quarterSize = hdriPixelArrayLen / 4;
        int halfSize = hdriPixelArrayLen / 2;
        int threeQuartersSize = hdriPixelArrayLen - quarterSize;

        if (tValues == null)
            yield return new WaitForEndOfFrame();

        xCoordsArray = xValues.ToArray();
        yCoordsArray = yValues.ToArray();
        tValuesArray = tValues.ToArray();

        counter = maxIterationsPerFrame;
        mainCamera.clearFlags = CameraClearFlags.Skybox;
        curveDataState = CurveDataState.MustRecalculate;
        for (int i = 0; i < hdriPixelArrayLen; i++, counter--)
        {
            if (i == quarterSize || i == halfSize || i == threeQuartersSize)
            {
                Debug.Log("Image Processing at " + (100.0f * (float) i / (float) hdriPixelArrayLen).ToString() +
                          "%");
            }

            // if (counter <= 0)
            // {
            //     counter = maxIterationsPerFrame;
            //     yield return new WaitForEndOfFrame();
            // }

            if(float.IsNaN(hdriPixelArray[i].r) || float.IsNaN(hdriPixelArray[i].g) || float.IsNaN(hdriPixelArray[i].r))
                Debug.Log("NaN");      
            
            
            Color tempColor = hdriPixelArray[i];
            if(
                (Mathf.Approximately(tempColor.r, 0.5f) && 
                Mathf.Approximately(tempColor.g, 0.5f)  && 
                Mathf.Approximately(tempColor.b, 0.5f)) ||
               ((tempColor.r > 0.50f && tempColor.r < 0.51f) && 
                (tempColor.g > 0.50f && tempColor.g < 0.51f) && 
                (tempColor.b > 0.50f && tempColor.b < 0.51f))
               // ||
               // (Mathf.Approximately(tempColor.r, 0.43f) && 
               //  Mathf.Approximately(tempColor.g, 0.43f)  && 
               //  Mathf.Approximately(tempColor.b, 0.43f)) ||
               //  ((tempColor.r > 0.42f && tempColor.r < 0.44f) && 
               //   (tempColor.g > 0.42f && tempColor.g < 0.44f) && 
               //   (tempColor.b > 0.42f && tempColor.b < 0.44f)))
               )
                Debug.Log("hasjdhja");
            
            // Full dynamic range of image
            hdriPixelArray[i].r = Mathf.Max(0.0f, hdriPixelArray[i].r);
            hdriPixelArray[i].g = Mathf.Max(0.0f, hdriPixelArray[i].g);
            hdriPixelArray[i].b = Mathf.Max(0.0f, hdriPixelArray[i].b);

            hdriPixelColor = hdriPixelArray[i] * exposure;
            rawMaxPixelValue = hdriPixelColor.maxColorComponent;
            ratio = Color.blue;
            // Secondary Nuance Grade, guardrails
            if (hdriPixelColor.r > maxRadiometricValue || hdriPixelColor.g > maxRadiometricValue ||
                hdriPixelColor.b > maxRadiometricValue)
            {
                hdriPixelColor.r = maxRadiometricValue;
                hdriPixelColor.g = maxRadiometricValue;
                hdriPixelColor.b = maxRadiometricValue;
            }

            // Calculate Pixel max color and ratio
            hdriMaxRGBChannel = hdriPixelColor.maxColorComponent;
            ratio = hdriPixelColor / hdriMaxRGBChannel;

            bleachingXCoord = 0.0f; // Intersect of x on Y = 1

            // if (isBleachingActive)
            // {
            //     // Calculate bleaching  values by iterating through the Y values array and returning the closest x coord
            //     bleachingXCoord =
            //         parametricCurve.getXCoordinate(1.0f, xCoordsArray, yCoordsArray, tValuesArray);
            //
            //     if (hdriPixelColor.r > bleachingXCoord || hdriPixelColor.g > bleachingXCoord ||
            //         hdriPixelColor.b > bleachingXCoord)
            //     {
            //         bleachingRange = maxRadiometricValue - bleachingXCoord;
            //         bleachingRatio = (hdriPixelColor.maxColorComponent - bleachingXCoord) /
            //                          bleachingRange;
            //
            //
            //         hdriPixelColorVec.Set(hdriPixelColor.r, hdriPixelColor.g, hdriPixelColor.b);
            //         maxDynamicRangeVec.Set(maxRadiometricValue, maxRadiometricValue,
            //             maxRadiometricValue);
            //         hdriPixelColorVec = Vector3.Lerp(hdriPixelColorVec, maxDynamicRangeVec,
            //             Mathf.Pow(bleachingRatio, (float) bleachingRatioPower));
            //
            //         hdriPixelColor.r = hdriPixelColorVec.x;
            //         hdriPixelColor.g = hdriPixelColorVec.y;
            //         hdriPixelColor.b = hdriPixelColorVec.z;
            //
            //         ratio = hdriPixelColor / hdriMaxRGBChannel;
            //     }
            // }

            // Get Y value from curve using the array version 
            float yValue = parametricCurve.getYCoordinateLogXInput(hdriMaxRGBChannel, xCoordsArray, yCoordsArray,
                tValuesArray, controlPoints);

            hdriYMaxValue = Mathf.Min(yValue, 1.0f);
            ratio.a = 1.0f;
            hdriPixelColor = hdriYMaxValue * ratio;


            hdriPixelArray[i].r = hdriPixelColor.r; //Mathf.Pow(hdriPixelColor.r, inverseSrgbEOTF);
            hdriPixelArray[i].g = hdriPixelColor.g; //Mathf.Pow(hdriPixelColor.g, inverseSrgbEOTF);
            hdriPixelArray[i].b = hdriPixelColor.b; //Mathf.Pow(hdriPixelColor.b, inverseSrgbEOTF);
            hdriPixelArray[i].a = 1.0f;
        }

        hdriTextureTransformed.SetPixels(hdriPixelArray);
        hdriTextureTransformed.Apply();
        
        // Write texture to disk
        File.WriteAllBytes("PostTransferFunctionImage.exr", hdriTextureTransformed.EncodeToEXR());        
        curveDataState = CurveDataState.Calculated;
        mainCamera.clearFlags = CameraClearFlags.Nothing;
        Debug.Log("Image Processing has finished");
    }


    public IEnumerator CpuGGMIterative()
    {
        int counter = maxIterationsPerFrame;
        int hdriPixelArrayLen = 0;

        float hdriMaxRGBChannel = 0.0f;
        float bleachingXCoord = 0.0f;
        float bleachingRange = 0.0f;

        float bleachingRatio = 0.0f;
        float hdriYMaxValue = 0.0f;
        float inverseSrgbEOTF = (1.0f / 2.2f);
        float rawMaxPixelValue = 0.0f;

        Color ratio = Color.black;
        Color hdriPixelColor = Color.black;

        Vector3 hdriPixelColorVec = Vector3.zero;
        Vector3 maxDynamicRangeVec = Vector3.zero;

        float[] xCoordsArray;
        float[] yCoordsArray;
        float[] tValuesArray;

        int completionStatus = 0;

        while (true)
        {
            if (!isSweepActive)
            {
                // if (inputTextureIdx != hdriIndex)
                {
                    inputTextureIdx = hdriIndex;
                    inputTexture = HDRIList[inputTextureIdx];
                }
            }
            else
            {
                inputTexture = sweepTexture;
            }

            hdriPixelArray = inputTexture.GetPixels();
            hdriPixelArrayLen = hdriPixelArray.Length;
            int quarterSize = hdriPixelArrayLen / 4;
            int halfSize = hdriPixelArrayLen / 2;
            int threeQuartersSize = hdriPixelArrayLen - quarterSize;

            if (curveDataState != CurveDataState.Calculated)
            {
                if (isMultiThreaded && tValues != null && tValues.Count > 0)
                {
                    GamutMapJob job = new GamutMapJob();

                    NativeArray<Color> pixels = new NativeArray<Color>(hdriPixelArrayLen, Allocator.TempJob);
                    pixels.CopyFrom(hdriPixelArray);
                    job.hdriPixelArray = pixels;
                    NativeArray<float> xVals = new NativeArray<float>(xValues.Count, Allocator.TempJob);
                    xVals.CopyFrom(xValues.ToArray());
                    job.xValues = xVals;
                    NativeArray<float> yVals = new NativeArray<float>(yValues.Count, Allocator.TempJob);
                    yVals.CopyFrom(yValues.ToArray());
                    job.yValues = yVals;
                    // parametric curve
                    NativeArray<float> tVals = new NativeArray<float>(tValues.Count, Allocator.TempJob);
                    tVals.CopyFrom(tValues.ToArray());
                    job.tValues = tVals;
                    NativeArray<Vector2> controlPts = new NativeArray<Vector2>(controlPoints.Length, Allocator.TempJob);
                    controlPts.CopyFrom(controlPoints);
                    job.controlPoints = controlPts;

                    job.exposure = exposure;
                    job.isBleachingActive = isBleachingActive;
                    job.showPixelsOutOfGamut = showPixelsOutOfGamut;
                    job.lutLength = curveLutLength;
                    job.yIndexIntersect = yIndexIntersect;
                    job.minRadiometricValue = minRadiometricValue;
                    job.maxRadiometricValue = maxRadiometricValue;

                    JobHandle handle = job.Schedule(hdriPixelArrayLen, 1);
                    handle.Complete();

                    // Finished processing image, write values back
                    for (int i = 0; i < hdriPixelArrayLen; i++)
                    {
                        hdriPixelArray[i] = pixels[i];
                    }

                    hdriTextureTransformed.SetPixels(hdriPixelArray);
                    hdriTextureTransformed.Apply();
                    // Cleanup arrays
                    pixels.Dispose();
                    xVals.Dispose();
                    tVals.Dispose();
                    yVals.Dispose();
                    controlPts.Dispose();
                    Debug.Log("Multi-threaded Image Processing has finished");

                    // ChangeCurveDataState(CurveDataState.Calculated);

                    yield return new WaitForEndOfFrame();
                }
                else
                {
                    if (tValues == null)
                        yield return new WaitForEndOfFrame();

                    xCoordsArray = xValues.ToArray();
                    yCoordsArray = yValues.ToArray();
                    tValuesArray = tValues.ToArray();

                    counter = maxIterationsPerFrame;
                    for (int i = 0; i < hdriPixelArrayLen; i++, counter--)
                    {
                        if (i == quarterSize || i == halfSize || i == threeQuartersSize)
                        {
                            Debug.Log("Image Processing at " +
                                      (100.0f * (float) i / (float) hdriPixelArrayLen).ToString() + "%");
                        }

                        if (counter <= 0)
                        {
                            counter = maxIterationsPerFrame;
                            yield return new WaitForEndOfFrame();
                        }

                        // Full dynamic range of image
                        hdriPixelColor = hdriPixelArray[i] * exposure;
                        rawMaxPixelValue = hdriPixelColor.maxColorComponent;
                        ratio = Color.blue;
                        // Secondary Nuance Grade, guardrails
                        if (hdriPixelColor.r > maxRadiometricValue || hdriPixelColor.g > maxRadiometricValue ||
                            hdriPixelColor.b > maxRadiometricValue)
                        {
                            hdriPixelColor.r = maxRadiometricValue;
                            hdriPixelColor.g = maxRadiometricValue;
                            hdriPixelColor.b = maxRadiometricValue;
                        }
                        // Add guardrails for minimum values
                        // if (hdriPixelColor.r < minRadiometricValue || hdriPixelColor.g < minRadiometricValue ||
                        //     hdriPixelColor.b < minRadiometricValue)
                        // {
                        //     hdriPixelColor.r = minRadiometricValue;
                        //     hdriPixelColor.g = minRadiometricValue;
                        //     hdriPixelColor.b = minRadiometricValue;
                        // }                        


                        // Calculate Pixel max color and ratio
                        hdriMaxRGBChannel = hdriPixelColor.maxColorComponent;
                        ratio = hdriPixelColor / hdriMaxRGBChannel;

                        // Transfer function
                        if (activeTransferFunction == TransferFunction.Max_RGB)
                        {
                            bleachingXCoord = 0.0f; // Intersect of x on Y = 1

                            if (isBleachingActive)
                            {
                                // Calculate bleaching  values by iterating through the Y values array and returning the closest x coord
                                bleachingXCoord =
                                    parametricCurve.getXCoordinate(1.0f, xCoordsArray, yCoordsArray, tValuesArray);

                                if (hdriPixelColor.r > bleachingXCoord || hdriPixelColor.g > bleachingXCoord ||
                                    hdriPixelColor.b > bleachingXCoord)
                                {
                                    bleachingRange = maxRadiometricValue - bleachingXCoord;
                                    bleachingRatio = (hdriPixelColor.maxColorComponent - bleachingXCoord) /
                                                     bleachingRange;


                                    hdriPixelColorVec.Set(hdriPixelColor.r, hdriPixelColor.g, hdriPixelColor.b);
                                    maxDynamicRangeVec.Set(maxRadiometricValue, maxRadiometricValue,
                                        maxRadiometricValue);
                                    hdriPixelColorVec = Vector3.Lerp(hdriPixelColorVec, maxDynamicRangeVec,
                                        Mathf.Pow(bleachingRatio, (float) bleachingRatioPower));

                                    hdriPixelColor.r = hdriPixelColorVec.x;
                                    hdriPixelColor.g = hdriPixelColorVec.y;
                                    hdriPixelColor.b = hdriPixelColorVec.z;

                                    ratio = hdriPixelColor / hdriMaxRGBChannel;
                                }
                            }

                            // Get Y value from curve using the array version 
                            float yValue = parametricCurve.getYCoordinate(hdriMaxRGBChannel, xCoordsArray, yCoordsArray,
                                tValuesArray,
                                controlPoints);

                            hdriYMaxValue = Mathf.Min(yValue, 1.0f);
                            ratio.a = 1.0f;
                            hdriPixelColor = hdriYMaxValue * ratio;

                            if (showPixelsOutOfGamut)
                            {
                                if (rawMaxPixelValue < minRadiometricValue) // Below Gamut
                                {
                                    hdriPixelColor = Color.red;
                                }
                                else if (rawMaxPixelValue > maxRadiometricValue) // Above gamut
                                {
                                    hdriPixelColor = Color.green;
                                }
                                else if (rawMaxPixelValue > bleachingXCoord)
                                {
                                    if (Mathf.Approximately(bleachingXCoord, 0.0f))
                                    {
                                        bleachingXCoord =
                                            parametricCurve.getXCoordinate(1.0f, xCoordsArray, yCoordsArray,
                                                tValuesArray);
                                        hdriPixelColor = rawMaxPixelValue > bleachingXCoord
                                            ? Color.blue
                                            : hdriPixelColor;
                                    }
                                    else
                                    {
                                        hdriPixelColor = Color.blue;
                                    }
                                }
                            }

                            activeTransferFunction = TransferFunction.Max_RGB;
                        }
                        else
                        {
                            activeTransferFunction = TransferFunction.Per_Channel;

                            hdriPixelColor.r = parametricCurve.getYCoordinate(hdriPixelColor.r, xCoordsArray,
                                yCoordsArray, tValuesArray, controlPoints);
                            hdriPixelColor.g = parametricCurve.getYCoordinate(hdriPixelColor.g, xCoordsArray,
                                yCoordsArray, tValuesArray, controlPoints);
                            hdriPixelColor.b = parametricCurve.getYCoordinate(hdriPixelColor.b, xCoordsArray,
                                yCoordsArray, tValuesArray, controlPoints);

                            if (showPixelsOutOfGamut)
                            {
                                if (hdriPixelColor.r < minRadiometricValue || hdriPixelColor.g < minRadiometricValue ||
                                    hdriPixelColor.b < minRadiometricValue)
                                {
                                    hdriPixelColor = Color.red;
                                }
                                else if (hdriPixelColor.r > 1.0f || hdriPixelColor.g > 1.0f ||
                                         hdriPixelColor.b > 1.0f)
                                {
                                    hdriPixelColor = Color.green;
                                }
                            }
                        }

                        hdriPixelArray[i].r = hdriPixelColor.r; //Mathf.Pow(hdriPixelColor.r, inverseSrgbEOTF);
                        hdriPixelArray[i].g = hdriPixelColor.g; //Mathf.Pow(hdriPixelColor.g, inverseSrgbEOTF);
                        hdriPixelArray[i].b = hdriPixelColor.b; //Mathf.Pow(hdriPixelColor.b, inverseSrgbEOTF);
                        hdriPixelArray[i].a = 1.0f;
                    }

                    hdriTextureTransformed.SetPixels(hdriPixelArray);
                    Debug.Log("Image Processing has finished");
                }

                hdriTextureTransformed.Apply();
                ChangeCurveDataState(CurveDataState.Calculated);
            }
            else
            {
                yield return new WaitForEndOfFrame();
            }
        }
    }

    public void exportTransferFunction(string fileName)
    {
        // Set the DOMAIN_MIN and DOMAIN_MAX ranges
        Vector3 minDisplayValueVec = Vector3.zero; //new Vector3(minDisplayValue, minDisplayValue, minDisplayValue);
        Vector3 maxDisplayValueVec = Vector3.one; //new Vector3(maxDisplayValue, maxDisplayValue, maxDisplayValue);

        CurveTest parametricCurveTmp =
            new CurveTest(minExposureValue, maxExposureValue, maxRadiometricValue, maxDisplayValue);
        Vector2[] controlPointsTmp = parametricCurveTmp.createControlPoints(origin, greyPoint, slope);
        List<float> xValuesTmp = initialiseXCoordsInRange(10000, maxRadiometricValue);
        List<float> tValuesTmp = parametricCurveTmp.calcTfromXquadratic(xValuesTmp.ToArray(), controlPointsTmp);
        List<float> yValuesTmp =
            parametricCurveTmp.calcYfromXQuadratic(xValuesTmp, tValuesTmp, new List<Vector2>(controlPointsTmp));

        // @TODO - implement the actual sRGB spec calculation
        float[] yValuesEOTF = new float[yValuesTmp.Count];
        int i = 0;
        for (i = 0; i < yValuesTmp.Count; i++)
        {
            if (yValuesTmp[i] >= 1.0f)
                break;
            // Normalise our Y values to go from 0.0 to 1.0 
            // float tmpVal = (yValues[i] - minDisplayValue) / (maxDisplayValue - minDisplayValue);
            // Encode the inverse EOTF and store it in the array of Y values
            yValuesEOTF[i] = Mathf.Pow(yValuesTmp[i], (1.0f / 2.2f));
            // yValuesEOTF[i] = yValues[i];
        }

        // Pass data to be converted and written to disk as a .cube file
        CubeLutExporter.saveLutAsCube(yValuesEOTF, fileName, i /*yValues.Count*/, minDisplayValueVec,
            maxDisplayValueVec, false);
    }

    private void ChangeCurveDataState(CurveDataState newState)
    {
        // if (curveDataState == newState)
        // {
        //     Debug.LogWarning("Current state is being change to itself again");
        //     return;
        // }
        //
        // switch (newState)
        // {
        //     case CurveDataState.NotCalculated:
        //     {
        //         mainCamera.clearFlags = CameraClearFlags.Skybox;
        //         curveDataState = CurveDataState.NotCalculated;
        //         break;
        //     }
        //     case CurveDataState.Calculated:
        //     {
        //         mainCamera.clearFlags = CameraClearFlags.Nothing;
        //         curveDataState = CurveDataState.Calculated;
        //         break;
        //     }
        //     case CurveDataState.MustRecalculate:
        //     {
        //         Debug.Log("Image Processing has started");
        //         mainCamera.clearFlags = CameraClearFlags.Skybox;
        //         curveDataState = CurveDataState.MustRecalculate;
        //         break;
        //     }
        // }
    }

    // Dimension - size of the look up table being created
    // maxRange - maximum radiometric value we are using
    public List<float> initialiseXCoordsInRange(int dimension, float maxRange)
    {
        List<float> xValues = new List<float>(dimension);

        if (true)
        {
            float xCoord = 0.0f;
            for (int i = 0; i < dimension /*- 1*/; ++i)
            {
                xCoord = minRadiometricValue + (Mathf.Pow((float) i / (float) dimension, 2.0f) * maxRange);
                xValues.Add(Shaper.calculateLinearToLog(xCoord, greyPoint.x, minExposureValue, maxExposureValue));
            }
        }
        else
        {
            float halfDimensionFlt = (((float) dimension) / 2.0f);
            int halfDimensionInt = dimension / 2;
            // calculate the step used from our minimum radiometric until our mid grey point
            float stepPreMidGrey = (greyPoint.x - minRadiometricValue) / halfDimensionFlt;
            // calculate the step necessary for the second half of the values, from mid grey point until maxRange
            float stepPostMidGrey = (maxRange - greyPoint.x) / (halfDimensionFlt - 1.0f);
            float xCoord = 0.0f;

            for (int i = 0; i <= halfDimensionInt; ++i)
            {
                xCoord = MinRadiometricValue + (i * stepPreMidGrey);

                if (xCoord < MinRadiometricValue)
                    continue;

                if (Mathf.Approximately(xCoord, maxRange))
                    break;

                xValues.Add(Shaper.calculateLinearToLog(xCoord, this.greyPoint.x, minExposureValue, maxExposureValue));
                // Debug.Log("1st half - Index: " + i + " xCoord: " + xCoord + " \t Shaped Value " + xValues[i] + " \t ");
            }

            int len = (dimension % 2) == 0 ? halfDimensionInt : halfDimensionInt + 1;
            for (int i = 1; i < len; ++i)
            {
                xCoord = 0.18f + (i * stepPostMidGrey);

                if (xCoord < MinRadiometricValue)
                    continue;


                xValues.Add(Shaper.calculateLinearToLog(xCoord, this.greyPoint.x, minExposureValue, maxExposureValue));
                // Debug.Log("2nd half -Index: " + (xValues.Count - 1) + " xCoord: " + xCoord + " \t Shaped Value " + xValues[xValues.Count - 1] + " \t ");
            }
        }

        return xValues;
    }

    public void getParametricCurveValues(out float inSlope, out float originPointX, out float originPointY,
        out float greyPointX, out float greyPointY)
    {
        inSlope = slope;
        originPointX = origin.x;
        originPointY = origin.y;
        greyPointX = greyPoint.x;
        greyPointY = greyPoint.y;
    }

    public void setParametricCurveValues(float inSlope, float originPointX, float originPointY,
        float greyPointX, float greyPointY)
    {
        this.slope = inSlope;
        this.origin.x = originPointX;
        this.origin.y = originPointY;
        this.greyPoint.x = greyPointX;
        this.greyPoint.y = greyPointY;

        ChangeCurveDataState(CurveDataState.MustRecalculate);
        createParametricCurve(greyPoint, origin);
    }

    public bool getShowSweep()
    {
        return isSweepActive;
    }

    public void setShowSweep(bool isActive, Texture2D inputTexture)
    {
        isSweepActive = isActive;
        this.inputTexture = inputTexture;
        ChangeCurveDataState(CurveDataState.MustRecalculate);
    }

    public bool getIsMultiThreaded()
    {
        return isMultiThreaded;
    }

    public void setIsMultiThreaded(bool isMultiThreaded)
    {
        this.isMultiThreaded = isMultiThreaded;
    }

    public void setShowOutOfGamutPixels(bool isPixelOutOfGamut)
    {
        this.showPixelsOutOfGamut = isPixelOutOfGamut;
        ChangeCurveDataState(CurveDataState.MustRecalculate);
    }

    Texture2D toTexture2D(RenderTexture rTex)
    {
        Texture2D tex = new Texture2D(rTex.width, rTex.height, TextureFormat.RGBAHalf, false, true);
        RenderTexture.active = rTex;
        tex.ReadPixels(new Rect(0, 0, rTex.width, rTex.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        return tex;
    }

    public Vector2[] getControlPoints()
    {
        // TODO: Replace with curveStateData state
        if (controlPoints == null || controlPoints.Length == 0)
        {
            greyPoint = new Vector2(0.18f, 0.18f);
            slope = 2.2f;
            minRadiometricValue = Mathf.Pow(2.0f, -6.0f) * greyPoint.x;
            maxRadiometricValue = Mathf.Pow(2.0f, 6.0f) * greyPoint.x;
            origin = new Vector2(minRadiometricValue, 0.00001f);

            createParametricCurve(greyPoint, origin);
        }

        return controlPoints;
    }

    public List<float> getTValues()
    {
        return tValues;
    }

    public List<float> getXValues()
    {
        return xValues;
    }

    public List<float> getYValues()
    {
        return yValues;
    }

    public CurveTest getParametricCurve()
    {
        if (parametricCurve == null)
            createParametricCurve(greyPoint, origin);

        return parametricCurve;
    }

    public void setInputTexture(Texture2D inputTexture)
    {
        this.inputTexture = inputTexture;
        ChangeCurveDataState(CurveDataState.MustRecalculate);
    }

    // public void setHDRIIndex(int index)
    // {
    //     hdriIndex = index;
    //     ChangeCurveDataState(CurveDataState.MustRecalculate);
    // }

    public void setBleaching(bool inIsBleachingActive)
    {
        isBleachingActive = inIsBleachingActive;
        ChangeCurveDataState(CurveDataState.MustRecalculate);
    }

    public void setExposure(float exposure)
    {
        this.exposure = exposure;
        ChangeCurveDataState(CurveDataState.MustRecalculate);
    }

    public void setActiveTransferFunction(TransferFunction transferFunction)
    {
        this.activeTransferFunction = transferFunction;
        ChangeCurveDataState(CurveDataState.MustRecalculate);
    }

    public void setBleachingRatioPower(int ratioPower)
    {
        this.bleachingRatioPower = ratioPower;
        ChangeCurveDataState(CurveDataState.MustRecalculate);
    }

    static public Texture2D GetRTPixels(RenderTexture rt)
    {
        // Remember currently active render texture
        RenderTexture currentActiveRT = RenderTexture.active;

        // Set the supplied RenderTexture as the active one
        RenderTexture.active = rt;

        // Create a new Texture2D and read the RenderTexture image into it
        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBAHalf, false);
        tex.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);

        // Restorie previously active render texture
        RenderTexture.active = currentActiveRT;
        return tex;
    }
}

struct GamutMapJob1 : IJobParallelFor
{
    public NativeArray<Color> hdriPixelArray;
    [ReadOnly] public NativeArray<float> tValues;
    [ReadOnly] public NativeArray<float> xValues;
    [ReadOnly] public NativeArray<float> yValues;
    [ReadOnly] public NativeArray<Vector2> controlPoints;

    [ReadOnly] public float exposure;

    // [ReadOnly] public Vector2 finalKeyframe;
    [ReadOnly] public bool isBleachingActive;
    [ReadOnly] public bool showPixelsOutOfGamut;

    [ReadOnly] public int lutLength;
    [ReadOnly] public int yIndexIntersect;
    [ReadOnly] public float minRadiometricValue;
    [ReadOnly] public float maxRadiometricValue;

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

    private enum ColorRange
    {
        InGamut,
        BelowGamut,
        AboveGamut
    };

    private ColorRange colorRange;

    public float calcYfromXQuadratic(float xValue, NativeArray<float> tValues, NativeArray<Vector2> controlPoints)
    {
        float yValues = 0.0f;
        Vector2[] controlPointsArray = new Vector2[]
        {
            controlPoints[0], controlPoints[1], controlPoints[2],
            controlPoints[2], controlPoints[3], controlPoints[4],
            controlPoints[4], controlPoints[5], controlPoints[6]
        };

        for (int index = 0; index < tValues.Length; index++)
        {
            for (int i = 0; i < controlPointsArray.Length - 1; i += 3)
            {
                Vector2 p0 = controlPointsArray[0 + i];
                Vector2 p1 = controlPointsArray[1 + i];
                Vector2 p2 = controlPointsArray[2 + i];

                if (p0.x <= xValue && xValue <= p2.x)
                {
                    float tValue = tValues[index];
                    yValues = (Mathf.Pow(1.0f - tValue, 2.0f) * p0.y) +
                              (2.0f * (1.0f - tValue) * tValue * p1.y) +
                              (Mathf.Pow(tValue, 2.0f) * p2.y);

                    return yValues;
                }
            }
        }

        return yValues;
    }

    public static float ClosestTo(NativeArray<float> list, float target, out int index)
    {
        float closest = float.MaxValue;
        float minDifference = float.MaxValue;
        float prevDifference = float.MaxValue;
        int outIndex = 0;
        int listSize = list.Length;
        for (int i = 0; i < listSize; i++)
        {
            float difference = Mathf.Abs((float) list[i] - target);

            // Early exit
            if (prevDifference < difference)
                break;

            if (minDifference > difference)
            {
                minDifference = difference;
                closest = list[i];
                outIndex = i;
            }

            prevDifference = difference;
        }

        index = outIndex;
        return closest;
    }

    public float getXCoordinate(float inputYCoord, NativeArray<float> YCoords, NativeArray<float> tValues,
        NativeArray<Vector2> controlPoints)
    {
        if (YCoords.Length <= 0 || tValues.Length <= 0)
        {
            Debug.Log("Input array of y values or t values are invalid ");
            return -1.0f;
        }

        Vector2[] controlPointsArray = new Vector2[]
        {
            controlPoints[0], controlPoints[1], controlPoints[2],
            controlPoints[2], controlPoints[3], controlPoints[4],
            controlPoints[4], controlPoints[5], controlPoints[6]
        };

        for (int i = 0; i < controlPointsArray.Length - 1; i += 3)
        {
            Vector2 p0 = controlPointsArray[0 + i];
            Vector2 p1 = controlPointsArray[1 + i];
            Vector2 p2 = controlPointsArray[2 + i];

            if (p0.y <= inputYCoord && inputYCoord <= p2.y)
            {
                // Search closest x value to xValue and grab its index in the array too
                // The array index is used to lookup the tValue
                int idx = 0;
                ClosestTo(YCoords, inputYCoord, out idx);
                float tValue = tValues[idx];

                return (Mathf.Pow(1.0f - tValue, 2.0f) * p0.x) +
                       (2.0f * (1.0f - tValue) * tValue * p1.x) +
                       (Mathf.Pow(tValue, 2.0f) * p2.x);
            }
        }

        return -1.0f;
    }

    public float getYCoordinate(float inputXCoord, NativeArray<float> xCoords,
        NativeArray<float> tValues, NativeArray<Vector2> controlPoints)
    {
        if (xCoords.Length <= 0 || tValues.Length <= 0)
        {
            Debug.Log("Input array of x values or t values have mismatched lengths ");
            return -1.0f;
        }

        Vector2[] controlPointsArray = new Vector2[]
        {
            controlPoints[0], controlPoints[1], controlPoints[2],
            controlPoints[2], controlPoints[3], controlPoints[4],
            controlPoints[4], controlPoints[5], controlPoints[6]
        };

        for (int i = 0; i < controlPointsArray.Length - 1; i += 3)
        {
            Vector2 p0 = controlPointsArray[0 + i];
            Vector2 p1 = controlPointsArray[1 + i];
            Vector2 p2 = controlPointsArray[2 + i];

            if (p0.x <= inputXCoord && inputXCoord <= p2.x)
            {
                // Search closest x value to xValue and grab its index in the array too
                // The array index is used to lookup the tValue
                int idx = 0;
                ClosestTo(xCoords, inputXCoord, out idx);
                float tValue = tValues[idx];

                return (Mathf.Pow(1.0f - tValue, 2.0f) * p0.y) +
                       (2.0f * (1.0f - tValue) * tValue * p1.y) +
                       (Mathf.Pow(tValue, 2.0f) * p2.y);
            }
        }

        return -1.0f;
    }

    public void Execute(int index)
    {
        hdriPixelColor = hdriPixelArray[index] * exposure;
        ratio = Color.black;
        inverseSrgbEOTF = 1.0f / 2.2f;
        colorRange = ColorRange.InGamut;

        if (hdriPixelColor.r > maxRadiometricValue || hdriPixelColor.g > maxRadiometricValue ||
            hdriPixelColor.b > maxRadiometricValue)
        {
            hdriPixelColor.r = maxRadiometricValue;
            hdriPixelColor.g = maxRadiometricValue;
            hdriPixelColor.b = maxRadiometricValue;
        }

        // Calculate Pixel max color and ratio
        hdriMaxRGBChannel = hdriPixelColor.maxColorComponent;
        ratio = hdriPixelColor / hdriMaxRGBChannel;

        bleachStartPoint = 1.0f;

        if (isBleachingActive)
        {
            // Calculate bleaching  values by iterating through the Y values array and returning the closest x coord
            // bleachingXCoord = parametricCurve.getXCoordinate(1.0f, yValues, tValues, new List<Vector2>(controlPoints));
            float bleachingXCoord =
                getXCoordinate(1.0f, yValues, tValues, controlPoints);

            if (hdriPixelColor.r > bleachingXCoord || hdriPixelColor.g > bleachingXCoord ||
                hdriPixelColor.b > bleachingXCoord)
            {
                bleachingRange = maxRadiometricValue - bleachingXCoord;
                bleachingRatio = (hdriPixelColor.maxColorComponent - bleachingXCoord) /
                                 bleachingRange;

                hdriPixelColorVec.Set(hdriPixelColor.r, hdriPixelColor.g, hdriPixelColor.b);
                maxDynamicRangeVec.Set(maxRadiometricValue, maxRadiometricValue, maxRadiometricValue);
                hdriPixelColorVec = Vector3.Lerp(hdriPixelColorVec, maxDynamicRangeVec,
                    bleachingRatio);

                hdriPixelColor.r = hdriPixelColorVec.x;
                hdriPixelColor.g = hdriPixelColorVec.y;
                hdriPixelColor.b = hdriPixelColorVec.z;

                ratio = hdriPixelColor / hdriMaxRGBChannel;
            }
        }

        // Get Y curve value
        float yValue = getYCoordinate(hdriMaxRGBChannel, xValues, tValues,
            controlPoints);
        hdriYMaxValue = Mathf.Min(yValue, 1.0f);
        hdriPixelColor = hdriYMaxValue * ratio;

        if (showPixelsOutOfGamut)
        {
            if (yValue < 0.0f || hdriMaxRGBChannel < minRadiometricValue) // Below Gamut
            {
                hdriPixelColor = Color.red;
            }
            else if (yValue > 1.0f) // Above gamut
            {
                hdriPixelColor = Color.green;
            }
        }

        tempResult.r = Mathf.Pow(hdriPixelColor.r, inverseSrgbEOTF);
        tempResult.g = Mathf.Pow(hdriPixelColor.g, inverseSrgbEOTF);
        tempResult.b = Mathf.Pow(hdriPixelColor.b, inverseSrgbEOTF);
        tempResult.a = 1.0f;

        hdriPixelArray[index] = tempResult;
    }
}