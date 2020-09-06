using System.Collections;
using System.Collections.Generic;
using UnityEngine;


// sRGB to XYZ matrix:
// [[ 0.4124  0.3576  0.1805]
// [ 0.2126  0.7152  0.0722]
// [ 0.0193  0.1192  0.9505]]
//
// XYZ to sRGB matrix:
// [[ 3.24062548 -1.53720797 -0.4986286 ]
// [-0.96893071  1.87575606  0.04151752]
// [ 0.05571012 -0.20402105  1.05699594]]

// Display P3 RGB to XYZ matrix:
// [[  4.86570949e-01   2.65667693e-01   1.98217285e-01]
// [  2.28974564e-01   6.91738522e-01   7.92869141e-02]
// [ -3.97207552e-17   4.51133819e-02   1.04394437e+00]]
//
// XYZ to Display P3 matrix:
// [[ 2.49349691 -0.93138362 -0.40271078]
// [-0.82948897  1.76266406  0.02362469]
// [ 0.03584583 -0.07617239  0.95688452]]

public class ColorSpace
{
    private Matrix4x4 srgbToXyzMat = new Matrix4x4( new Vector4(0.4124f , 0.3576f,  0.1805f, 0.0f), 
                                            new Vector4(0.2126f , 0.7152f,  0.0722f, 0.0f), 
                                            new Vector4(0.0193f , 0.1192f,  0.9505f, 0.0f), 
                                            new Vector4(0.0f, 0.0f, 0.0f, 0.0f));
    
    
    private Matrix4x4 xyzToSrgbMat = new Matrix4x4(new Vector4(3.24062548f, -1.53720797f,-0.4986286f, 0.0f), 
                                        new Vector4(-0.96893071f, 1.87575606f,0.04151752f, 0.0f), 
                                        new Vector4(0.05571012f, -0.20402105f,1.05699594f, 0.0f), 
                                        new Vector4(0.0f, 0.0f, 0.0f, 0.0f));
    
    private Matrix4x4 displayP3ToXyzMat = new Matrix4x4( new Vector4(0.486570949f , 0.265667693f,  0.198217285f, 0.0f), 
                                            new Vector4(0.228974564f , 0.691738522f,  0.0792869141f, 0.0f), 
                                            new Vector4(0.0f , 0.0451133819f,  1.04394437f, 0.0f), 
                                            new Vector4(0.0f, 0.0f, 0.0f, 0.0f));

    private Matrix4x4 xyzToDisplayP3Mat = new Matrix4x4( new Vector4(2.49349691f , -0.93138362f,  -0.40271078f, 0.0f), 
                                            new Vector4(-0.82948897f , 1.76266406f,  0.02362469f, 0.0f), 
                                            new Vector4(0.03584583f , -0.07617239f,  0.95688452f, 0.0f), 
                                            new Vector4(0.0f, 0.0f, 0.0f, 0.0f));
    private Vector3 colorVec;

    // Pre-multiply these two matrices to cache results at runtime
    private Matrix4x4 srgbToDisplayP3Mat;
    private Matrix4x4 displayP3ToSrgbMat;

    public ColorSpace()
    {
        colorVec = Vector3.zero;
        srgbToXyzMat = srgbToXyzMat.transpose;
        xyzToSrgbMat = xyzToSrgbMat.transpose;
        displayP3ToXyzMat = displayP3ToXyzMat.transpose;
        xyzToDisplayP3Mat = xyzToDisplayP3Mat.transpose;

        srgbToDisplayP3Mat =  xyzToDisplayP3Mat * srgbToXyzMat;
        displayP3ToSrgbMat =  xyzToSrgbMat * displayP3ToXyzMat;
    }
    
    // Input: variable inputColor should be in sRGB colorspace
    // Output: resulting Color will be in XYZ
    public Color srgbToXyz(Color inputColor)
    {
        colorVec.Set(inputColor.r, inputColor.g, inputColor.b);
        return Vec3ToColor((srgbToXyzMat.MultiplyVector(colorVec)));
    }

    public Color srgbToDisplayP3(Color inputColor)
    {
        colorVec.Set(inputColor.r, inputColor.g, inputColor.b);
        return Vec3ToColor(srgbToDisplayP3Mat.MultiplyVector(colorVec));
    }

    public Color displayP3ToSrgb(Color inputColor)
    {
        colorVec.Set(inputColor.r, inputColor.g, inputColor.b);
        return Vec3ToColor(displayP3ToSrgbMat.MultiplyVector(colorVec));
    }

    private Color Vec3ToColor(Vector3 inVec)
    {
        return new Color(inVec.x, inVec.y, inVec.z);
    }
    public void transformColor(Color inColor)
    {
        Vector4 inColorVec = inColor;
        // Debug.Log("initial Color is \t " + inColorVec.ToString("F4"));
        // Vector3 xyzColor = srgbToXyzMat.MultiplyVector(inColorVec);
        // Debug.Log("Color in XYZ \t " + xyzColor.ToString("F4"));
        // xyzColor = xyzToSrgbMat.MultiplyVector(xyzColor);
        // Debug.Log("Color back in sRGB \t " + xyzColor.ToString("F4"));
        Debug.Log("initial Color is \t " + inColorVec.ToString("F4"));
        Vector3 xyzColor = srgbToXyzMat.MultiplyVector(inColorVec);
        Debug.Log("Color in XYZ \t " + xyzColor.ToString("F4"));
        xyzColor = xyzToDisplayP3Mat.MultiplyVector(xyzColor);
        Debug.Log("Color in Display P3 \t " + xyzColor.ToString("F4"));
        xyzColor = displayP3ToXyzMat.MultiplyVector(xyzColor);
        Debug.Log("Color back in XYZ \t " + xyzColor.ToString("F4"));
        xyzColor = xyzToSrgbMat.MultiplyVector(xyzColor);
        Debug.Log("Color back in sRGB \t " + xyzColor.ToString("F4"));
        Debug.Log("-------------------------------------------------------");

        Color tmpColor;
        Debug.Log("initial Color is \t " + inColorVec.ToString("F4"));
        tmpColor = srgbToDisplayP3(inColor);
        Debug.Log("Color in Display P3 \t " + tmpColor.ToString("F4"));
        tmpColor = displayP3ToSrgb(tmpColor);
        Debug.Log("Color back in sRGB \t " + tmpColor.ToString("F4"));

    }


}
