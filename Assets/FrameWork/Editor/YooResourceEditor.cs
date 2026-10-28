using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;

namespace FrameWork.Editor
{
    public static class YooResourceEditor
    {
        public const string SettingsPath = "Assets/FrameWork/Resources/YooResourceSettings.asset";
        public const string BuiltinRoot = "Library/FrameWork/YooAssetBuiltin";

        [MenuItem("FrameWork/YooAsset/配置资源收集")]
        public static void Configure()
        {
            Directory.CreateDirectory("Assets/FrameWork/YooAssetSettings");
            Directory.CreateDirectory("Assets/FrameWork/Resources");
            Directory.CreateDirectory(ABEditor.abAssetPath);
            if (Directory.GetFiles(ABEditor.abAssetPath).Any(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("请将 Asset 根目录的资源移到子文件夹：Base 放基础资源，其他目录放按需下载资源。");
            AssetDatabase.Refresh();
            var runtime = AssetDatabase.LoadAssetAtPath<YooResourceSettings>(SettingsPath);
            if (runtime == null)
            {
                runtime = ScriptableObject.CreateInstance<YooResourceSettings>();
                AssetDatabase.CreateAsset(runtime, SettingsPath);
            }
            var setting = BundleCollectorSettingData.Setting;
            var package = setting.Packages.FirstOrDefault(x => x.PackageName == runtime.packageName)
                ?? BundleCollectorSettingData.CreatePackage(runtime.packageName);
            package.EnableAddressable = true;
            var group = package.Groups.FirstOrDefault(x => x.GroupName == "FrameWork")
                ?? BundleCollectorSettingData.CreateGroup(package, "FrameWork");
            SynchronizeCollectors(group);
            BundleCollectorSettingData.ModifyPackage(package);
            BundleCollectorSettingData.SaveFile();
        }

        // 自动同步只处理已有配置，不在资源导入期间创建配置或调用 Refresh。
        internal static void SynchronizeAssetFolders()
        {
            if (!AssetDatabase.IsValidFolder(ABEditor.abAssetPath)) return;
            var runtime = AssetDatabase.LoadAssetAtPath<YooResourceSettings>(SettingsPath);
            if (runtime == null) return;
            var package = BundleCollectorSettingData.Setting.Packages
                .FirstOrDefault(x => x.PackageName == runtime.packageName);
            var group = package?.Groups.FirstOrDefault(x => x.GroupName == "FrameWork");
            if (group == null) return;
            if (!SynchronizeCollectors(group)) return;
            BundleCollectorSettingData.ModifyPackage(package);
            BundleCollectorSettingData.SaveFile();
        }

        private static bool SynchronizeCollectors(BundleCollectorGroup group)
        {
            bool changed = false;
            string root = ABEditor.abAssetPath.TrimEnd('/');
            // 按 GUID 跟随重命名/移动，保留已有打包规则和标签；删除失效引用。
            foreach (var collector in group.Collectors.ToArray())
            {
                if (collector.CollectPath != root &&
                    !collector.CollectPath.StartsWith(root + "/", StringComparison.Ordinal)) continue;
                string path = string.IsNullOrEmpty(collector.CollectorGUID) ? string.Empty
                    : AssetDatabase.GUIDToAssetPath(collector.CollectorGUID);
                if (string.IsNullOrEmpty(path)) path = collector.CollectPath;
                if (!path.StartsWith(root + "/", StringComparison.Ordinal) ||
                    (!AssetDatabase.IsValidFolder(path) && !File.Exists(path)))
                {
                    BundleCollectorSettingData.RemoveCollector(group, collector);
                    changed = true;
                    continue;
                }
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (collector.CollectPath != path || collector.CollectorGUID != guid)
                {
                    collector.CollectPath = path;
                    collector.CollectorGUID = guid;
                    changed = true;
                }
            }
            // 为新增的一级目录补充收集器，保持 PackDirectory 分包方式。
            foreach (string directory in Directory.GetDirectories(ABEditor.abAssetPath))
            {
                string path = directory.Replace('\\', '/');
                if (path.EndsWith("~", StringComparison.Ordinal)) continue;
                if (!AssetDatabase.IsValidFolder(path)) continue;
                if (group.Collectors.Any(x => x.CollectPath == path ||
                    x.CollectPath.StartsWith(path + "/", StringComparison.Ordinal))) continue;
                string folder = Path.GetFileName(path);
                bool startup = folder == "Dll" || folder == "Ui" || folder == "Xlsx" || folder == "Base";
                BundleCollectorSettingData.CreateCollector(group, new BundleCollector {
                    CollectPath = path,
                    CollectorGUID = AssetDatabase.AssetPathToGUID(path),
                    CollectorType = ECollectorType.MainAssetCollector,
                    AddressRuleName = nameof(AddressByFileName),
                    PackRuleName = nameof(PackDirectory),
                    FilterRuleName = nameof(CollectAll),
                    AssetTags = startup ? "startup" : folder
                });
                changed = true;
            }
            return changed;
        }

        [MenuItem("FrameWork/YooAsset/构建当前平台资源")]
        public static void BuildResources()
        {
            var config = AssetDatabase.LoadAssetAtPath<ConfigData>(Config.AssetPath);
            string version = (config != null ? config.versions : "1.0.0") + "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            Build(version);
        }

        public static BuildResult Build(string version)
        {
            Configure();
            var runtime = AssetDatabase.LoadAssetAtPath<YooResourceSettings>(SettingsPath);
            var target = EditorUserBuildSettings.activeBuildTarget;
            var parameters = new LegacyBuildParameters {
                BuildOutputRoot = "BuildOutput/YooAsset",
                BundledFileRoot = Path.Combine(BuiltinRoot, target.ToString(), "yoo"),
                BuildPipeline = nameof(LegacyBuildPipeline),
                BuildBundleType = (int)EBundleType.AssetBundle,
                BuildTarget = target,
                PackageName = runtime.packageName,
                PackageVersion = version,
                VerifyBuildingResult = true,
                CompressOption = ECompressOption.LZ4,
                BundledCopyOption = runtime.playerMode == YooPlayerMode.Offline
                    ? EBundledCopyOption.ClearAndCopyAll : EBundledCopyOption.None,
                BundledCopyParams = "startup"
            };
            var result = new LegacyBuildPipeline().Run(parameters, true);
            if (!result.Success)
                throw new InvalidOperationException($"YooAsset 构建失败：{result.FailedTask}\n{result.ErrorInfo}");
            Debug.Log("YooAsset 资源构建完成：" + result.OutputPackageDirectory);
            return result;
        }
    }
    [InitializeOnLoad]
    internal sealed class YooAssetFolderPostprocessor : AssetPostprocessor
    {
        static YooAssetFolderPostprocessor()
        {
            QueueSync();
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (importedAssets.Concat(deletedAssets).Concat(movedAssets).Concat(movedFromAssetPaths)
                .Any(path => path == ABEditor.abAssetPath ||
                    path.StartsWith(ABEditor.abAssetPath + "/", StringComparison.Ordinal)))
                QueueSync();
        }

        private static void QueueSync()
        {
            EditorApplication.delayCall -= Sync;
            EditorApplication.delayCall += Sync;
        }

        private static void Sync()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                QueueSync();
                return;
            }
            try
            {
                YooResourceEditor.SynchronizeAssetFolders();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
