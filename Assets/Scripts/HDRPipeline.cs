using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class HDRPipeline : MonoBehaviour
{
    private ColorGamut1 colorGamut;
    
    void Start()
    {
        initialiseColorGamut();
    }
    
    // Update is called once per frame
    void Update()
    {
        colorGamut.Update();
    }

    private void initialiseColorGamut()
    {
        colorGamut = new ColorGamut1();
        colorGamut.Start(this);
    }

    public ColorGamut1 getColorGamut()
    {
        if(colorGamut == null)
            initialiseColorGamut();
        return colorGamut;
    }
}
