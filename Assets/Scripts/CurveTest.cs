using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using MathNet;
public class CurveTest
{
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

    // we want m to vary between 0.3 and 4.5
    public void linearSection(Vector2 greyPoint, float slope) 
    {


        // calculate y intersection when x = 0
        float b = calculateLineYIntercept(greyPoint.x, greyPoint.y, slope);
        Debug.Log("b = " + b.ToString());
        float xCoordAtYEqualsToOne = calculateLineX(1.0f, b, slope);
        Debug.Log("At Y = 1.0 the coord X = " + xCoordAtYEqualsToOne);
        // Create bezier curve for toe
        // Create bezier curve for shoulder



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
