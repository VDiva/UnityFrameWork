using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using FlexFramework.Excel;


using Object = UnityEngine.Object;

namespace FlexReader.Editor
{
    public class SpawnScriptWindows : EditorWindow
    {
        private static SpawnScriptWindows _instance;
        
        
        [SerializeField]
        private Object xlsxFile;

        private string _xlsxPath;
        
        // [SerializeField]
        // private Object outputFolder;

        private string _outputPath="Assets/FrameWork/Scripts/OldVXlsx";
        
        [MenuItem("FrameWork/SpawnXlsxScriptWindows",false,-1)]
        public static void Init()
        {
            _instance=(SpawnScriptWindows)EditorWindow.GetWindow(typeof(SpawnScriptWindows));
            _instance.position = new Rect(400, 200, 400, 300);
            _instance.titleContent = new GUIContent("通过xlsx生成类");
        }


        private void OnEnable()
        {
            if (PlayerPrefs.GetString("XlsxPath").IsNormalized())
            {
                _xlsxPath = PlayerPrefs.GetString("XlsxPath");
            }

            if (PlayerPrefs.GetString("OutPath").IsNormalized())
            {
                
            }
        }

        private void OnGUI()
        {
            
            EditorGUILayout.LabelField("xlsx表格第一行注释第二行类型第三行变量名其他都是变量值");
            
            EditorGUILayout.LabelField("");
            
            EditorGUILayout.LabelField("xlsx路径");
            _xlsxPath = EditorGUILayout.TextField(_xlsxPath);
            PlayerPrefs.SetString("XlsxPath",_xlsxPath);
            
            
            // EditorGUILayout.LabelField("输出目录");
            // outputFolder = EditorGUILayout.ObjectField(outputFolder, typeof(object),false);
            // if (outputFolder!=null)
            // {
            //     _outputPath = Application.dataPath.Replace("Assets", "") + AssetDatabase.GetAssetPath(outputFolder);
            // }
            
            
            if (GUILayout.Button("生成类代码"))
            {
                if (_xlsxPath!=null&& _outputPath!=null)
                {
                    DirectoryInfo dir = new DirectoryInfo(_xlsxPath);
                    FileInfo[] inf = dir.GetFiles();
                    
                    List<string> paths = new List<string>();
                    string fileName = "";
                    foreach (var item in inf)
                    {
                        if (item.Extension.Equals(".xlsx"))
                        {
                            string lastString = _xlsxPath.Last().ToString();
                            if (lastString.Equals("/")| lastString.Equals("\\"))
                            {
                                paths.Add(_xlsxPath+item.Name);
                            }
                            else
                            {
                                _xlsxPath = _xlsxPath.Replace("\\", "/");
                                paths.Add(_xlsxPath+"/"+item.Name);
                            }
                        }
                    }

                    foreach (var item in paths)
                    {
                        
                        var book = new WorkBook(item)[0].Rows;
                        fileName = "Xlsx_"+item.Split('/').Last().Split('.')[0];
                        var fileQuName = fileName + "_Query";


                        
                        StringBuilder stringBuilder = new StringBuilder();
                        
                        Debug.Log(_outputPath);
                        using (StreamWriter sw=new StreamWriter(_outputPath+"\\"+fileName+".cs",false))
                        {
                            
                            sw.WriteLine("using System.Collections.Generic;");
                            sw.WriteLine("using UnityEngine;");
                            sw.WriteLine("namespace Xlsx");
                            sw.WriteLine("{");
                            sw.WriteLine("\tpublic class "+fileName);
                            sw.WriteLine("\t{");
                            
                            string gz = "";
                            string fz = "";
                            for (int i = 0; i < book[0].Count; i++)
                            {


                                stringBuilder.AppendLine("\t\tpublic static " + fileName + " By" + book[2][i] + "GetXlsx(" +book[1][i] + " value){");
                                stringBuilder.AppendLine("\t\t\tforeach (var item in _xlsxs)");
                                stringBuilder.AppendLine("\t\t\t{");
                                stringBuilder.AppendLine("\t\t\t\tif (item."+book[2][i]+".Equals(value))");
                                stringBuilder.AppendLine("\t\t\t\t{");
                                stringBuilder.AppendLine("\t\t\t\t\treturn item;");
                                stringBuilder.AppendLine("\t\t\t\t}");
                                stringBuilder.AppendLine("\t\t\t}");
                                stringBuilder.AppendLine("\t\t\treturn null;");
                                stringBuilder.AppendLine("\t\t}");
                                stringBuilder.AppendLine("");
                                
                                
                                sw.WriteLine("\t\t/// <summary>");
                                sw.WriteLine("\t\t/// "+book[0][i]);
                                sw.WriteLine("\t\t/// </summary>");
                                sw.WriteLine("\t\tpublic "+CheckTypeKey(book[1][i])+" "+book[2][i]+";");
                                sw.WriteLine("");
                                
                                fz += "\t\t\tthis." + book[2][i] + "=" + book[2][i]+";\n";
                                
                                if (i!=book[0].Count-1)
                                {
                                    gz += book[1][i] + " " + book[2][i]+",";
                                }
                                else
                                {
                                    gz += book[1][i] + " " + book[2][i];
                                }
                            }
                            sw.Write("\t\tpublic "+fileName+"(");
                            sw.Write(gz);
                            sw.Write(")");
                            sw.WriteLine("{");
                            sw.WriteLine(fz);
                            sw.WriteLine("\t\t}");
                            // sw.WriteLine("public void css(int value){Debug.Log(value);}");
                            
                            sw.Write("\t\tpublic "+fileName+"(){}");
                            
                            sw.WriteLine("\t}");
                            sw.WriteLine("}");
                        }
                        
                        
                        using (StreamWriter sw=new StreamWriter(_outputPath+"\\"+fileQuName+".cs",false))
                        {
                            sw.WriteLine("using System.Collections.Generic;");
                            sw.WriteLine("using UnityEngine;");
                            sw.WriteLine("using Xlsx;");
                            sw.WriteLine("namespace Xlsx");
                            sw.WriteLine("{");
                            
                            sw.WriteLine("\tpublic static class "+fileQuName);
                            sw.WriteLine("\t{");
                            sw.WriteLine("\t\tprivate static List<"+fileName+"> _xlsxs = new List<"+fileName+">()");
                            sw.WriteLine("\t\t{");
                            
                            for (int i = 3; i < book.Count; i++)
                            {
                                //if (book[i][0].ToString().Equals(""))continue;
                                sw.Write("\t\t\tnew "+fileName+"(");
                                for (int j = 0; j < book[i].Count; j++)
                                {
                                    sw.Write(CheckTypeValue(book[1][j].ToString(),book[i][j].ToString()));
                                    if (j!=book[i].Count-1)
                                    {
                                        sw.Write(",");
                                    }
                                    
                                }
                                
                                if (i!=book.Count-1)
                                {
                                    sw.Write("),");
                                }
                                else
                                {
                                    sw.Write(")");
                                }
                                sw.WriteLine("");
                            }
                            
                            
                            sw.WriteLine("\n\t\t};");
                            
                            
                            sw.WriteLine(stringBuilder);
                            
                            sw.WriteLine("\t\tpublic static List<"+fileName+"> GetAllXlsx(){return _xlsxs;}");
                            
                            sw.WriteLine();

                            sw.WriteLine("\t}");
                            
                            sw.WriteLine("}");
                        }
                    }
                    AssetDatabase.Refresh();

                }
            }
            
            
        }

