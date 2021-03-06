﻿using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class LutGenerator 
{
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
  
  public static Color[] generateHdrTexLut(int sliceLength, float maxValue, bool useShaper = true)
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
  
  
  
}
