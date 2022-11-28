using System;
using System.Collections.Generic;
using System.Numerics;
using UnityEditor.PackageManager;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;


/// <summary>
/// This class implements the curve used for gamut mapping (brings the radiometric calculated values into display linear)
/// The curve is composed of 3 Bezier curves with one for the toe of the curve, another for the middle section and
/// the last curve for the shoulder of the curve.
/// </summary>
public class GamutCurve
{
    public float MaxDisplayValue
    {
        get => maxDisplayValue;
        set => maxDisplayValue = value;
    }

    public float MinDisplayValue
    {
        get => minDisplayValue;
        set => minDisplayValue = value;
    }

    public float MaxRadiometricDynamicRange
    {
        get => maxRadiometricDynamicRange;
        set => maxRadiometricDynamicRange = value;
    }

    public float MinRadiometricDynamicRange
    {
        get => minRadiometricDynamicRange;
        set => minRadiometricDynamicRange = value;
    }

    private float minRadiometricExposure;
    private float maxRadiometricExposure;

    private float maxRadiometricDynamicRange;
    private float minRadiometricDynamicRange;
    private float maxDisplayValue;
    private float minDisplayValue;
    private float minDisplayExposure;
    private float maxDisplayExposure;
    private float maxLatitudeLimit;
    private float maxRadiometricLatitude;
    private float maxRadiometricLatitudeExposure;
    private Vector2 logMiddleGrey;
    private Vector2 radiometricMiddleGrey;


    public GamutCurve(float minRadiometricExposure, float maxRadiometricExposure, float maxRadiometricDynamicRange,
        float maxDisplayValue, float minDisplayExposure,
        float maxDisplayExposure, float maxRadiometricLatitude, float maxRadiometricLatitudeExposure,
        float maxLatitudeLimit)
    {
        this.minRadiometricExposure = minRadiometricExposure;
        this.maxRadiometricExposure = maxRadiometricExposure;
        this.maxRadiometricDynamicRange = maxRadiometricDynamicRange;
        this.maxDisplayValue = maxDisplayValue;
        this.minDisplayExposure = minDisplayExposure;
        this.maxDisplayExposure = maxDisplayExposure;
        this.maxLatitudeLimit = maxLatitudeLimit;
        this.maxRadiometricLatitude = maxRadiometricLatitude;
        this.maxRadiometricLatitudeExposure = maxRadiometricLatitudeExposure;

    }

    /// <summary>
    /// Calculates a number of control points for 3 quadratic Bezier curves
    /// </summary>
    /// <param name="originCoord"> Coordinates of the origin of the curve</param>
    /// <param name="greyPoint"> Coordinates of the middle grey point used. This is usually (0.18, 0.18)</param>
    /// <param name="slope"> Curve slope </param>
    /// <returns></returns>
    public Vector2[] CreateControlPoints(Vector2 originCoord, Vector2 midGrey, float slope)
    {
        float toeP2YLinearValue = 0.00055f;
        float toeP1YLinearValue = 0.00001f;

        minRadiometricDynamicRange = originCoord.x;
        minDisplayValue = originCoord.y;
        radiometricMiddleGrey.x = midGrey.x;
        radiometricMiddleGrey.y = midGrey.y;
        // Convert mid grey
        logMiddleGrey = new Vector2(
            Shaper.CalculateLinearToLog2(midGrey.x, midGrey.x, minRadiometricExposure, maxRadiometricExposure),
            Shaper.CalculateLinearToLog2(midGrey.y, midGrey.y, minDisplayExposure, maxDisplayExposure));
        float toeP2YCoord =
            Shaper.CalculateLinearToLog2(toeP2YLinearValue, midGrey.y, minDisplayExposure, maxDisplayExposure);
        float toeP1YCoord =
            Shaper.CalculateLinearToLog2(toeP1YLinearValue, midGrey.y, minDisplayExposure, maxDisplayExposure);


        Vector2[] controlPoints = new Vector2[7];
        // P0, P1 and P2 correspond to the originCoord, control point and final point of a quadratic Bezier curve
        // We will design our curve from 3 separate Bezier curves: toe, middle linear section, shoulder
        Vector2 toeP0Coords = originCoord; /*new Vector2(originCoord.x, originCoord);*/ // // originCoord of plot
        Vector2 toeP1Coords = new Vector2(0.0f, 0.0f); // We don't know where it will be yet
        Vector2 toeP2Coords = new Vector2(0.0f, toeP2YCoord);
        Vector2 midP1Coords = new Vector2(0.0f, 0.0f); // Unknown at this point
        Vector2 shP0Coords = this.logMiddleGrey;
        Vector2 shP1Coords = new Vector2(0.0f, maxDisplayValue); // Unknown at this point
        Vector2 shP2Coords = new Vector2(maxLatitudeLimit, maxDisplayValue);

        // calculate y intersection when y = 0
        float b = calculateLineYIntercept(this.logMiddleGrey.x, this.logMiddleGrey.y, slope);
        // Calculate the coords for P1 in the first segment
        float xP1Coord = calculateLineX(0.0f, b, slope);
        toeP1Coords.y = toeP1YCoord; //0.001f;
        toeP1Coords.x = xP1Coord;
        // Calculate the toe's P2 using an already known Y value and the equation y = mx + b
        toeP2Coords.x = calculateLineX(toeP2Coords.y, b, slope);
        // Calculate the middle linear's section (x, y) coords
        midP1Coords = (shP0Coords + toeP2Coords) / 2.0f;
        // calculate shoulder's P1 which amounts to knowing the x value when y = 1.0
        shP1Coords.x = calculateLineX(shP1Coords.y, b, slope);

        // Create bezier curve for toe
        // P0: toeP0Coords   P1: toeP1Coords   P2: toeP2Coords
        controlPoints[0] = new Vector2(toeP0Coords.x, toeP0Coords.y);
        controlPoints[1] = new Vector2(toeP1Coords.x, toeP1Coords.y);
        controlPoints[2] = new Vector2(toeP2Coords.x, toeP2Coords.y);

        // Create bezier for middle section
        // P0: toeP2Coords   P1: midP1Coords   P2: shP0Coords
        controlPoints[3] = new Vector2(midP1Coords.x, midP1Coords.y);

        // Create bezier curve for shoulder
        // P0: shP0Coords    P1: shP1Coords    P2: shP2Coords
        controlPoints[4] = new Vector2(shP0Coords.x, shP0Coords.y);
        controlPoints[5] = new Vector2(shP1Coords.x, shP1Coords.y);
        controlPoints[6] = new Vector2(shP2Coords.x, shP2Coords.y);

        return controlPoints;
    }