        private string CheckTypeKey(string type)
        {
            
            switch (type)
            {
                
                default:
                    return type;
                    break;
            }
        }
        
        private string CheckTypeValue(string type,string value) 
        {

            if (value=="null")
            {
                return "null";
            }
            
            if (type.IndexOf("Dictionary")!=-1)
            {
                
                return $"new {type}(){{{value}}}";
            }
            
            if (type.IndexOf("[]")!=-1)
            {
                string zhi = "";
                zhi += $"new {type}{{";
                string[] zhis = value.Split(',');
                switch (type)
                {
                    case "float[]":
                        
                        for (int i = 0; i < zhis.Length; i++)
                        {
                            if (i<zhis.Length)
                            {
                                zhi += zhis[i] + "f,";
                            }
                            else
                            {
                                zhi += zhis[i] + "f";
                            }
                        }
                        
                        break;
                    case "string[]":
                        for (int i = 0; i < zhis.Length; i++)
                        {
                            if (i<zhis.Length)
                            {
                                zhi += $"\"{zhis[i]}\",";
                            }
                            else
                            {
                                zhi += $"\"{zhis[i]}\"";
                            }
                        }
                        break;
                    case "int[]":
                        zhi += value;
                        break;
                }
                zhi += "}";
                return zhi;
            }

            
            switch (type)
            {
                case "int":
                    return value==""?"0": value;
                    break;
                case "float":
                    return value==""?"0":$"{value}f";
                    break;
                case "string":
                    return $"\"{value}\"";
                    break;
                case "Vector2":
                    return $"new Vector2({value})";
                    break;
                case "Vector3":
                    return $"new Vector3({value})";
                    break;
            }
            return value;
        }

        private void OnDestroy()
        {
            _instance = null;
        }
    }
}