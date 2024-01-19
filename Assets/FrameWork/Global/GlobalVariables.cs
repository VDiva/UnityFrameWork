using FrameWork.Tool;
using UnityEditor;
using UnityEngine;

namespace FrameWork.Global
{
    public static class GlobalVariables
    {

        //public static Configure Configure=AssetDatabase.LoadAssetAtPath<Configure>(@"Assets/FrameWork/Configure.asset");
        public static Configure Configure = Resources.Load<Configure>("Configure");

    }
}