using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
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
                    XlsxAssetWriter.Queue(name, path);
                }
                catch (Exception error) { Debug.LogError($"[配置表] {path}：{error.Message}"); }
            }
            AssetDatabase.Refresh();
            XlsxAssetWriter.Schedule();
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
                    string value = Convert.ToString(table.Rows[row][column], System.Globalization.CultureInfo.InvariantCulture);
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
            var xlsxName = fileName.Substring("Xlsx_".Length);
            var xlsxKeyName = "Xlsx_"+xlsxName+"_Key";
            var xlsxTypeName = "Xlsx_"+xlsxName+"_Type";
            HashSet<string> key = new HashSet<string>();

            var outPath = ExcelToolData.xlsxOutScriptPath;
            
            if (!Directory.Exists(outPath))
            {
                Directory.CreateDirectory(outPath);
            }
            
            using (StreamWriter sw = new StreamWriter(Path.Combine(outPath, xlsxKeyName + ".cs"), false))
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

        public static void SpawnQueryClass(string fileName,List<object> row,List<object> row2)
        {
            var xlsxQueryName = fileName+"_Query";
            var assetName = XlsxAssetWriter.AssetName(fileName);

            var outPath = ExcelToolData.xlsxOutScriptPath;
            
            if (!Directory.Exists(outPath))
            {
                Directory.CreateDirectory(outPath);
            }
            
            using (StreamWriter sw = new StreamWriter(Path.Combine(outPath, xlsxQueryName + ".cs"), false))
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
                sw.WriteLine($"\t\t\tvar nextData = Tool.LoadXlsxAsset<{assetName}>(\"{assetName}\").Rows;");
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
        

        public static void SpawnClass(string fileName,List<List<object>> book)
        {
            var outPath = ExcelToolData.xlsxOutScriptPath;

            if (!Directory.Exists(outPath))
            {
                Directory.CreateDirectory(outPath);
            }
            
            using (StreamWriter sw = new StreamWriter(Path.Combine(outPath, fileName + ".cs"), false))
            {
                // sw.WriteLine("using System.Collections.Generic;");
                // sw.WriteLine("using UnityEngine;");
                
                for (int i = 0; i < ExcelToolData.xlsxSpawnUse.Length; i++)
                {
                    sw.WriteLine("using "+ExcelToolData.xlsxSpawnUse[i]+";");
                }
                
                sw.WriteLine("namespace Xlsx");
                sw.WriteLine("{");
                sw.WriteLine("\tpublic class " + fileName + " : ScriptableObject");
                sw.WriteLine("\t{");
                
                for (int i = 0; i < book[0].Count; i++)
                {
                    sw.WriteLine("\t\t/// <summary>");
                    foreach (var line in book[0][i].ToString().Replace("\r", "").Split('\n'))
                        sw.WriteLine("\t\t/// " + System.Security.SecurityElement.Escape(line));
                    sw.WriteLine("\t\t/// </summary>");
                    sw.WriteLine("\t\tpublic "+book[1][i]+" "+book[2][i]+";");
                    sw.WriteLine("");
                    
                }
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

    internal static class XlsxAssetWriter
    {
        private const string PendingKey = "FrameWork.Xlsx.PendingAssets";

        internal static string AssetName(string rowName) => "XlsxAsset_" + rowName.Substring("Xlsx_".Length);

        internal static void Queue(string rowName, string path)
        {
            string assetName = AssetName(rowName);
            string script = Path.Combine(ExcelToolData.xlsxOutScriptPath, assetName + ".cs");
            string source = "using System.Collections.Generic;\nusing Sirenix.OdinInspector;\nusing UnityEngine;\n\n" +
                "namespace Xlsx\n{\n" +
                $"    public sealed class {assetName} : ScriptableObject\n    {{\n" +
                $"        [LabelText(\"配置表数据行\")] public List<{rowName}> Rows = new List<{rowName}>();\n" +
                "    }\n}\n";
            if (!File.Exists(script) || File.ReadAllText(script) != source)
                File.WriteAllText(script, source);

            var pending = ReadPending();
            if (!pending.Contains(path, StringComparer.OrdinalIgnoreCase))
                pending.Add(path);
            SessionState.SetString(PendingKey, JsonMapper.ToJson(pending));
        }

        private static List<string> ReadPending() =>
            JsonMapper.ToObject<List<string>>(SessionState.GetString(PendingKey, "[]"));

        [InitializeOnLoadMethod]
        internal static void Schedule()
        {
            EditorApplication.delayCall -= GeneratePending;
            EditorApplication.delayCall += GeneratePending;
        }

        private static void GeneratePending()
        {
            var paths = ReadPending();
            if (paths.Count == 0)
                return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                Schedule();
                return;
            }

            SessionState.EraseString(PendingKey);
            Directory.CreateDirectory(ExcelToolData.xlsxOutPath);
            AssetDatabase.Refresh();
            foreach (var path in paths)
            {
                try
                {
                    using var data = XlsxAssetPostprocessor.GetTabelData(path);
                    var table = data.Tables.Count > 0 ? data.Tables[0] : null;
                    var errors = XlsxAssetPostprocessor.ValidateTable(table, path);
                    if (errors.Count > 0)
                    {
                        foreach (var error in errors) Debug.LogError(error);
                        continue;
                    }

                    string rowName = "Xlsx_" + Path.GetFileNameWithoutExtension(path);
                    SaveTable(rowName, table);
                }
                catch (Exception error)
                {
                    Debug.LogError($"[配置表] {path}：{error}");
                }
            }

            AssetDatabase.SaveAssets();
            var addresses = Directory.GetFiles(ExcelToolData.xlsxOutPath, "XlsxAsset_*.asset")
                .Select(Path.GetFileNameWithoutExtension).OrderBy(name => name, StringComparer.Ordinal).ToList();
            File.WriteAllText(Path.Combine(ExcelToolData.xlsxOutPath, "AllXlsxAssets.json"), JsonMapper.ToJson(addresses));
            XlsxAssetPostprocessor.SpawnAllXlsxText();
            AssetDatabase.Refresh();
        }

        private static void SaveTable(string rowName, DataTable table)
        {
            var assembly = typeof(Tool).Assembly;
            string assetName = AssetName(rowName);
            var rowType = assembly.GetType("Xlsx." + rowName, throwOnError: true);
            var assetType = assembly.GetType("Xlsx." + assetName, throwOnError: true);
            if (!typeof(ScriptableObject).IsAssignableFrom(rowType))
                throw new InvalidOperationException($"请等待 {rowName} 的 ScriptableObject 类型编译完成后重新转换。");

            var rowsField = assetType.GetField("Rows");
            var rows = (System.Collections.IList)Activator.CreateInstance(rowsField.FieldType);
            const System.Reflection.BindingFlags fieldFlags = System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.DeclaredOnly;
            var fields = new System.Reflection.FieldInfo[table.Columns.Count];
            for (int column = 0; column < fields.Length; column++)
            {
                string fieldName = table.Rows[2][column].ToString();
                string fieldType = table.Rows[1][column].ToString();
                var sample = Tool.ConversionType(fieldType, "");
                var expectedType = sample?.GetType() ?? (fieldType == "Vector2" ? typeof(Vector2) : typeof(Vector3));
                var field = rowType.GetField(fieldName, fieldFlags);
                if (field == null || field.FieldType != expectedType)
                    throw new InvalidOperationException($"已编译的配置字段与表头不匹配：{rowName}.{fieldName}");
                fields[column] = field;
            }
            if (rowType.GetFields(fieldFlags).Length != fields.Length)
                throw new InvalidOperationException($"已编译的配置字段数量与表头不匹配：{rowName}");

            string path = Path.Combine(ExcelToolData.xlsxOutPath, assetName + ".asset").Replace('\\', '/');
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset != null && asset.GetType() != assetType)
                throw new InvalidOperationException($"配置资产路径已被占用：{path}");
            var oldRows = AssetDatabase.LoadAllAssetsAtPath(path)
                .Where(item => item != null && item.GetType() == rowType).ToArray();

            bool committed = false;
            try
            {
                for (int index = 3; index < table.Rows.Count; index++)
                {
                    var row = ScriptableObject.CreateInstance(rowType);
                    rows.Add(row);
                    row.name = $"{rowName}_{index - 2}";
                    for (int column = 0; column < fields.Length; column++)
                    {
                        string type = table.Rows[1][column].ToString();
                        string value = Convert.ToString(table.Rows[index][column], System.Globalization.CultureInfo.InvariantCulture);
                        var converted = Tool.ConversionType(type, value);
                        if (converted != null)
                            fields[column].SetValue(row, converted);
                    }
                }

                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance(assetType);
                    AssetDatabase.CreateAsset(asset, path);
                }
                foreach (ScriptableObject row in rows)
                    AssetDatabase.AddObjectToAsset(row, asset);
                rowsField.SetValue(asset, rows);
                EditorUtility.SetDirty(asset);
                committed = true;
                foreach (var row in oldRows)
                    UnityEngine.Object.DestroyImmediate(row, true);
            }
            finally
            {
                if (!committed)
                    foreach (ScriptableObject row in rows)
                        UnityEngine.Object.DestroyImmediate(row, true);
            }
        }
    }
}
