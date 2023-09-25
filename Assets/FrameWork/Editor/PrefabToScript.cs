using System.IO;
using FrameWork.Global;
using UnityEditor;
using UnityEngine;

namespace FrameWork.Editor
{
    public class PrefabToScript: UnityEditor.Editor
    {
        
         
        [MenuItem("Assets/FrameWork/Prefab/CreateScript")]
        public static void Init()
        {


            
            
            
            string path = GlobalVariables.PrefabPath;
            string name = Selection.activeGameObject.name;
            Transform trans = Selection.activeGameObject.transform;
            int count = trans.childCount;

            
            
            AssetImporter ai=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(Selection.activeGameObject));
            ai.assetBundleName = GlobalVariables.ABName;
            ai.assetBundleVariant = GlobalVariables.ABNameEnd;
            
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
                        sw.WriteLine("public partial class "+name+" : MonoBehaviour");
                        sw.WriteLine("{");
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
                    swMode.WriteLine("public partial class "+name+" : MonoBehaviour");
                    swMode.WriteLine("{");
                    
                    swView.WriteLine("using UnityEngine;");
                    swView.WriteLine("public partial class "+name+" : MonoBehaviour");
                    swView.WriteLine("{");
                    swView.WriteLine("\tprivate void Awake()\n\t{");
                    
                    Writer(swMode,swView,"",trans,true);
                    
                    swMode.WriteLine("}");
                    swView.WriteLine("\t}");
                    swView.WriteLine("}");
                }
                
            }
            
            AssetBundle.CreatPCAssetBundleAsWindows();
            
            
            AssetDatabase.Refresh();
        }


        private static void Writer(StreamWriter swMode,StreamWriter swView,string path,Transform trans,bool isRoot=false)
        {
            foreach (var item in trans.GetComponents<Component>())
            {
                swMode.WriteLine("\tprivate "+item.GetType().Name+" "+item.GetType().Name+item.gameObject.name+";");

                if (isRoot)
                {
                    swView.WriteLine("\t\t"+item.GetType().Name+item.gameObject.name+" = "+"transform.GetComponent<"+item.GetType().Name+">();");
                }
                else
                {
                    swView.WriteLine("\t\t"+item.GetType().Name+item.gameObject.name+" = "+"transform.Find(\""+path+"\").GetComponent<"+item.GetType().Name+">();");
                }
            }

            for (int i = 0; i < trans.childCount; i++)
            {
                Writer(swMode,swView,path+trans.GetChild(i).gameObject.name+"/",trans.GetChild(i));
            }
        }
    }
}