    public List<float> CalcYfromXQuadratic(List<float> inXCoords, List<float> tValues, List<Vector2> controlPoints)
    {
        if (inXCoords.Count <= 0 || tValues.Count <= 0)
        {
            Debug.Log("Input array of x values or t values have mismatched lengths ");
            return null;
        }

        List<float> yValues = new List<float>();
        Vector2[] controlPointsArray = new Vector2[]
        {
                controlPoints[0], controlPoints[1], controlPoints[2],
                controlPoints[2], controlPoints[3], controlPoints[4],
                controlPoints[4], controlPoints[5], controlPoints[6]
        };


        for (int index = 0; index < inXCoords.Count; index++)
        {
            for (int i = 0; i < controlPointsArray.Length - 1; i += 3)
            {
                Vector2 p0 = controlPointsArray[0 + i];
                Vector2 p1 = controlPointsArray[1 + i];
                Vector2 p2 = controlPointsArray[2 + i];

                float xValue = inXCoords[index];

                if (p0.x <= xValue && xValue <= p2.x)
                {
                    float tValue = CalcTfromXquadratic(xValue, controlPoints.ToArray());

                    float yVal = (Mathf.Pow(1.0f - tValue, 2.0f) * p0.y) +
                                 (2.0f * (1.0f - tValue) * tValue * p1.y) +
                                 (Mathf.Pow(tValue, 2.0f) * p2.y);
                    yValues.Add(yVal);
                    break;
                }
                else if (xValue >= controlPoints[6].x)
                {
                    int lastIndex = yValues.Count - 1;
                    yValues.Add(yValues[lastIndex] + 0.0001f);
                    break;
                }
                else if (xValue <= controlPoints[0].x)
                {
                    float value = (index > 0) ? (yValues[index - 1] + 0.00001f) : 0.00001f;
                    yValues.Add(value);
                    break;
                }
            }
        }

        return yValues;
    }

