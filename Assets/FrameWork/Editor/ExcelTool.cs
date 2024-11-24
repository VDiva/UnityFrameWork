using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using ExcelDataReader;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;

namespace FrameWork
{
    [ScriptedImporter(1, ".xlsx")]
    public class ExcelTool:ScriptedImporter
    {
        // public static string xlsxPath = _configData.XlsxPath;
        // public static string xlsxOutPath = _configData.XlsxOutPath;
        //
        // public static string xlsxOutResourcesPath = Config.XlsxOutResourcesPath;
        //
        // public static string xlsxOutScriptPath = Config.XlsxOutScriptPath;
        public static List<string> paths = new List<string>();

        public static ConfigData ConfigData;
        public override void OnImportAsset(AssetImportContext ctx)
        {
            ConfigData=Resources.Load<ConfigData>("ConfigData");
            MyLog.LogWarning("导入");
            if (!Directory.Exists(ConfigData.xlsxPath))
            {
                Directory.CreateDirectory(ConfigData.xlsxPath);
                return;
            }
            if (!ctx.assetPath.StartsWith(ConfigData.xlsxPath)) return;
            var path = ctx.assetPath;
            var fileName = Path.GetFileNameWithoutExtension(path);
            if (fileName.StartsWith("~$")) return;
            paths.Add(path); //添加到待转换列表里 等待资源加载完成后转换
        }
    }


    public class XlsxAssetPostprocessor : AssetPostprocessor
    {
        
        static List<List<object>> _rows = new List<List<object>>();
        static StringBuilder _stringBuilder = new StringBuilder();
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            
            if (ExcelTool.paths == null
                || ExcelTool.paths.Count == 0) return;
            
            if (EditorUtility.DisplayDialog("信息", "检测到有表格内容发生改变，是否重新转换", "是", "否"))
            {
                for (int i = 0; i < ExcelTool.paths.Count; i++)
                {
                    var dataSet=GetTabelData(ExcelTool.paths[i]);
                    var table = dataSet.Tables[0];
                    var name = "Xlsx_"+Path.GetFileNameWithoutExtension(ExcelTool.paths[i]);
                    _rows.Clear();
                    var colNum = table.Columns.Count;
                    var rowNum = table.Rows.Count;
                    if (colNum == 0 || rowNum == 0)
                        continue;
                    _stringBuilder.Clear();
                    _rows.Add(table.Rows[0].ItemArray.ToList());
                    _rows.Add(table.Rows[1].ItemArray.ToList());
                    _rows.Add(table.Rows[2].ItemArray.ToList());
                    SpawnKey(name,table.Rows);
                    SpawnClass(name,_rows);
                    SpawnQueryClass(name,table.Rows[1].ItemArray.ToList(),table.Rows[2].ItemArray.ToList());
                    for (int k = 0; k < rowNum; k++)
                    {
                        var items = table.Rows[k].ItemArray;
                        for (int j = 0; j < items.Length; j++)
                        {
                            if (j==items.Length-1)
                            {
                                _stringBuilder.Append(items[j].ToString()+"\n");
                            }
                            else
                            {
                                _stringBuilder.Append(items[j].ToString()+"|");
                            }
                        }
                    }

                    var outPath = Config.IsAb ? ExcelTool.ConfigData.xlsxOutPath : ExcelTool.ConfigData.xlsxOutResourcesPath;
                    
                    if (!Directory.Exists(outPath))
                    {
                        Directory.CreateDirectory(outPath);
                    }
                    using (StreamWriter sw=new StreamWriter(outPath+"\\"+name+".txt",false))
                    {
                        MyLog.Log("写入");
                        sw.Write(_stringBuilder);
                    }
                    
                    MyLog.Log(ExcelTool.paths[i]);
                }
                MyLog.Log("更新");
                if (Config.IsAb)
                {
                    ABConfig.AssetPackaged();
                    AssetBundle.UpdateAssetBundle("xlsx");
                }
                else
                {
                    ABConfig.AssetPackaged();
                }
                
            }
            ExcelTool.paths.Clear();
        }

