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
            // 将旧的整目录收集器拆为一级目录，保持 PackDirectory 分包方式。
            foreach (var collector in group.Collectors.Where(x => x.CollectPath == ABEditor.abAssetPath).ToArray())
                BundleCollectorSettingData.RemoveCollector(group, collector);
            foreach (string directory in Directory.GetDirectories(ABEditor.abAssetPath))
            {
                string path = directory.Replace('\\', '/');
                if (path.EndsWith("~", StringComparison.Ordinal)) continue;
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
            }
            BundleCollectorSettingData.ModifyPackage(package);
            BundleCollectorSettingData.SaveFile();
        }

        [MenuItem("FrameWork/YooAsset/构建当前平台资源")]
        public static void BuildResources()
        {
            Configure();
            var config = AssetDatabase.LoadAssetAtPath<ConfigData>("Assets/FrameWork/Resources/ConfigData.asset");
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
                BundledCopyOption = EBundledCopyOption.ClearAndCopyByTags,
                BundledCopyParams = "startup"
            };
            var result = new LegacyBuildPipeline().Run(parameters, true);
            if (!result.Success)
                throw new InvalidOperationException($"YooAsset 构建失败：{result.FailedTask}\n{result.ErrorInfo}");
            Debug.Log("YooAsset 资源构建完成：" + result.OutputPackageDirectory);
            return result;
        }
    }
}