    // Assumption: the input float[] is always sorted from smallest to largest values
    public void BilinearClosestTo(float[] inputArray, float target, Vector2[] controlPoints, out int arrayIndex,
        out int arrayIndex2)
    {
        int outIndex = 0;
        int outIndex2 = 0;

        int maxArrayIndex = inputArray.Length - 1;
        outIndex = Mathf.Clamp(Mathf.RoundToInt(target * maxArrayIndex), 0, maxArrayIndex);

        // If we find an entry which is a perfect match, just return that index
        if (Mathf.Approximately(inputArray[outIndex], target))
        {
            arrayIndex = outIndex;
            arrayIndex2 = outIndex;
            return;
        }

        int indexBefore = Mathf.Clamp((outIndex - 1), 0, maxArrayIndex);
        int indexAfter = Mathf.Clamp((outIndex + 1), 0, maxArrayIndex);
        float currentDiffBefore = Mathf.Abs((float)inputArray[indexBefore] - target);
        float currentDiffAfter = Mathf.Abs((float)inputArray[indexAfter] - target);
        outIndex2 = (currentDiffBefore < currentDiffAfter) ? indexBefore : indexAfter;

        arrayIndex = outIndex;
        arrayIndex2 = outIndex2;
    }

    // Assumption: the input float[] is always sorted from smallest to largest values
    public float ClosestTo(float[] inputArray, float target, Vector2[] controlPoints, out int arrayIndex)
    {
        float closest = float.MaxValue;
        float minDifference = float.MaxValue;
        float prevDifference = float.MaxValue;

        int outIndex = 0;
        int listSize = inputArray.Length;
        for (int i = 0; i < listSize; i++)
        {
            float currentDifference = Mathf.Abs((float)inputArray[i] - target);

            // Early exit because the array is always ordered from smallest to largest
            if (prevDifference < currentDifference)
                break;

            if (minDifference > currentDifference)
            {
                minDifference = currentDifference;
                closest = inputArray[i];
                outIndex = i;
            }

            prevDifference = currentDifference;
        }

        arrayIndex = outIndex;
        return closest;
    }

    public float GetYCoordinateLogXInput(float inputXCoord, float[] xCoords, float[] yCoords, float[] tValues,
        Vector2[] controlPoints)
    {
        if (xCoords.Length <= 0 || yCoords.Length <= 0 || tValues.Length <= 0)
        {
            Debug.Log("Input array of x/y coords or t values have mismatched lengths ");
            return -1.0f;
        }

        // Shape the input x coord in radiometric
        float logInputXCoord = inputXCoord;

        int idx = 0;
        int idx2 = 0;
        BilinearClosestTo(xCoords, logInputXCoord, controlPoints, out idx, out idx2);
        if (idx >= tValues.Length || idx2 >= tValues.Length)
        {
            Debug.Log("Index " + idx.ToString() + "or Index " + idx2.ToString() + " is invalid");
        }

        float linearInputXCoord =
            Shaper.CalculateLog2ToLinear(logInputXCoord, radiometricMiddleGrey.x, minRadiometricExposure,
                maxRadiometricExposure);
        float linearXCoordIdx =
            Shaper.CalculateLog2ToLinear(xCoords[idx], radiometricMiddleGrey.x, minRadiometricExposure,
                maxRadiometricExposure);
        float linearXCoordIdx2 =
            Shaper.CalculateLog2ToLinear(xCoords[idx2], radiometricMiddleGrey.x, minRadiometricExposure,
                maxRadiometricExposure);

        // Calculate interpolation factor
        if (idx == idx2)
        {
            return yCoords[idx];
        }
        else if (idx < idx2)
        {
            float lerpValue = (linearInputXCoord - linearXCoordIdx) / (linearXCoordIdx2 - linearXCoordIdx);
            return Mathf.Lerp(yCoords[idx], yCoords[idx2], lerpValue);
        }
        else
        {
            float lerpValue = (linearInputXCoord - linearXCoordIdx2) / (linearXCoordIdx - linearXCoordIdx2);
            return Mathf.Lerp(yCoords[idx2], yCoords[idx], lerpValue);
        }
    }

