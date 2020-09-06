using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
public class ColorGradingLDR : MonoBehaviour
{
    public Material colorGradingMat;
    public Material fullscreenMat;
    public Texture2D colorLUT;
    public Texture2D inputTexture;
    public GameObject secondPlane;

    private RenderTexture screenGrab;
    private Texture2D textureToSave;


    // Start is called before the first frame update
    void Start()
    {
        if(inputTexture == null || colorLUT == null)
            Debug.LogError("Error - a necessary texture is not set");

        screenGrab = new RenderTexture(inputTexture.width, inputTexture.height, 0);
        textureToSave = new Texture2D(inputTexture.width, inputTexture.height);
    }

    private void OnPreRender()
    {
        secondPlane.GetComponent<MeshRenderer>().material.SetTexture("_MainTex", inputTexture);
    }

    private void OnRenderImage(RenderTexture src, RenderTexture dest) 
    {
        Graphics.Blit(src, screenGrab, fullscreenMat);
        colorGradingMat.SetTexture("_LUT", colorLUT);
        Graphics.Blit(screenGrab, dest, colorGradingMat);

    }

    private void Update() {
        // if(Input.GetKeyUp(KeyCode.T))
        // {
        //     SaveToDisk("EXRTexture " + Time.realtimeSinceStartup);

        // }    
    }

    private void SaveToDisk(string fileName)
    {
        Color[] pixels = inputTexture.GetPixels();
        Texture2D textureToSave = new Texture2D(inputTexture.width, inputTexture.height);
        textureToSave.SetPixels(pixels);
        textureToSave.Apply();
        File.WriteAllBytes(@fileName, textureToSave.EncodeToEXR());
    }


}
