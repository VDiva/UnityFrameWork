using UnityEngine;

namespace FrameWork.Global
{
    public static class GlobalVariables
    {
        public static string PrefabPath=Application.dataPath+"/FrameWork/PrefabScript/";
        public static string ABAsAndroid = Application.streamingAssetsPath;
        public static string ABAsWindows = Application.streamingAssetsPath;
        public static string ABAsIos = Application.dataPath+"/AssetBundles/IOS/";
        
        
        public static string UiABName = "Ui";
        public static string ScriptPrefabABName = "Prefab";
        public static string MaterialABName = "Material";
        public static string ScreenABName = "Screen";
        
        public static string ABNameEnd = "info";
        
        
        public static string ABConfigPath = Application.dataPath + "/FrameWork/ABConfig/";

        public static string UpdateDownLoadUrl = "http://127.0.0.1:9999/";

        public static string ABConfigName = "ABConfig.txt";
    }
}