    // Return the t value that corresponds to a specific x input coordinate of a quadratic Bezier
    // curve
    public float CalcTfromXquadratic(float xValue, Vector2[] controlPoints)
    {
        float rootValue = -1.0f;
        if (controlPoints.Length < 3)
        {
            Debug.Log("Not enough control points used as input");
            return 0.0f;
        }

        Vector2[] controlPointsArray = new Vector2[]
        {
                controlPoints[0], controlPoints[1], controlPoints[2],
                controlPoints[2], controlPoints[3], controlPoints[4],
                controlPoints[4], controlPoints[5], controlPoints[6]
        };

        double[] coefficients = new double[3];
        float tmpRoot = -1.0f;

        for (int i = 0; i < controlPointsArray.Length - 1; i += 3)
        {
            Vector2 p0 = controlPointsArray[0 + i];
            Vector2 p1 = controlPointsArray[1 + i];
            Vector2 p2 = controlPointsArray[2 + i];

            if (p0.x <= xValue && xValue <= p2.x)
            {
                coefficients[0] = p0.x - xValue;
                coefficients[1] = (2.0f * p1.x) - (2.0f * p0.x);
                coefficients[2] = p0.x - (2.0f * p1.x) + p2.x;

                Complex[] roots = SolveQuadraticEquation(coefficients[2], coefficients[1], coefficients[0]);
                // check if it is complex
                for (int idx = 0; idx < roots.Length; idx++)
                {
                    float rootAtIndex = (float)roots[idx].Real;
                    if (tmpRoot < 0.0f || (rootAtIndex >= 0.0f && rootAtIndex <= 1.0f))
                    {
                        tmpRoot = rootAtIndex;
                    }
                }

                if (tmpRoot >= 0.0 && tmpRoot <= 1.0)
                {
                    rootValue = tmpRoot;
                    tmpRoot = -1.0f;
                    break;
                }
            }
            else if (xValue > controlPoints[6].x)
            {
                rootValue = controlPoints[6].x;
                break;
            }
        }

        if (rootValue < 0.0f)
           Debug.Log("Invalid root value being returned for " + xValue + " in CalcTfromXquadratic()");

        return rootValue;
    }


    private Complex[] SolveQuadraticEquation(double a, double b, double c)
    {
        Complex[] roots = new Complex[2];
        var q = -(b + Math.Sign(b) * Complex.Sqrt(b * b - 4 * a * c)) / 2;
        roots[0] = q / a;
        roots[1] = c / q;
        return roots;
    }

    // Array based implementation to calculate the implicit t values needed for a quadratic Bezier
    // curve from an array of x coordinates
    public List<float> CalcTfromXquadratic(float[] xValues, Vector2[] controlPoints)
    {
        List<float> rootsLst = new List<float>();
        if (controlPoints.Length < 3)
        {
            Debug.Log("Not enough control points used as input");
            return rootsLst;
        }

        Vector2[] controlPointsArray = new Vector2[]
        {
                controlPoints[0], controlPoints[1], controlPoints[2],
                controlPoints[2], controlPoints[3], controlPoints[4],
                controlPoints[4], controlPoints[5], controlPoints[6]
        };

        double[] coefficients = new double[3];
        float tmpRoot = -1.0f;
        int xValuesLength = xValues.Length;
        for (int index = 0; index < xValuesLength; index++)
        {
            for (int i = 0; i < controlPointsArray.Length - 1; i += 3)
            {
                Vector2 p0 = controlPointsArray[0 + i];
                Vector2 p1 = controlPointsArray[1 + i];
                Vector2 p2 = controlPointsArray[2 + i];

                if (p0.x <= xValues[index] && xValues[index] <= p2.x)
                {
                    coefficients[0] = p0.x - xValues[index];
                    coefficients[1] = (2.0f * p1.x) - (2.0f * p0.x);
                    coefficients[2] = p0.x - (2.0f * p1.x) + p2.x;

                    Complex[] roots = SolveQuadraticEquation(coefficients[2], coefficients[1], coefficients[0]);
                    // check if it is positive and smaller than 1.0f
                    for (int idx = 0; idx < roots.Length; idx++)
                    {
                        float rootAtIndex = (float)roots[idx].Real;
                        if (tmpRoot < 0.0f || (rootAtIndex >= 0.0f && rootAtIndex <= 1.0f) ||
                            Mathf.Approximately(rootAtIndex, 1.0f))
                        {
                            tmpRoot = rootAtIndex;
                        }
                        else
                        {
                            Debug.Log("Invalid Root");
                        }
                    }

                    if (tmpRoot >= 0.0 && tmpRoot <= 1.0)
                    {
                        rootsLst.Add(tmpRoot);
                        tmpRoot = -1.0f;
                        break;
                    }
                    else
                    {
                        Debug.Log("No roots found");
                    }
                }
                else if (xValues[index] >= controlPoints[6].x || xValues[index] <= controlPoints[0].x)
                {
                    rootsLst.Add(xValues[index]);
                    tmpRoot = -1.0f;
                    break;
                }
            }
        }

        return rootsLst;
    }

    //# Calculate the Y intercept based on slope and an X / Y coordinate pair.
    public float calculateLineYIntercept(float inX, float inY, float slope)
    {
        return (inY - (slope * inX));
    }

    // Calculate the X of a line given by the slope and Y intercept.
    public float calculateLineX(float inY, float yIntercept, float slope)
    {
        return (inY - yIntercept) / slope;
    }
}
