using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PQShaper
{
    
    // SMPTE ST.2084 (PQ) transfer functions
    // 1.0 = 100nits, 100.0 = 10knits
    const float DEFAULT_MAX_PQ =  100.0f;

    struct ParamsPQ
    {
        public float N, M;
        public float C1, C2, C3;
    };

    private ParamsPQ PQ = new ParamsPQ
    {
        N = 2610.0f / 4096.0f / 4.0f,   // N
        M = 2523.0f / 4096.0f * 128.0f, // M
        C1 = 3424.0f / 4096.0f,         // C1
        C2 = 2413.0f / 4096.0f * 32.0f,  // C2
        C3 = 2392.0f / 4096.0f * 32.0f,  // C3
    };

    private Vector3 vecColor;
    private Vector3 denom;

    public PQShaper()
    {
        vecColor = Vector3.zero;
        denom = Vector3.zero;
    }

    public Vector3 LinearToPQ(Vector3 inputColor, float maxPQValue = DEFAULT_MAX_PQ)
    {
        vecColor.Set(Mathf.Pow(inputColor.x / maxPQValue, PQ.N), 
                     Mathf.Pow(inputColor.y / maxPQValue, PQ.N),
                     Mathf.Pow(inputColor.z / maxPQValue, PQ.N));
        vecColor.Set((PQ.C1 + PQ.C2 * vecColor.x) / (1.0f + PQ.C3 * vecColor.x),
                     (PQ.C1 + PQ.C2 * vecColor.y) / (1.0f + PQ.C3 * vecColor.y),
                     (PQ.C1 + PQ.C2 * vecColor.z) / (1.0f + PQ.C3 * vecColor.z));
        
        return new Vector3( Mathf.Pow(vecColor.x, PQ.M), 
                            Mathf.Pow(vecColor.y, PQ.M), 
                            Mathf.Pow(vecColor.z, PQ.M));
    }

    public Vector3 PQToLinear(Vector3 inputColor, float maxPQValue = DEFAULT_MAX_PQ)
    {
        vecColor.Set(Mathf.Pow(inputColor.x, 1.0f/PQ.M), 
                     Mathf.Pow(inputColor.y, 1.0f/PQ.M), 
                     Mathf.Pow(inputColor.z, 1.0f/PQ.M));
        Vector3 nd = Vector3.Max(new Vector3(vecColor.x - PQ.C1,vecColor.y - PQ.C1,vecColor.z - PQ.C1), 
                                 Vector3.zero);
        
        denom.Set(PQ.C2 - (PQ.C3 * vecColor.x),
                  PQ.C2 - (PQ.C3 * vecColor.y), 
                  PQ.C2 - (PQ.C3 * vecColor.z));
        
        nd.Set(nd.x/denom.x, nd.y/denom.y, nd.z/denom.z);

        return new Vector3( Mathf.Pow(nd.x, 1.0f/(PQ.N)), 
                            Mathf.Pow(nd.y, 1.0f/(PQ.N)), 
                            Mathf.Pow(nd.z, 1.0f/(PQ.N))) * maxPQValue;
    }
}