        private static void SpawnKey(string fileName,DataRowCollection coll)
        {
            var xlsxName = fileName.Split('_')[1];
            var xlsxKeyName = "Xlsx_"+xlsxName+"_Key";
            var xlsxTypeName = "Xlsx_"+xlsxName+"_Type";
            HashSet<string> key = new HashSet<string>();

            var outPath = ExcelTool.ConfigData.xlsxOutScriptPath;
            
            if (!Directory.Exists(outPath))
            {
                Directory.CreateDirectory(outPath);
            }
            
            using (StreamWriter sw = new StreamWriter(outPath + "\\" + xlsxKeyName + ".cs", false))
            {
                sw.WriteLine("namespace Xlsx");
                sw.WriteLine("{");
                sw.WriteLine("\tpublic enum "+xlsxKeyName);
                sw.WriteLine("\t{");
                for (int i = 3; i < coll.Count; i++)
                {
                    if (!key.Contains(coll[i].ItemArray[0].ToString()))
                    {
                        sw.WriteLine($"\t\t{coll[i].ItemArray[0]},");
                        key.Add(coll[i].ItemArray[0].ToString());
                    }
                    
                }
                
                sw.WriteLine("\n");
                sw.WriteLine("\t}");
                
                sw.WriteLine("\tpublic enum "+xlsxTypeName);
                sw.WriteLine("\t{");
                var row = coll[2].ItemArray;
                for (int i = 0; i < row.Length; i++)
                {
                    sw.WriteLine($"\t\t{row[i]},");
                }
                
                sw.WriteLine("\t}");
                sw.WriteLine("}");
                
            }
        }

        private static StringBuilder _queryClassBuilder = new StringBuilder();
        public static void SpawnQueryClass(string fileName,List<object> row,List<object> row2)
        {
            _queryClassBuilder.Clear();
            var xlsxName = fileName.Split('_')[1];
            var xlsxQueryName = fileName+"_Query";

            var outPath = ExcelTool.ConfigData.xlsxOutScriptPath;
            
            if (!Directory.Exists(outPath))
            {
                Directory.CreateDirectory(outPath);
            }
            
            using (StreamWriter sw = new StreamWriter(outPath + "\\" + xlsxQueryName + ".cs", false))
            {
                // sw.WriteLine("using System.Collections.Generic;");
                // sw.WriteLine("using UnityEngine;");
                // sw.WriteLine("using FrameWork;");

                for (int i = 0; i < Config.XlsxSpawnUse.Length; i++)
                {
                    sw.WriteLine("using "+Config.XlsxSpawnUse[i]+";");
                }
                
                sw.WriteLine("namespace Xlsx");
                sw.WriteLine("{");
                sw.WriteLine("\tpublic static class "+xlsxQueryName);
                sw.WriteLine("\t{");
                sw.WriteLine($"\t\tpublic static List<{fileName}> data=new List<{fileName}>();");
                sw.WriteLine($"\t\tpublic static XlsxData<{row[0]},{fileName}> XlsxDataAsOneKey;");
                if (row.Count>=2)
                {
                    sw.WriteLine($"\t\tpublic static XlsxData<{row[0]},{row[1]},{fileName}> XlsxDataAsTowKey;");
                }
                sw.WriteLine("\t\tstatic "+xlsxQueryName+"()");
                sw.WriteLine("\t\t{");

                if (Config.IsAb)
                {
                    sw.WriteLine($"\t\t\tvar xlsx=ABLoad.LoadAsset<TextAsset>(Tool.GetMd5AsString(\"{fileName}\"), \"{fileName}\");");
                }
                else
                {
                    sw.WriteLine($"\t\t\tvar xlsx=Resources.Load<TextAsset>(\"xlsx/{fileName}\");");
                }
                
                sw.WriteLine("\t\t\tvar itemData=xlsx.text.Split('\\n');");
                sw.WriteLine("\t\t\tvar fileNames = itemData[2].Split('|');");
                sw.WriteLine("\t\t\tvar fileTypes = itemData[1].Split('|');");
                sw.WriteLine("\t\t\tfor (int i = 3; i < itemData.Length; i++)");
                sw.WriteLine("\t\t\t{");
                sw.WriteLine("\t\t\t\tif (string.IsNullOrEmpty(itemData[i]))continue;");
                sw.WriteLine("\t\t\t\tvar items = itemData[i].Split('|');");
                sw.WriteLine($"\t\t\t\tvar xlsxData = new {fileName}();");
                sw.WriteLine("\t\t\t\tvar type=xlsxData.GetType();");
                sw.WriteLine("\t\t\t\tfor (int j = 0; j < items.Length; j++)");
                sw.WriteLine("\t\t\t\t{");
                sw.WriteLine("\t\t\t\t\tif (string.IsNullOrEmpty(items[j]))continue;");
                sw.WriteLine("\t\t\t\t\ttype.GetField(fileNames[j]).SetValue(xlsxData,Tool.ConversionType(fileTypes[j],items[j]));");
                sw.WriteLine("\t\t\t\t}");
                sw.WriteLine("\t\t\t\tdata.Add(xlsxData);");
                sw.WriteLine("\t\t\t}");
                sw.WriteLine($"\t\t\tXlsxDataAsOneKey = new XlsxData<{row[0]},{fileName}>(\"{row2[0]}\", data);");
                // if (row.Count>=2)
                // {
                //     sw.WriteLine($"\t\t\tXlsxDataAsTowKey = new XlsxData<{row[0]},{row[1]},{fileName}>(\"{row2[0]}\",\"{row2[1]}\", data);");
                // }
                sw.WriteLine("\t\t}");
                sw.WriteLine("\t}");
                sw.WriteLine("}");
                
            }
        }
        

