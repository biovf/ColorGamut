using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class LutGenerator 
{


  public static Color[] generateIdentityCubeLUT(int cubeSideLenght)
  {
    Color[] identityLUT = new Color[cubeSideLenght * cubeSideLenght * cubeSideLenght];

    for (int bChannel = 0; bChannel < cubeSideLenght; bChannel++)
    {
      for (int gChannel = 0; gChannel < cubeSideLenght; gChannel++)
      {
        for (int rChannel = 0; rChannel < cubeSideLenght; rChannel++)
        {
          int index = rChannel + (cubeSideLenght * gChannel) + (cubeSideLenght * cubeSideLenght * bChannel);
          identityLUT[index] =
            new Color((float)rChannel / (float)(cubeSideLenght - 1),
                      (float)gChannel / (float)(cubeSideLenght - 1),
                      (float)bChannel / (float)(cubeSideLenght - 1));
        }
      }
    }

    return identityLUT;
  }

  public static Color[] generateSdrTexLut(int sliceLength)
  {
    float redCounter = 0.0f;
    float greenCounter = 0.0f;
    float blueCounter = 0.0f;
    float pixelColorStride = 1.0f / (float)(sliceLength - 1);

    int textureWidth = sliceLength * sliceLength;
    int lutArrayLen = sliceLength * sliceLength * sliceLength;
    
    Color[] lutTexture = new Color[lutArrayLen];
    lutTexture[0] = Color.black;
    
    for (int i = 1; i < lutArrayLen; i++)
    {
      redCounter   = (i % sliceLength)  == 0 ?  0.0f : redCounter + pixelColorStride;
      greenCounter = (i % textureWidth) == 0 ? greenCounter + pixelColorStride : greenCounter;
      blueCounter  = (i % textureWidth) == 0 ? 0.0f : ((i % sliceLength) == 0 ?  
                                                        blueCounter + pixelColorStride : blueCounter);
      
      lutTexture[i] = new Color(redCounter, greenCounter, blueCounter);
    }

    return lutTexture;
  }
  
  public static Color[] generateHdrTexLutPQShape(int sliceLength, float maxValue, bool useShaper = true)
  {
    PQShaper pqShaper = new PQShaper();
    float redCounter = 0.0f;
    float greenCounter = 0.0f;
    float blueCounter = 0.0f;
    float pixelColorStride = maxValue / (float)(sliceLength - 1);
    int textureWidth = sliceLength * sliceLength;
    int lutArrayLen = sliceLength * sliceLength * sliceLength;
    
    Color[] lutTexture = new Color[lutArrayLen];
    lutTexture[0] = Color.black;
    
    for (int i = 1; i < lutArrayLen; i++)
    {
      redCounter   = (i % sliceLength)  == 0 ?  0.0f : redCounter + pixelColorStride;
      greenCounter = (i % textureWidth) == 0 ? greenCounter + pixelColorStride : greenCounter;
      blueCounter  = (i % textureWidth) == 0 ? 0.0f : ((i % sliceLength) == 0 ?  
                                               blueCounter + pixelColorStride : blueCounter);
      
      if (useShaper == true)
      {
        Vector3 shapedColor = pqShaper.LinearToPQ(new Vector3(redCounter, greenCounter, blueCounter), maxValue);
        lutTexture[i] = new Color(shapedColor.x, shapedColor.y, shapedColor.z);
      }
      else
      {
        lutTexture[i] = new Color(redCounter, greenCounter, blueCounter);
      }
    }

    return lutTexture;
  }
  // Uses Log2 shaper
  public static Color[] generateHdrTexLut(int sliceLength, float maxValue, bool useShaper,
    Vector2 midGrey, float minExposure, float maxExposure)
  {
    float redCounter = 0.0f;
    float greenCounter = 0.0f;
    float blueCounter = 0.0f;
    float pixelColorStride = maxValue / (float)(sliceLength - 1);
    int textureWidth = sliceLength * sliceLength;
    int lutArrayLen = sliceLength * sliceLength * sliceLength;
    
    Color[] lutTexture = new Color[lutArrayLen];
    lutTexture[0] = Color.black;
    
    for (int i = 1; i < lutArrayLen; i++)
    {
      redCounter   = (i % sliceLength)  == 0 ?  0.0f : redCounter + pixelColorStride;
      greenCounter = (i % textureWidth) == 0 ? greenCounter + pixelColorStride : greenCounter;
      blueCounter  = (i % textureWidth) == 0 ? 0.0f : ((i % sliceLength) == 0 ?  
        blueCounter + pixelColorStride : blueCounter);
      
      if (useShaper == true)
      {
        lutTexture[i] = new Color(Shaper.calculateLinearToLog2(redCounter, midGrey.x, minExposure, maxExposure),
                                  Shaper.calculateLinearToLog2(greenCounter, midGrey.x, minExposure, maxExposure),
                                  Shaper.calculateLinearToLog2(blueCounter, midGrey.x, minExposure, maxExposure));
      }
      else
      {
        lutTexture[i] = new Color(redCounter, greenCounter, blueCounter);
      }
    }

    return lutTexture;
  }
  
  
}
