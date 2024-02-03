
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FrameWork.Editor
{
    public class PrefabToScript: UnityEditor.Editor
    {
        
         
        [MenuItem("Assets/FrameWork/Prefab/CreateScript")]
        public static void Init()
        {


            
            
            
            string path = GlobalVariables.Configure.SpawnPrefabScriptPath;
            string name = Selection.activeGameObject.name;
            Transform trans = Selection.activeGameObject.transform;
            int count = trans.childCount;

            
            
            AssetImporter ai=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(Selection.activeGameObject));
            ai.assetBundleName = GlobalVariables.Configure.AbModePrefabName;
            ai.assetBundleVariant = GlobalVariables.Configure.AbEndName;
            
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
                        sw.WriteLine("namespace FrameWork\n{");
                        sw.WriteLine("\tpublic partial class "+name+" : MonoBehaviour");
                        sw.WriteLine("\t{");
                        sw.WriteLine("\t}");
                        sw.WriteLine("}");
                    }
                    
                    
                    // using (StreamWriter swSystem = new StreamWriter( path+"/"+name + "//" + name + ".System.cs", false))
                    // {
                    //     swSystem.WriteLine("using UnityEngine;");
                    //     swSystem.WriteLine("public partial class "+name+" : MonoBehaviour");
                    //     swSystem.WriteLine("{");
                    //     swSystem.WriteLine("}");
                    // }

                    swMode.WriteLine("using UnityEngine;");
                    swMode.WriteLine("using FrameWork;");
                    swMode.WriteLine("namespace FrameWork\n{");
                    swMode.WriteLine("\tpublic partial class "+name+" : MonoBehaviour");
                    swMode.WriteLine("\t{");
                    
                    swView.WriteLine("using UnityEngine;");
                    swView.WriteLine("using FrameWork;");
                    swView.WriteLine("namespace FrameWork\n{");
                    swView.WriteLine("\tpublic partial class "+name+" : MonoBehaviour");
                    swView.WriteLine("\t{");
                    swView.WriteLine("\t\tprivate void Awake()\n\t\t{");
                    
                    Writer(swMode,swView,"",trans,true);
                    
                    swMode.WriteLine("\t}");
                    
                    swMode.WriteLine("}");
                    
                    swView.WriteLine("\t\t}");
                    swView.WriteLine("\t}");
                    
                    swView.WriteLine("}");
                    
                }
                
            }
            
            AssetBundle.CreatPCAssetBundleAsWindows();
        
            AssetDatabase.Refresh();
            if (Selection.activeGameObject.GetComponent("FrameWork."+Selection.activeGameObject.name)==null)
            {
                System.Type type = typeof(AssemblyType).Assembly.GetType("FrameWork." + Selection.activeGameObject.name);
                Selection.activeGameObject.AddComponent(type);
            }
        }


        private static void Writer(StreamWriter swMode,StreamWriter swView,string path,Transform trans,bool isRoot=false)
        {
            foreach (var item in trans.GetComponents<Component>())
            {
                swMode.WriteLine("\t\tprivate "+item.GetType().Name+" "+(item.GetType().Name+item.gameObject.name).Replace(" ","")+";");

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