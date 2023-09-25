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
            if (Application.platform == RuntimePlatform.Android) path = GlobalVariables.ABAsAndroid;
            if (Application.platform == RuntimePlatform.WindowsPlayer) path = GlobalVariables.ABAsWindows;
            if (Application.platform == RuntimePlatform.IPhonePlayer) path = GlobalVariables.ABAsIos;
            if (Application.platform == RuntimePlatform.WindowsEditor) path = GlobalVariables.ABAsWindows;

            DirectoryInfo directoryInfo = new DirectoryInfo(path);
            FileInfo[] fileInfos = directoryInfo.GetFiles();

            string info = "";
            foreach (var item in fileInfos)
            {
                if (item.Extension.Equals(".info"))
                {
                    info += item.Name + " " + info.Length + " " + Tool.Tool.GetMd5(item.FullName);
                    info += "|";
                }
            }
            info = info.Substring(0, info.Length - 1);

            if (!Directory.Exists(GlobalVariables.ABConfigPath))
            {
                Directory.CreateDirectory(GlobalVariables.ABConfigPath);
            }
            
            using (StreamWriter sw=new StreamWriter(GlobalVariables.ABConfigPath+"/ABConfig.txt",false))
            {
                sw.Write(info);
            }
            
           
            
            Debug.Log("AB包对比文件生成成功");
            AssetDatabase.Refresh();
        }
    }
}