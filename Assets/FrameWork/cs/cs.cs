using System;
using FrameWork.AssetBundles;
using FrameWork.Global;
using FrameWork.ObjectPool;
using UnityEngine;

namespace FrameWork.cs
{
    public class cs : MonoBehaviour
    {
        private void Start()
        {
            
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                AssetBundlesLoad.LoadAsset<GameObject>(GlobalVariables.ABName, "Cube", (go =>
                {
                    
                    Debug.Log(go.name);
                }));
            }
        }
    }
}