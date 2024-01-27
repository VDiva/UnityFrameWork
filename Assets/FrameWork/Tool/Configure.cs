using UnityEngine;

namespace FrameWork
{
    [CreateAssetMenu(fileName = "Configure", menuName = "FrameWork/CreateConfigure", order = 0)]
    public class Configure : ScriptableObject
    {
        public string DownLoadUrl;
        public string ConfigName;
        //public string ConfigPath;
        public string AbAssetPath;
        // public string AbAndroidPath;
        // public string AbWindowsPath;
        // public string AbIosPath;
        public string AbEndName;
        public string AbMaterialName;
        public string AbUiPrefabName;
        public string AbModePrefabName;
        public string AbScreenName;
        public string SpawnScriptPath;
    }
}