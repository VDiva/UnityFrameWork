using UnityEngine;

namespace FrameWork.Global
{
    public static class GlobalVariables
    {
        public static string PrefabPath=Application.dataPath+"/FrameWork/PrefabScript";
        public static string ABAsAndroid = Application.streamingAssetsPath;
        public static string ABAsWindows = Application.streamingAssetsPath;
        public static string ABAsIos = Application.dataPath+"/AssetBundles/IOS";
    }
}