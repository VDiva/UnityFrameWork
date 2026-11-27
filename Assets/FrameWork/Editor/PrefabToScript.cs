using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace FrameWork.Editor
{
    public static class PrefabToScript
    {
        /// <summary>
        /// 预制体生成代码引用（仅包含框架自身存在的稳定命名空间，避免生成即报 CS0234）。
        /// </summary>
        private static readonly string[] SpawnScriptUsings =
        {
            "System.Collections.Generic",
            "UnityEngine",
            "UnityEngine.UI",
            "TMPro"
        };

        /// <summary>
        /// 预制体生成代码路径
        /// </summary>
        private const string SpawnScriptPath = "Assets/FrameWork/Scripts/PrefabScript";

        [MenuItem("Assets/FrameWork/预制体/生成代码", true)]
        private static bool ValidateCreateScript()
        {
            var obj = Selection.activeGameObject;
            return obj != null && PrefabUtility.IsPartOfPrefabAsset(obj);
        }

        [MenuItem("Assets/FrameWork/预制体/生成代码")]
        public static void CreateScript()
        {
            var obj = Selection.activeGameObject;
            if (obj == null)
            {
                Debug.LogWarning("[PrefabToScript] 请先在 Project 窗口选择一个预制体。");
                return;
            }

            if (obj.TryGetComponent<RectTransform>(out _))
                Generate(obj, "UiActor");
            else
                Generate(obj, "Actor");
        }

        private static void Generate(GameObject prefab, string baseClassName)
        {
            string rawName = prefab.name;
            string className = SanitizeIdentifier(rawName);
            string dir = Path.Combine(SpawnScriptPath, className);
            Directory.CreateDirectory(dir);

            var headerBuilder = new StringBuilder();
            var awakeBuilder = new StringBuilder();
            AppendUsings(headerBuilder);
            headerBuilder.AppendLine("namespace FrameWork");
            headerBuilder.AppendLine("{");
            headerBuilder.AppendLine($"\tpublic partial class {className} : {baseClassName}");
            headerBuilder.AppendLine("\t{");

            AppendUsings(awakeBuilder);
            awakeBuilder.AppendLine("namespace FrameWork");
            awakeBuilder.AppendLine("{");
            awakeBuilder.AppendLine($"\tpublic partial class {className} : {baseClassName}");
            awakeBuilder.AppendLine("\t{");
            awakeBuilder.AppendLine("\t\tpublic override void Awake()");
            awakeBuilder.AppendLine("\t\t{");
            awakeBuilder.AppendLine("\t\t\tbase.Awake();");

            var usedFieldNames = new HashSet<string>(StringComparer.Ordinal);
            WriteBindings(headerBuilder, awakeBuilder, string.Empty, prefab.transform, true, usedFieldNames);

            headerBuilder.AppendLine("\t}");
            headerBuilder.AppendLine("}");
            awakeBuilder.AppendLine("\t\t}");
            awakeBuilder.AppendLine("\t}");
            awakeBuilder.AppendLine("}");

            bool anyWritten = false;
            anyWritten |= WriteIfChanged(Path.Combine(dir, className + ".H.cs"), headerBuilder.ToString());
            anyWritten |= WriteIfChanged(Path.Combine(dir, className + ".Awake.cs"), awakeBuilder.ToString());

            string mainPath = Path.Combine(dir, className + ".cs");
            if (!File.Exists(mainPath))
            {
                var sb = new StringBuilder();
                AppendUsings(sb);
                sb.AppendLine("namespace FrameWork");
                sb.AppendLine("{");
                sb.AppendLine($"\tpublic partial class {className} : {baseClassName}");
                sb.AppendLine("\t{");
                sb.AppendLine("\t}");
                sb.AppendLine("}");
                anyWritten |= WriteIfChanged(mainPath, sb.ToString());
            }

            string attrPath = Path.Combine(dir, className + ".Attr.cs");
            if (!File.Exists(attrPath))
            {
                string assetPath = AssetDatabase.GetAssetPath(prefab);
                var importer = string.IsNullOrEmpty(assetPath) ? null : AssetImporter.GetAtPath(assetPath);
                string bundleName = importer != null ? importer.assetBundleName : string.Empty;

                var sb = new StringBuilder();
                AppendUsings(sb);
                sb.AppendLine("namespace FrameWork");
                sb.AppendLine("{");
                if (baseClassName == "UiActor")
                    sb.AppendLine("\t[UiMode(Mode.Normal)]");
                sb.AppendLine($"\t[ActorInfo(\"{bundleName}\",\"{rawName}\")]");
                sb.AppendLine($"\tpublic partial class {className} : {baseClassName}");
                sb.AppendLine("\t{");
                sb.AppendLine("\t}");
                sb.AppendLine("}");
                anyWritten |= WriteIfChanged(attrPath, sb.ToString());
            }

            string extendPath = Path.Combine(dir, className + ".Extend.cs");
            if (!File.Exists(extendPath))
            {
                var sb = new StringBuilder();
                AppendUsings(sb);
                sb.AppendLine("namespace FrameWork");
                sb.AppendLine("{");
                sb.AppendLine($"\tpublic partial class {className} : {baseClassName}");
                sb.AppendLine("\t{");
                sb.AppendLine($"\t\tpublic {className}(Transform trans): base(trans){{}}");
                sb.AppendLine($"\t\tpublic {className}(): base(){{}}");
                sb.AppendLine("\t}");
                sb.AppendLine("}");
                anyWritten |= WriteIfChanged(extendPath, sb.ToString());
            }

            string funPath = Path.Combine(dir, className + ".Fun.cs");
            if (!File.Exists(funPath))
            {
                var sb = new StringBuilder();
                AppendUsings(sb);
                sb.AppendLine("namespace FrameWork");
                sb.AppendLine("{");
                sb.AppendLine($"\tpublic partial class {className} : {baseClassName}");
                sb.AppendLine("\t{");
                sb.AppendLine("\t}");
                sb.AppendLine("}");
                anyWritten |= WriteIfChanged(funPath, sb.ToString());
            }

            if (anyWritten)
                AssetDatabase.Refresh();
        }

        private static void WriteBindings(
            StringBuilder header,
            StringBuilder awake,
            string relativePath,
            Transform trans,
            bool isRoot,
            HashSet<string> usedFieldNames)
        {
            if (trans.GetComponent<AddScripts>() != null)
            {
                var components = trans.GetComponents<Component>();
                string nodeVar = null;
                if (!isRoot && components.Length > 1)
                {
                    nodeVar = "node_" + usedFieldNames.Count;
                    awake.AppendLine($"\t\t\tvar {nodeVar} = GetGameObject().transform.Find(\"{relativePath}\");");
                }

                for (int i = 0; i < components.Length; i++)
                {
                    Component item = components[i];
                    if (item == null || item is AddScripts) continue;

                    string typeName = item.GetType().Name;
                    string baseFieldName = SanitizeIdentifier(typeName + trans.gameObject.name);
                    string fieldName = MakeUnique(baseFieldName, usedFieldNames);
                    header.AppendLine($"\t\tpublic {typeName} {fieldName};");

                    if (isRoot)
                        awake.AppendLine($"\t\t\t{fieldName} = GetGameObject().transform.GetComponent<{typeName}>();");
                    else if (nodeVar != null)
                        awake.AppendLine($"\t\t\t{fieldName} = {nodeVar}.GetComponent<{typeName}>();");
                    else
                        awake.AppendLine($"\t\t\t{fieldName} = GetGameObject().transform.Find(\"{relativePath}\").GetComponent<{typeName}>();");
                }
            }

            for (int i = 0; i < trans.childCount; i++)
            {
                Transform child = trans.GetChild(i);
                string childPath = string.IsNullOrEmpty(relativePath)
                    ? child.gameObject.name
                    : relativePath + "/" + child.gameObject.name;
                WriteBindings(header, awake, childPath, child, false, usedFieldNames);
            }
        }

        private static void AppendUsings(StringBuilder sb)
        {
            for (int i = 0; i < SpawnScriptUsings.Length; i++)
                sb.AppendLine($"using {SpawnScriptUsings[i]};");
        }

        private static bool WriteIfChanged(string filePath, string content)
        {
            if (File.Exists(filePath) && File.ReadAllText(filePath) == content)
                return false;
            File.WriteAllText(filePath, content, new UTF8Encoding(false));
            return true;
        }

        private static string SanitizeIdentifier(string raw)
        {
            string cleaned = Regex.Replace(raw ?? string.Empty, @"[^\p{L}\p{Nd}_]", string.Empty);
            if (string.IsNullOrEmpty(cleaned) || char.IsDigit(cleaned[0]))
                cleaned = "_" + cleaned;
            return cleaned;
        }

        private static string MakeUnique(string candidate, HashSet<string> used)
        {
            if (used.Add(candidate)) return candidate;
            int suffix = 2;
            while (!used.Add(candidate + "_" + suffix)) suffix++;
            return candidate + "_" + suffix;
        }
    }
}
