using System;
using System.Collections.Generic;
using System.Linq;
using HybridCLR;
using UnityEditor;
using UnityEditor.Compilation;

namespace FrameWork.Editor
{
    internal static class FrameworkHotUpdateLayout
    {
        internal static HashSet<string> AssemblyNames() => new HashSet<string>(
            SettingsUtil.HotUpdateAssemblyNamesExcludePreserved.Concat(new[] { "HotUpdate" }),
            StringComparer.Ordinal);

        internal static HashSet<string> SourceFiles()
        {
            var names = AssemblyNames();
            return new HashSet<string>(CompilationPipeline.GetAssemblies(AssembliesType.Player)
                .Where(a => names.Contains(a.name)).SelectMany(a => a.sourceFiles)
                .Select(p => p.Replace('\\', '/')), StringComparer.Ordinal);
        }

        internal static void ValidateBootScene()
        {
            var sources = SourceFiles();
            var businessDependencies = AssetDatabase.GetDependencies(FrameworkIntegrationEditor.BootScenePath, true)
                .Where(sources.Contains).ToArray();
            if (businessDependencies.Length != 0)
                throw new InvalidOperationException("启动场景或其预制体不能挂载热更新脚本，请在 DLL 加载后创建业务组件：\n" +
                    string.Join("\n", businessDependencies));
        }

        // 先加载被依赖程序集，不能依赖 HybridCLR 配置列表中的偶然顺序。
        internal static string[] OrderedDlls()
        {
            var names = new HashSet<string>(SettingsUtil.HotUpdateAssemblyNamesExcludePreserved, StringComparer.Ordinal);
            var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player).ToDictionary(a => a.name);
            foreach (var assembly in assemblies.Values.Where(a => !names.Contains(a.name)))
                foreach (var reference in assembly.assemblyReferences)
                    if (names.Contains(reference.name))
                        throw new InvalidOperationException($"AOT 程序集 {assembly.name} 引用了热更新程序集 {reference.name}，请先调整依赖。");
            var result = new List<string>();
            var visited = new HashSet<string>();
            var visiting = new HashSet<string>();
            void Visit(string name)
            {
                if (visited.Contains(name)) return;
                if (!visiting.Add(name)) throw new InvalidOperationException("热更新程序集存在循环依赖：" + name);
                if (assemblies.TryGetValue(name, out var assembly))
                    foreach (var dependency in assembly.assemblyReferences.Where(a => names.Contains(a.name)).OrderBy(a => a.name))
                        Visit(dependency.name);
                visiting.Remove(name);
                visited.Add(name);
                result.Add(name + ".dll");
            }
            foreach (string name in names.OrderBy(n => n, StringComparer.Ordinal)) Visit(name);
            return result.ToArray();
        }
    }
}
