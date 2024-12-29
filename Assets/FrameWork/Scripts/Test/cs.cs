using System.Collections;
using System.Collections.Generic;
using FrameWork;
using UnityEngine;

public class cs : MonoBehaviour
{
    private void Start()
    {
        Log();
    }

    [Log]
    public void Log()
    {
        Debug.Log("Hello World!");
        
    }
}
