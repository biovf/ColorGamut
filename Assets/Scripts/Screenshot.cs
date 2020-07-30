using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class Screenshot : MonoBehaviour
{
    public Material fullScreenTextureMat;
    private RenderTexture screenGrab;
    private Texture2D textureToSave;

    void Start()
    {
        textureToSave = new Texture2D(Screen.width, Screen.height, TextureFormat.RGBAHalf, false);
        screenGrab = new RenderTexture(Screen.width, Screen.height, 0);
    }

    void Update()
    {
        if (Input.GetKeyUp(KeyCode.T)) 
        {
            saveScreenshot("ExRImage" + Time.realtimeSinceStartup + ".exr");
        }
    }

    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        Graphics.Blit(src, screenGrab, fullScreenTextureMat);
        Graphics.Blit(screenGrab, dest);
    }
    Texture2D toTexture2D(RenderTexture rTex)
    {
        Texture2D tex = new Texture2D(rTex.width, rTex.height, TextureFormat.RGBAHalf, false);
        RenderTexture.active = rTex;
        tex.ReadPixels(new Rect(0, 0, rTex.width, rTex.height), 0, 0);
        tex.Apply();
        return tex;
    }

    private void saveScreenshot(string fileName) 
    {
        Color[] pixels = toTexture2D(screenGrab).GetPixels();
        textureToSave.SetPixels(pixels);
        textureToSave.Apply();
        File.WriteAllBytes(fileName, textureToSave.EncodeToEXR());
    }

}
