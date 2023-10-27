using System.IO;
using FrameWork.Global;
using UnityEditor;
using UnityEngine;

namespace FrameWork.Editor
{
    public class ABConfig : UnityEditor.Editor 
    {
        [MenuItem("FrameWork/CreateConfig")]
        public static void CreateAbConfig()
        {
            string path = "";
            if (Application.platform == RuntimePlatform.Android) path = "AssetBundles/Android";
            if (Application.platform == RuntimePlatform.WindowsPlayer) path ="AssetBundles/StandaloneWindows";
            if (Application.platform == RuntimePlatform.IPhonePlayer) path = "AssetBundles/Ios";
            if (Application.platform == RuntimePlatform.WindowsEditor) path = "AssetBundles/StandaloneWindows";
            
            DirectoryInfo directoryInfo = new DirectoryInfo(path);
            FileInfo[] fileInfos = directoryInfo.GetFiles();
            
            string info = "";
            foreach (var item in fileInfos)
            {
                if (item.Extension.Equals(".info"))
                {
                    info += item.Name + " "+item.Length+" "+ Tool.Tool.GetMd5(item.FullName);
                    info += "|";
                }
            }
            info = info.Substring(0, info.Length - 1);
            
            if (!Directory.Exists("Assets/ABConfig"))
            {
                Directory.CreateDirectory("Assets/ABConfig/");
            }
            
            using (StreamWriter sw=new StreamWriter("Assets/ABConfig/ABConfig.txt",false))
            {
                sw.Write(info);
            }
            
            Debug.Log("AB包对比文件生成成功");
            AssetDatabase.Refresh();
        }
    }
}