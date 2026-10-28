using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using ExcelDataReader;
using LitJson;
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
            if (!ctx.assetPath.StartsWith(ExcelToolData.xlsxPath.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase)) return;
            var path = ctx.assetPath;
            var fileName = Path.GetFileNameWithoutExtension(path);
            if (fileName.StartsWith("~$")) return;
            if (!paths.Contains(path)) paths.Add(path);
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
            
            if (Application.isBatchMode || EditorUtility.DisplayDialog("信息", "检测到有表格内容发生改变，是否重新转换", "是", "否"))
            {
                Spawn();
            }
            ExcelTool.paths.Clear();
        }

        
        
        [MenuItem("FrameWork/配置表/生成所有配置表代码")]
        public static void SpawnAllXlsx()
        {
            
            //var configData=AssetDatabase.LoadAssetAtPath<ConfigData>(Config.AssetPath);
            
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
            var paths = ExcelTool.paths.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            ExcelTool.paths.Clear();
            foreach (var path in paths)
            {
                try
                {
                    using var dataSet = GetTabelData(path);
                    if (dataSet.Tables.Count == 0)
                    {
                        Debug.LogError($"[配置表] {path}：没有可读取的工作表。");
                        continue;
                    }
                    var table = dataSet.Tables[0];
                    var errors = ValidateTable(table, path);
                    if (errors.Count > 0)
                    {
                        foreach (var error in errors) Debug.LogError(error);
                        continue; // 校验通过前不覆盖已有生成文件。
                    }
                    var name = "Xlsx_" + Path.GetFileNameWithoutExtension(path);
                    _rows.Clear();
                    for (int row = 0; row < 3; row++) _rows.Add(table.Rows[row].ItemArray.ToList());
                    if (table.Rows[1][0].ToString() == "string") SpawnKey(name, table.Rows);
                    SpawnClass(name, _rows);
                    SpawnQueryClass(name, _rows[1], _rows[2]);
                    _stringBuilder.Clear();
                    foreach (DataRow row in table.Rows)
                    {
                        for (int column = 0; column < table.Columns.Count; column++)
                        {
                            if (column > 0) _stringBuilder.Append('|');
                            _stringBuilder.Append(row[column]);
                        }
                        _stringBuilder.Append('\n');
                    }
                    Directory.CreateDirectory(ExcelToolData.xlsxOutPath);
                    File.WriteAllText(Path.Combine(ExcelToolData.xlsxOutPath, name + ".txt"), _stringBuilder.ToString());
                }
                catch (Exception error) { Debug.LogError($"[配置表] {path}：{error.Message}"); }
            }
            SpawnAllXlsxText();
            AssetDatabase.Refresh();
        }

        private static readonly HashSet<string> SupportedTypes = new HashSet<string>
        { "string", "int", "float", "long", "double", "Vector2", "Vector3", "string[]", "int[]", "float[]" };
        private static readonly HashSet<string> Keywords = new HashSet<string>((
            "abstract as base bool break byte case catch char checked class const continue decimal default delegate do double else enum event explicit extern false finally fixed float for foreach goto if implicit in int interface internal is lock long namespace new null object operator out override params private protected public readonly ref return sbyte sealed short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe ushort using virtual void volatile while").Split(' '));

        private static bool IsIdentifier(string name) => !Keywords.Contains(name) &&
            System.Text.RegularExpressions.Regex.IsMatch(name ?? "", @"^[_\p{L}\p{Nl}][\p{L}\p{Nl}\p{Nd}\p{Pc}\p{Mn}\p{Mc}\p{Cf}]*$");

        /// <summary>错误定位使用 Excel 的一基行列；重复主 key 保留原有分组语义。</summary>
        internal static List<string> ValidateTable(DataTable table, string path)
        {
            var errors = new List<string>();
            void Error(int row, int column, string reason) => errors.Add($"[配置表] {path}，第 {row} 行、第 {column} 列：{reason}");
            if (table == null || table.Columns.Count == 0 || table.Rows.Count < 3)
            {
                Error(1, 1, "至少需要描述、字段类型、字段名三行表头和一列数据。");
                return errors;
            }
            string generatedName = "Xlsx_" + Path.GetFileNameWithoutExtension(path);
            if (!IsIdentifier(generatedName)) Error(1, 1, "文件名不能生成有效的 C# 类名。");
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int column = 0; column < table.Columns.Count; column++)
            {
                string type = table.Rows[1][column].ToString();
                string name = table.Rows[2][column].ToString();
                bool supported = SupportedTypes.Contains(type);
                if (!supported) Error(2, column + 1, "不支持的字段类型：" + type);
                if (!IsIdentifier(name) || name == generatedName) Error(3, column + 1, "字段名不是有效的 C# 标识符：" + name);
                if (!names.Add(name)) Error(3, column + 1, "重复字段名：" + name);
                for (int row = 0; row < table.Rows.Count; row++)
                {
                    string value = table.Rows[row][column].ToString();
                    if (value.IndexOfAny(new[] { '|', '\r', '\n' }) >= 0)
                        Error(row + 1, column + 1, "单元格包含配置格式不支持的分隔符或换行。");
                    if (row < 3 || !supported) continue;
                    if (column == 0 && string.IsNullOrWhiteSpace(value)) Error(row + 1, 1, "主 key 不能为空。");
                    if (column == 0 && type == "string" && (!IsIdentifier(value) || value == generatedName + "_Key"))
                        Error(row + 1, 1, "主 key 不能生成有效的枚举成员：" + value);
                    try
                    {
                        if (type == "Vector2" || type == "Vector3")
                        {
                            int dimensions = type == "Vector2" ? 2 : 3;
                            if (!string.IsNullOrEmpty(value) && value.Split(',').Length != dimensions)
                                throw new FormatException("向量分量数应为 " + dimensions);
                        }
                        Tool.ConversionType(type, value);
                    }
                    catch (Exception error) { Error(row + 1, column + 1, $"值不能转换成 {type}：{value}（{error.Message}）"); }
                }
            }
            return errors;
        }

        private static void SpawnKey(string fileName,DataRowCollection coll)
        {
            var configData=AssetDatabase.LoadAssetAtPath<ConfigData>(Config.AssetPath);
            var xlsxName = fileName.Substring("Xlsx_".Length);
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
            //var configData=AssetDatabase.LoadAssetAtPath<ConfigData>(Config.AssetPath);
            _queryClassBuilder.Clear();
            var xlsxName = fileName.Substring("Xlsx_".Length);
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
                sw.WriteLine("\t\t\tUpdate();");
                sw.WriteLine("\t\t}");
                
                sw.WriteLine("\t\tpublic static void Update()");
                sw.WriteLine("\t\t{");
                sw.WriteLine($"\t\t\tvar xlsx=Tool.LoadXlsx(\"{fileName}\");");
                sw.WriteLine("\t\t\tvar itemData=xlsx.Split('\\n');");
                sw.WriteLine("\t\t\tvar fileNames = itemData[2].Split('|');");
                sw.WriteLine("\t\t\tvar fileTypes = itemData[1].Split('|');");
                sw.WriteLine($"\t\t\tvar nextData = new List<{fileName}>();");
                sw.WriteLine($"\t\t\tvar type = typeof({fileName});");
                sw.WriteLine("\t\t\tvar fields = new System.Reflection.FieldInfo[fileNames.Length];");
                sw.WriteLine("\t\t\tfor (int j = 0; j < fields.Length; j++) fields[j] = type.GetField(fileNames[j]);");
                sw.WriteLine("\t\t\tfor (int i = 3; i < itemData.Length; i++)");
                sw.WriteLine("\t\t\t{");
                sw.WriteLine("\t\t\t\tif (string.IsNullOrEmpty(itemData[i]))continue;");
                sw.WriteLine("\t\t\t\tvar items = itemData[i].Split('|');");
                sw.WriteLine($"\t\t\t\tvar xlsxData = new {fileName}();");

                sw.WriteLine("\t\t\t\tfor (int j = 0; j < items.Length; j++)");
                sw.WriteLine("\t\t\t\t{");
                //sw.WriteLine("\t\t\t\t\tif (string.IsNullOrEmpty(items[j]))continue;");
                sw.WriteLine("\t\t\t\t\tfields[j].SetValue(xlsxData,Tool.ConversionType(fileTypes[j],items[j]));");
                sw.WriteLine("\t\t\t\t}");
                sw.WriteLine("\t\t\t\tnextData.Add(xlsxData);");
                sw.WriteLine("\t\t\t}");
                sw.WriteLine($"\t\t\tvar nextIndex = new XlsxData<{row[0]},{fileName}>(\"{row2[0]}\", nextData);");
                sw.WriteLine("\t\t\tdata = nextData;");
                sw.WriteLine("\t\t\tXlsxDataAsOneKey = nextIndex;");
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
            var configData=AssetDatabase.LoadAssetAtPath<ConfigData>(Config.AssetPath);
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
                using var reader = ExcelReaderFactory.CreateOpenXmlReader(fileStream);
                var dataSet = reader.AsDataSet();
                return dataSet;
            }
            
        }


        public static void SpawnAllXlsxText()
        {
            Directory.CreateDirectory(ExcelToolData.xlsxOutPath);
            DirectoryInfo directoryInfo = new DirectoryInfo(ExcelToolData.xlsxOutPath);
            var list=new List<string>();
            var files=directoryInfo.GetFiles("*.txt");
            for (int i = 0; i < files.Length; i++)
            {
                list.Add(Path.GetFileNameWithoutExtension(files[i].Name));
            }

            using (StreamWriter sw = new StreamWriter(ExcelToolData.xlsxOutPath+"/AllXlsx.json", false))
            {
                sw.WriteLine(JsonMapper.ToJson(list));
            }
        }
    }
}
