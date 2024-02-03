using System.IO;
using UnityEditor;

namespace FrameWork
{
    public class UiToScript: UnityEditor.Editor
    {
        [MenuItem("Assets/FrameWork/Ui/CreateScript")]
        public static void Init()
        {
            var go=Selection.activeGameObject.gameObject;
            string name = go.name;
            string path = GlobalVariables.Configure.SpawnUiScriptPath+"/";

            if (!Directory.Exists(path+"/"+name))
            {
                Directory.CreateDirectory(path + "/" + name);
            }
            
            using (StreamWriter swMode = new StreamWriter(path + "/" + name + "//" + name + ".Init.cs", false))
            {
                swMode.WriteLine("using UnityEngine;");
                swMode.WriteLine("using FrameWork;");
                
                swMode.WriteLine("\tpublic partial class "+name+": UiBase");
                swMode.WriteLine("\t{");
                swMode.WriteLine("\t}");
            }
            
            AssetBundle.CreatPCAssetBundleAsWindows();
            AssetDatabase.Refresh();
            

        }
    }
}