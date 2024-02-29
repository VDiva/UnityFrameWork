
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace FrameWork.Editor
{
    public class PrefabToScript: UnityEditor.Editor
    {

        [MenuItem("Assets/FrameWork/Prefab/CreateScripToUiActor")]
        public static void CreateScriptUiActor()
        {
            Init("UiActor");
        }
        
        [MenuItem("Assets/FrameWork/Prefab/CreateScripToNettUiActor")]
        public static void CreateScriptNetUiActor()
        {
            Init("NetUiActor");
        }
        
        
        [MenuItem("Assets/FrameWork/Prefab/CreateScriptActor")]
        public static void CreateScriptActor()
        {
            Init("Actor");
        }
        
        [MenuItem("Assets/FrameWork/Prefab/CreateScriptToNetActor")]
        public static void CreateScriptNetActor()
        {
            Init("NetActor");
        }

        
        private static void Init(string scriptName)
        {
            string path = GlobalVariables.Configure.SpawnPrefabScriptPath;
            string name = Selection.activeGameObject.name;
            Transform trans = Selection.activeGameObject.transform;
            int count = trans.childCount;

            //
            //
            // AssetImporter ai=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(Selection.activeGameObject));
            // ai.assetBundleName = GlobalVariables.Configure.AbModePrefabName;
            // ai.assetBundleVariant = GlobalVariables.Configure.AbEndName;
            
            
            
            
            if (!Directory.Exists(path+"/"+name))
            {
                Directory.CreateDirectory(path+"/"+name);
            }
            
            
            using (StreamWriter swMode=new StreamWriter(path+"/"+name+"//"+name+".H.cs",false))
            {
                using (StreamWriter swView=new StreamWriter(path+"/"+name+"//"+name+".Awake.cs",false))
                {
                    using (StreamWriter sw = new StreamWriter( path+"/"+name + "//" + name + ".cs", false))
                    {
                        sw.WriteLine("using UnityEngine;");
                        sw.WriteLine("using FrameWork;");
                        sw.WriteLine("using UnityEngine.UI;");
                        sw.WriteLine("namespace FrameWork\n{");
                        sw.WriteLine("\tpublic partial class "+name+" : "+scriptName);
                        sw.WriteLine("\t{");
                        sw.WriteLine("\t}");
                        sw.WriteLine("}");
                    }
                    
                    
                    // using (StreamWriter swSystem = new StreamWriter( path+"/"+name + "//" + name + ".System.cs", false))
                    // {
                    //     swSystem.WriteLine("using UnityEngine;");
                    //     swSystem.WriteLine("using UnityEngine;");
                    //     swSystem.WriteLine("public partial class "+name+" : "+scriptName);
                    //     swSystem.WriteLine("{");
                    //     swSystem.WriteLine("}");
                    // }

                    swMode.WriteLine("using UnityEngine;");
                    swMode.WriteLine("using FrameWork;");
                    swMode.WriteLine("using UnityEngine.UI;");
                    swMode.WriteLine("namespace FrameWork\n{");
                    swMode.WriteLine("\tpublic partial class "+name+" : "+scriptName);
                    swMode.WriteLine("\t{");
                    
                    swView.WriteLine("using UnityEngine;");
                    swView.WriteLine("using FrameWork;");
                    swView.WriteLine("using UnityEngine.UI;");
                    swView.WriteLine("namespace FrameWork\n{");
                    swView.WriteLine("\tpublic partial class "+name+" : "+scriptName);
                    swView.WriteLine("\t{");
                    swView.WriteLine("\t\tprotected virtual void Awake()\n\t\t{");
                    
                    Writer(swMode,swView,"",trans,true);
                    
                    swMode.WriteLine("\t}");
                    
                    swMode.WriteLine("}");
                    
                    swView.WriteLine("\t\t}");
                    swView.WriteLine("\t}");
                    
                    swView.WriteLine("}");
                    
                }
            }
            
            ABConfig.AssetPackaged();
            AssetBundle.CreatPCAssetBundleAsWindows();
        
            AssetDatabase.Refresh();
            
            //AssetDatabase.ImportAsset(GlobalVariables.Configure.SpawnPrefabScriptPath);
            
            // if (Selection.activeGameObject.GetComponent("FrameWork."+Selection.activeGameObject.name)==null)
            // {
            //     string typeName = "FrameWork." + Selection.activeGameObject.name;
            //     System.Type type = typeof(AssemblyType).Assembly.GetType(typeName);
            //     Selection.activeGameObject.AddComponent(type);
            // }
        }


        private static void Writer(StreamWriter swMode,StreamWriter swView,string path,Transform trans,bool isRoot=false)
        {
            foreach (var item in trans.GetComponents<Component>())
            {
                swMode.WriteLine("\t\tprotected "+item.GetType().Name+" "+(item.GetType().Name+item.gameObject.name).Replace(" ","")+";");

                if (isRoot)
                {
                    swView.WriteLine("\t\t\t"+(item.GetType().Name+item.gameObject.name).Replace(" ","")+" = "+"transform.GetComponent<"+item.GetType().Name+">();");
                }
                else
                {
                    swView.WriteLine("\t\t\t"+(item.GetType().Name+item.gameObject.name).Replace(" ","")+" = "+"transform.Find(\""+path+"\").GetComponent<"+item.GetType().Name+">();");
                }
            }

            for (int i = 0; i < trans.childCount; i++)
            {
                Writer(swMode,swView,path+trans.GetChild(i).gameObject.name+"/",trans.GetChild(i));
            }
        }
    }
}