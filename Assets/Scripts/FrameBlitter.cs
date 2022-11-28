using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class FrameBlitter : MonoBehaviour
{
    public RenderTexture sceneFrame;

    private Camera mainCamera;

    // Start is called before the first frame update
    void Start()
    {
        mainCamera = this.GetComponent<Camera>();
    }

    // Update is called once per frame
    void Update()
    {
        Graphics.Blit(mainCamera.activeTexture, sceneFrame);
        if (Input.GetKeyUp(KeyCode.N)) 
        {
            Texture2D res = GetRTPixels(sceneFrame);
            byte[] byteRes = res.EncodeToPNG();
            File.WriteAllBytes(Application.dataPath + "/../Res.png", byteRes);
        }
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
