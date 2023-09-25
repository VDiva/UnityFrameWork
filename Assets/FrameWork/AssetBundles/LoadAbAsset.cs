using System;
using FrameWork.Global;
using UnityEngine;
using Object = System.Object;

namespace FrameWork.AssetBundles
{
    public class LoadAbAsset
    {
        public static T LoadAsset<T>(string name) where T : UnityEngine.Object
        {
            return AssetBundlesLoad.LoadAsset<T>(GlobalVariables.ABName,name);
        }
    }
}