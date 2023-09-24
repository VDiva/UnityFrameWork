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
            
            
            if (!Directory.Exists(GlobalVariables.PrefabPath+"/"+Selection.activeGameObject.name))
            {
                Directory.CreateDirectory(GlobalVariables.PrefabPath+"/"+Selection.activeGameObject.name);
            }

            Transform trans = Selection.activeGameObject.transform;
            int count = trans.childCount;
            
            
            
            using (StreamWriter swMode=new StreamWriter(GlobalVariables.PrefabPath+"/"+Selection.activeGameObject.name+"//"+Selection.activeGameObject.name+".Mode.cs",false))
            {
                using (StreamWriter swView=new StreamWriter(GlobalVariables.PrefabPath+"/"+Selection.activeGameObject.name+"//"+Selection.activeGameObject.name+".View.cs",false))
                {
                    using (StreamWriter sw = new StreamWriter( GlobalVariables.PrefabPath+"/"+Selection.activeGameObject.name + "//" + Selection.activeGameObject.name + ".cs", false))
                    {
                        sw.WriteLine("using UnityEngine;");
                        sw.WriteLine("\tpublic partial class "+Selection.activeGameObject.name+" : MonoBehaviour");
                        sw.WriteLine("\t{");
                        sw.WriteLine("\t}");
                    }
                    
                    
                    using (StreamWriter swSystem = new StreamWriter( GlobalVariables.PrefabPath+"/"+Selection.activeGameObject.name + "//" + Selection.activeGameObject.name + ".System.cs", false))
                    {
                        swSystem.WriteLine("using UnityEngine;");
                        swSystem.WriteLine("\tpublic partial class "+Selection.activeGameObject.name+" : MonoBehaviour");
                        swSystem.WriteLine("\t{");
                        swSystem.WriteLine("\t}");
                    }

                    swMode.WriteLine("using UnityEngine;");
                    swMode.WriteLine("\tpublic partial class "+Selection.activeGameObject.name+" : MonoBehaviour");
                    swMode.WriteLine("\t{");
                    
                    swView.WriteLine("using UnityEngine;");
                    swView.WriteLine("\tpublic partial class "+Selection.activeGameObject.name+" : MonoBehaviour");
                    swView.WriteLine("\t{");
                    swView.WriteLine("\t\tprivate void Awake()\n\t\t{");
                    
                    Writer(swMode,swView,"",trans,true);
                    
                    swMode.WriteLine("\t}");
                    swView.WriteLine("\t\t}");
                    swView.WriteLine("\t}");
                }
                
            }
            
            
            
            AssetDatabase.Refresh();
        }


        private static void Writer(StreamWriter swMode,StreamWriter swView,string path,Transform trans,bool isRoot=false)
        {
            foreach (var item in trans.GetComponents<Component>())
            {
                swMode.WriteLine("\t\tprivate "+item.GetType().Name+" "+item.GetType().Name+item.gameObject.name+";");

                if (isRoot)
                {
                    swView.WriteLine("\t\t\t"+item.GetType().Name+item.gameObject.name+" = "+"transform.GetComponent<"+item.GetType().Name+">();");
                }
                else
                {
                    swView.WriteLine("\t\t\t"+item.GetType().Name+item.gameObject.name+" = "+"transform.Find(\""+path+"\").GetComponent<"+item.GetType().Name+">();");
                }
            }

            for (int i = 0; i < trans.childCount; i++)
            {
                Writer(swMode,swView,path+trans.GetChild(i).gameObject.name+"/",trans.GetChild(i));
            }
        }
    }
}