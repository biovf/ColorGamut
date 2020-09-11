using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using MathNet.Numerics;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;

//using MathNet;
public class CurveTest
{
    public CurveTest()
    {
    }

    float mix(float a, float b, float t)
    {
        // degree 1
        return a * (1.0f - t) + b * t;
    }

    float BezierQuadratic(float A, float B, float C, float t)
    { 
        // degree 2
        float AB = mix(A, B, t);
        float BC = mix(B, C, t);
        return mix(AB, BC, t);
    }

    // Given two points of a line, calculate the x coordinate of a point P given its Y coordinate 
    public float calculateXCoord(Vector2 p1, Vector2 p2, float y) 
    {
        return ((p2.x - p1.x) * (y - p1.y) / (p2.y - p1.y)) + p1.x;

    }

    // we want m/slope to vary between 0.3 and 4.5
    public Vector2[] createCurveControlPoints(Vector2 greyPoint, float slope, Vector2 origin)
    {
        Vector2[] controlPoints = new Vector2[7];
        // P0, P1 and P2 correspond to the origin, control point and final point of a quadratic Bezier curve
        // We will design our curve from 3 separate Bezier curves: toe, middle linear section, shoulder
        Vector2 toeP0Coords = origin;                                // origin of plot
        Vector2 toeP1Coords = new Vector2(0.0f, 0.0f);        // We don't know where it will be yet
        Vector2 toeP2Coords = new Vector2(0.0f, 0.085f);
        Vector2 midP1Coords = new Vector2(0.0f, 0.0f);        // Unknown
        Vector2 shP0Coords = greyPoint;
        Vector2 shP1Coords = new Vector2(0.0f, 1.0f);         // Unknown
        Vector2 shP2Coords = new Vector2(12.0f, 1.0f);
        
        // calculate y intersection when y = 0
        float b = calculateLineYIntercept(greyPoint.x, greyPoint.y, slope);
        // Calculate the coords for P1 in the first segment
        float xP1Coord = calculateLineX(0.0f, b, slope);
        toeP1Coords.y = float.Epsilon;
        toeP1Coords.x = xP1Coord;
        // Calculate the toe's P2 using an already known Y value and the equation y = mx + b 
        toeP2Coords.x = (toeP2Coords.y - b) / slope;
        // Calculate the middle linear's section (x, y) coords
        midP1Coords = (shP0Coords + toeP2Coords) / 2.0f;
        // calculate shoulder's P1 which amounts to knowing the x value when y = 1.0 
        shP1Coords.x = calculateLineX(shP1Coords.y, b, slope);
        
        // Create bezier curve for toe      P0: toeP0Coords   P1: toeP1Coords   P2: toeP2Coords
        controlPoints[0] = toeP0Coords;
        controlPoints[1] = toeP1Coords;
        controlPoints[2] = toeP2Coords;
        // Create bezier for middle section P0: toeP2Coords   P1: midP1Coords   P2: shP0Coords
        controlPoints[3] = midP1Coords;
        // Create bezier curve for shoulder P0: shP0Coords    P1: shP1Coords    P2: shP2Coords
        controlPoints[4] = shP0Coords;
        controlPoints[5] = shP1Coords;
        controlPoints[6] = shP2Coords;

        // for (int i = 0; i < controlPoints.Length; i++)
        // {
        //     Debug.Log("P" + i + " " + controlPoints[i].ToString("G4"));
        // }
        
        return controlPoints;

     
    }

    // @TODO Refactor to use fixed arrays as inputs instead of Lists
    public List<float> calcYfromXQuadratic(List<float> xValues, List<float> tValues, List<Vector2> controlPoints)
    {
        
        if (xValues.Count <= 0 || tValues.Count <= 0 /*|| xValues.Count != tValues.Count*/)
        {
            Debug.Log("Input array of x values or t values have mismatched lengths ");
            return null;
        }
        List<float> yValues = new List<float>();
        Vector2[] controlPointsArray = new Vector2[]{ 
            controlPoints[0], controlPoints[1], controlPoints[2],
            controlPoints[2], controlPoints[3], controlPoints[4],
            controlPoints[4], controlPoints[5], controlPoints[6]};

        double[] coefficients = new double[3];
        for (int index = 0; index < xValues.Count; index++)
        {
            for (int i = 0; i < controlPointsArray.Length - 1 ; i += 3)
            {
                Vector2 p0 = controlPointsArray[0 + i];
                Vector2 p1 = controlPointsArray[1 + i];
                Vector2 p2 = controlPointsArray[2 + i];

                if (p0.x <= xValues[index] && xValues[index] <= p2.x)
                {
                    float yVal = (
                        ((Mathf.Pow(1.0f - tValues[index], 2.0f) * p0.y) +
                        (2.0f * (1.0f - tValues[index]) * tValues[index] * p1.y) +
                        (Mathf.Pow(tValues[index], 2.0f) * p2.y)));
                    yValues.Add(yVal);
                }
            }
        }
        return yValues;
        
    }