        private static StringBuilder _classBuilder = new StringBuilder();
        public static void SpawnClass(string fileName,List<List<object>> book)
        {
            _classBuilder.Clear();
            var outPath = ExcelTool.ConfigData.xlsxOutScriptPath;

            if (!Directory.Exists(outPath))
            {
                Directory.CreateDirectory(outPath);
            }
            
            using (StreamWriter sw=new StreamWriter(outPath+"\\"+fileName+".cs",false))
            {
                // sw.WriteLine("using System.Collections.Generic;");
                // sw.WriteLine("using UnityEngine;");
                
                for (int i = 0; i < Config.XlsxSpawnUse.Length; i++)
                {
                    sw.WriteLine("using "+Config.XlsxSpawnUse[i]+";");
                }
                
                sw.WriteLine("namespace Xlsx");
                sw.WriteLine("{");
                sw.WriteLine("\tpublic class "+fileName);
                sw.WriteLine("\t{");
                
                string gz = "";
                string fz = "";
                for (int i = 0; i < book[0].Count; i++)
                {
                    _classBuilder.AppendLine("\t\tpublic static " + fileName + " By" + book[2][i] + "GetXlsx(" +book[1][i] + " value){");
                    _classBuilder.AppendLine("\t\t\tforeach (var item in _xlsxs)");
                    _classBuilder.AppendLine("\t\t\t{");
                    _classBuilder.AppendLine("\t\t\t\tif (item."+book[2][i]+".Equals(value))");
                    _classBuilder.AppendLine("\t\t\t\t{");
                    _classBuilder.AppendLine("\t\t\t\t\treturn item;");
                    _classBuilder.AppendLine("\t\t\t\t}");
                    _classBuilder.AppendLine("\t\t\t}");
                    _classBuilder.AppendLine("\t\t\treturn null;");
                    _classBuilder.AppendLine("\t\t}");
                    _classBuilder.AppendLine("");
                    
                    
                    sw.WriteLine("\t\t/// <summary>");
                    sw.WriteLine("\t\t/// "+book[0][i]);
                    sw.WriteLine("\t\t/// </summary>");
                    sw.WriteLine("\t\tpublic "+book[1][i]+" "+book[2][i]+";");
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
        }
        
        public static DataSet GetTabelData(string path)
        {
            var reader = ExcelReaderFactory.CreateOpenXmlReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            var dataSet = reader.AsDataSet();
            return dataSet;
        }
    }
}