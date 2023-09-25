using UnityEngine;

namespace FrameWork.Global
{
    public static class GlobalVariables
    {
        public static string PrefabPath=Application.dataPath+"/FrameWork/PrefabScript";
        public static string ABAsAndroid = Application.streamingAssetsPath;
        public static string ABAsWindows = Application.streamingAssetsPath;
        public static string ABAsIos = Application.dataPath+"/AssetBundles/IOS";

        public static string ABName = "assets";
        public static string ABNameEnd = "info";

        public static string ABWindowsAndAnidroidPath = Application.streamingAssetsPath + "/" + ABName + "." + ABNameEnd;
        public static string ABIosPath = Application.dataPath+"/AssetBundles/IOS" + "/" + ABName + "." + ABNameEnd;

        public static string ABConfigPath = Application.dataPath + "/FrameWork/ABConfig";
    }
}