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
            if (Application.platform == RuntimePlatform.Android) path = GlobalVariables.Configure.AbAndroidPath;
            if (Application.platform == RuntimePlatform.WindowsPlayer) path =GlobalVariables.Configure.AbWindowsPath;
            if (Application.platform == RuntimePlatform.IPhonePlayer) path = GlobalVariables.Configure.AbIosPath;
            if (Application.platform == RuntimePlatform.WindowsEditor) path = GlobalVariables.Configure.AbWindowsPath;
            
            DirectoryInfo directoryInfo = new DirectoryInfo(path);
            FileInfo[] fileInfos = directoryInfo.GetFiles();
            
            string info = "";
            foreach (var item in fileInfos)
            {
                if (item.Extension.Equals("."+GlobalVariables.Configure.AbEndName))
                {
                    info += item.Name + " "+item.Length+" "+ Tool.Tool.GetMd5(item.FullName);
                    info += "|";
                }
            }
            info = info.Substring(0, info.Length - 1);
            
            if (!Directory.Exists(GlobalVariables.Configure.ConfigPath))
            {
                Directory.CreateDirectory(GlobalVariables.Configure.ConfigPath);
            }
            
            using (StreamWriter sw=new StreamWriter(GlobalVariables.Configure.ConfigPath+"/"+GlobalVariables.Configure.ConfigName,false))
            {
                sw.Write(info);
            }
            
            
            
            Debug.Log("AB包对比文件生成成功");
            AssetDatabase.Refresh();
        }
    }
}