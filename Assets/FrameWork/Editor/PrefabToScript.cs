
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace FrameWork.Editor
{
    public class PrefabToScript: UnityEditor.Editor
    {

        [MenuItem("Assets/FrameWork/Prefab/CreateScrip")]
        public static void CreateScript()
        {
            var obj = Selection.activeGameObject;
            if (obj.TryGetComponent<RectTransform>(out RectTransform rectTransform))
            {
                Init("UiActor");
            }
            else
            {
                Init("Actor");
            }
            
        }
        
        private static void Init(string scriptName)
        {
            ABConfig.AssetPackaged();
            string path = Config.spawnScriptPath;
            string name = Selection.activeGameObject.name;
            Transform trans = Selection.activeGameObject.transform;
            int count = trans.childCount;

            
            
            if (!Directory.Exists(path+"/"+name))
            {
                Directory.CreateDirectory(path+"/"+name);
            }
            
            
            using (StreamWriter swMode=new StreamWriter(path+"/"+name+"//"+name+".H.cs",false))
            {
                using (StreamWriter swView=new StreamWriter(path+"/"+name+"//"+name+".Awake.cs",false))
                {

                    if (!File.Exists(path+"/"+name + "//" + name + ".cs"))
                    {
                        using (StreamWriter sw = new StreamWriter( path+"/"+name + "//" + name + ".cs", false))
                        {
                            sw.WriteLine("using UnityEngine;");
                            sw.WriteLine("using UnityEngine.Video;");
                            sw.WriteLine("using FrameWork;");
                            sw.WriteLine("using UnityEngine.UI;");
                            sw.WriteLine("namespace FrameWork\n{");
                            sw.WriteLine("\tpublic partial class "+name+" : "+scriptName);
                            sw.WriteLine("\t{");
                        
                            sw.WriteLine("\t}");
                            sw.WriteLine("}");
                        }
                    }
                    
                    

                    swMode.WriteLine("using UnityEngine;");
                    swMode.WriteLine("using FrameWork;");
                    swMode.WriteLine("using UnityEngine.UI;");
                    swMode.WriteLine("using UnityEngine.Video;");
                    swMode.WriteLine("namespace FrameWork\n{");
                    swMode.WriteLine("\tpublic partial class "+name+" : "+scriptName);
                    swMode.WriteLine("\t{");
                    
                    swView.WriteLine("using UnityEngine;");
                    swView.WriteLine("using FrameWork;");
                    swView.WriteLine("using UnityEngine.UI;");
                    swView.WriteLine("using UnityEngine.Video;");
                    swView.WriteLine("namespace FrameWork\n{");
                    swView.WriteLine("\tpublic partial class "+name+" : "+scriptName);
                    swView.WriteLine("\t{");
                    swView.WriteLine("\t\tpublic override void Awake()\n\t\t{");
                    swView.WriteLine("\t\t\tbase.Awake();");
                    
                    Writer(swMode,swView,"",trans,true);
                    
                    swMode.WriteLine("\t}");
                    
                    swMode.WriteLine("}");
                    
                    swView.WriteLine("\t\t}");
                    swView.WriteLine("\t}");
                    
                    swView.WriteLine("}");
                    
                }
            }

            if (!File.Exists(path + "/" + name + "//" + name + ".Attr.cs"))
            {
                using (StreamWriter swAttr = new StreamWriter(path + "/" + name + "//" + name + ".Attr.cs", false))
                {
                    AssetImporter ai=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(Selection.activeGameObject));
                    swAttr.WriteLine("using UnityEngine;");
                    swAttr.WriteLine("using FrameWork;");
                    swAttr.WriteLine("using UnityEngine.UI;");
                    swAttr.WriteLine("using UnityEngine.Video;");
                    swAttr.WriteLine("namespace FrameWork\n{");
                    if (scriptName=="UiActor")
                    {
                        swAttr.WriteLine("\t[UiMode(Mode.Normal)]");
                    }
                    swAttr.WriteLine("\t[ActorInfo(\""+ai.assetBundleName+"\",\""+name+"\")]");
                    swAttr.WriteLine("\tpublic partial class "+name+" : "+scriptName);
                    swAttr.WriteLine("\t{");
                    swAttr.WriteLine("\t}");
                    swAttr.WriteLine("}");
                }
            }


            if (!File.Exists(path + "/" + name + "//" + name + ".Extend.cs"))
            {
                using (StreamWriter swAttr = new StreamWriter(path + "/" + name + "//" + name + ".Extend.cs", false))
                {
                    AssetImporter ai=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(Selection.activeGameObject));
                    swAttr.WriteLine("using UnityEngine;");
                    swAttr.WriteLine("using FrameWork;");
                    swAttr.WriteLine("using UnityEngine.UI;");
                    swAttr.WriteLine("using UnityEngine.Video;");
                    swAttr.WriteLine("namespace FrameWork\n{");
                    swAttr.WriteLine("\tpublic partial class "+name+" : "+scriptName);
                    swAttr.WriteLine("\t{");
                    swAttr.WriteLine("\t\tpublic "+name+"(Transform trans): base(trans){}");
                    swAttr.WriteLine("\t\tpublic "+name+"(): base(){}");
                    swAttr.WriteLine("\t}");
                    swAttr.WriteLine("}");
                }
            }
            
            AssetImporter assetImporter=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(Selection.activeGameObject));
            AssetBundle.UpdateAssetBundle(assetImporter.assetBundleName);
            AssetDatabase.Refresh();

        }


        private static void Writer(StreamWriter swMode,StreamWriter swView,string path,Transform trans,bool isRoot=false)
        {
            foreach (var item in trans.GetComponents<Component>())
            {
                swMode.WriteLine("\t\tpublic "+item.GetType().Name+" "+(item.GetType().Name+item.gameObject.name).Replace(" ","")+";");

                if (isRoot)
                {
                    swView.WriteLine("\t\t\t"+(item.GetType().Name+item.gameObject.name).Replace(" ","")+" = "+"GetGameObject().transform.GetComponent<"+item.GetType().Name+">();");
                }
                else
                {
                    swView.WriteLine("\t\t\t"+(item.GetType().Name+item.gameObject.name).Replace(" ","")+" = "+"GetGameObject().transform.Find(\""+path+"\").GetComponent<"+item.GetType().Name+">();");
                }
            }

            for (int i = 0; i < trans.childCount; i++)
            {
                Writer(swMode,swView,path+trans.GetChild(i).gameObject.name+"/",trans.GetChild(i));
            }
        }
    }
}