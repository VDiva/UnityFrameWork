using System;
using FrameWork.Tool;
using UnityEngine;

namespace cs
{
    public class Main : MonoBehaviour
    {
        private void Start()
        {
            
            var type=DllLoad.LoadType("HotUpdate.dll.bytes", "FrameWork.Cs");
            Debug.Log(type);
            type.GetMethod("Run")?.Invoke(null, null);
        }
    }
}