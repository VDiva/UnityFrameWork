
using UnityEngine;

namespace FrameWork
{
    public static class GlobalVariables
    {

        //public static Configure Configure=AssetDatabase.LoadAssetAtPath<Configure>(@"Assets/FrameWork/Configure.asset");
        public static Configure Configure = Resources.Load<Configure>("Configure");

    }
}