    // public float getYfromX(float xValue, float tValue)
    // {
    //     
    //     
    // }


    public List<float> calcTfromXquadratic(List<float> xValues, List<Vector2> controlPoints)
    {
        List<float> rootsLst = new List<float>();
        if (controlPoints.Count < 3)
        {
            Debug.LogError("Not enough control points used as input");
            return rootsLst;
        }

        Vector2[] controlPointsArray = new Vector2[]{ controlPoints[0], controlPoints[1], controlPoints[2],
                                                      controlPoints[2], controlPoints[3], controlPoints[4],
                                                      controlPoints[4], controlPoints[5], controlPoints[6]};

        double[] coefficients = new double[3];
        float tmpRoot = -1.0f;
        for (int index = 0; index < xValues.Count; index++)
        {
            for (int i = 0; i < controlPointsArray.Length - 1 ; i += 3)
            {
                Vector2 p0 = controlPointsArray[0 + i];
                Vector2 p1 = controlPointsArray[1 + i];
                Vector2 p2 = controlPointsArray[2 + i];

                if (p0.x <= xValues[index] && xValues[index] <= p2.x)
                {
                    coefficients[0] = p0.x - xValues[index];
                    coefficients[1] = (2.0f * p1.x) - (2.0f * p0.x);
                    coefficients[2] = p0.x - (2.0f * p1.x) + p2.x;

                    Complex[] roots = FindRoots.Polynomial(coefficients);
                    for (int idx = 0; idx < roots.Length; idx++)
                    {
                        if (tmpRoot < 0.0f || roots[idx].Real < tmpRoot)
                        {
                            tmpRoot = (float)roots[idx].Real;
                        }
                    }
                    if (tmpRoot >= 0.0 && tmpRoot <= 10000)
                    {
                        // Debug.Log("X value " + xValues[index] + " Adding " + tmpRoot);
                        rootsLst.Add(tmpRoot);
                        tmpRoot = -1.0f;
                        break;
                    }
                }
            }
        }
        return rootsLst;
    }


    // Return the (x,y) value for a given input t and 3 control points p0, p1 and p2
    public Vector2 CalculateQuadraticBezierPoint(float t, Vector2 p0, Vector2 p1, Vector2 p2)
    {
        t = Mathf.Clamp01(t);
        float u = 1 - t;
        float uu = u * u;
        float tt = t * t;
        Vector2 res = uu * p0;
        res += 2 * u * t * p1;
        res += tt * p2;
        
        return res;
    }
    
    float calculateEV2RL(float inEV, float rlMiddleGrey = 0.18f) 
    {
        return Mathf.Pow(2.0f, inEV) * rlMiddleGrey;

    }
    
    // Convert radiometric linear value to relative EV
    float calculateRL2EV(float inRl, float rlMiddleGrey = 0.18f)
    {
        return Mathf.Log(inRl, 2.0f) - Mathf.Log(rlMiddleGrey, 2.0f);
    }
    
    //# Calculate the Y intercept based on slope and an X / Y coordinate pair.
    float calculateLineYIntercept(float inX, float inY, float slope) 
    {
        return (inY - (slope * inX));
    }
    
    // Calculate the Y of a line given by slope and X coordinate.
    float calculateLineY(float inX, float yIntercept, float slope) 
    {
        return (slope * inX) + yIntercept;
    }
    // Calculate the X of a line given by the slope and Y intercept.
    float calculateLineX(float inY, float yIntercept, float slope) 
    {
        return (inY - yIntercept / slope);
    }

    //# Calculate the slope of a line given by two coordinates.
    float calculateLineSlope(float inX1, float inY1, float inX2, float inY2) 
    {
        return (inX1 - inX2) / (inY1 - inY2);
    }

   


}
