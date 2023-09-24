using System;
using FrameWork.Global;
using UnityEngine;

namespace FrameWork.cs
{
    public class cs : MonoBehaviour
    {
        private void Start()
        {
            var assetBundle=AssetBundle.LoadFromFile(GlobalVariables.ABAsWindows+"/cs.info");
            var go = assetBundle.LoadAsset<GameObject>("Cube");
            Debug.Log(go.name);
            assetBundle.Unload(true);
            Debug.Log(go.name);
        }
    }
}