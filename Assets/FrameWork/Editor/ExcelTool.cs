using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using ExcelDataReader;
using UnityEditor;

using UnityEngine;

namespace FrameWork
{

    public static class ExcelToolData
    {
        //配置表文件文件夹
        public static string xlsxPath = "Assets/FrameWork/Xlsx";
        
        public static string xlsxOutPath = "Assets/FrameWork/Asset/Xlsx";
        
        public static string xlsxOutScriptPath = "Assets/FrameWork/Scripts/Xlsx";
        /// <summary>
        /// 配置表生成引用
        /// </summary>
        public static string[] xlsxSpawnUse = new string[] {"System.Collections.Generic","UnityEngine","Xlsx","FrameWork"};
    }
    
    
    [UnityEditor.AssetImporters.ScriptedImporter(1, ".xlsx")]
    public class ExcelTool:UnityEditor.AssetImporters.ScriptedImporter
    {
        
        public static List<string> paths = new List<string>();

        //public static ConfigData ConfigData;
        public override void OnImportAsset(UnityEditor.AssetImporters.AssetImportContext ctx)
        {
            MyLog.LogWarning("导入");
            if (!Directory.Exists(ExcelToolData.xlsxPath))
            {
                Directory.CreateDirectory(ExcelToolData.xlsxPath);
                return;
            }
            if (!ctx.assetPath.StartsWith(ExcelToolData.xlsxPath)) return;
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
                Spawn();
            }
            ExcelTool.paths.Clear();
        }

        
        
        [MenuItem("FrameWork/配置表/生成所有配置表代码")]
        public static void SpawnAllXlsx()
        {
            
            //var configData=AssetDatabase.LoadAssetAtPath<ConfigData>("Assets/FrameWork/Resources/ConfigData.asset");
            
            if (!Directory.Exists(ExcelToolData.xlsxPath))
            {
                MyLog.LogError(ExcelToolData.xlsxPath+"---路径不存在");
                return;
            }
            
            DirectoryInfo directoryInfo = new DirectoryInfo(ExcelToolData.xlsxPath);

            
            
            var  files=directoryInfo.GetFiles();
            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
                var path = file.FullName;
                var fileName = Path.GetFileNameWithoutExtension(path);
                if (fileName.StartsWith("~$")) continue;
                if (Path.GetExtension(path)!=".xlsx")continue;
                ExcelTool.paths.Add(path); //添加到待转换列表里 等待资源加载完成后转换   
            }
            if (ExcelTool.paths == null
                || ExcelTool.paths.Count == 0) return;
            
            Spawn();
            ExcelTool.paths.Clear();
        }
        

        private static void Spawn()
        {
            //var configData=AssetDatabase.LoadAssetAtPath<ConfigData>("Assets/FrameWork/Resources/ConfigData.asset");
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
                if (table.Rows[0][0].ToString()=="string")
                {
                    SpawnKey(name,table.Rows);
                }
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

                var outPath = ExcelToolData.xlsxOutPath;
                
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
            ABEditor.AssetPackaged();
        }

        private static void SpawnKey(string fileName,DataRowCollection coll)
        {
            var configData=AssetDatabase.LoadAssetAtPath<ConfigData>("Assets/FrameWork/Resources/ConfigData.asset");
            var xlsxName = fileName.Split('_')[1];
            var xlsxKeyName = "Xlsx_"+xlsxName+"_Key";
            var xlsxTypeName = "Xlsx_"+xlsxName+"_Type";
            HashSet<string> key = new HashSet<string>();

            var outPath = ExcelToolData.xlsxOutScriptPath;
            
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
            //var configData=AssetDatabase.LoadAssetAtPath<ConfigData>("Assets/FrameWork/Resources/ConfigData.asset");
            _queryClassBuilder.Clear();
            var xlsxName = fileName.Split('_')[1];
            var xlsxQueryName = fileName+"_Query";

            var outPath = ExcelToolData.xlsxOutScriptPath;
            
            if (!Directory.Exists(outPath))
            {
                Directory.CreateDirectory(outPath);
            }
            
            using (StreamWriter sw = new StreamWriter(outPath + "\\" + xlsxQueryName + ".cs", false))
            {
                // sw.WriteLine("using System.Collections.Generic;");
                // sw.WriteLine("using UnityEngine;");
                // sw.WriteLine("using FrameWork;");

                for (int i = 0; i < ExcelToolData.xlsxSpawnUse.Length; i++)
                {
                    sw.WriteLine("using "+ExcelToolData.xlsxSpawnUse[i]+";");
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
                sw.WriteLine($"\t\t\tvar xlsx=ABMrg.Load<TextAsset>(\"{fileName}\");");
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
                //sw.WriteLine("\t\t\t\t\tif (string.IsNullOrEmpty(items[j]))continue;");
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
            var configData=Resources.Load<ConfigData>("ConfigData");
            _classBuilder.Clear();
            var outPath = ExcelToolData.xlsxOutScriptPath;

            if (!Directory.Exists(outPath))
            {
                Directory.CreateDirectory(outPath);
            }
            
            using (StreamWriter sw=new StreamWriter(outPath+"\\"+fileName+".cs",false))
            {
                // sw.WriteLine("using System.Collections.Generic;");
                // sw.WriteLine("using UnityEngine;");
                
                for (int i = 0; i < ExcelToolData.xlsxSpawnUse.Length; i++)
                {
                    sw.WriteLine("using "+ExcelToolData.xlsxSpawnUse[i]+";");
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
            using (var fileStream=File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var reader = ExcelReaderFactory.CreateOpenXmlReader(fileStream);
                var dataSet = reader.AsDataSet();
                return dataSet;
            }
            
        }
    }
}