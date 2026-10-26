using System;
using System.IO;
using HybridCLR;
using HybridCLR.Installer;
using UnityEditor;
using UnityEngine;

namespace FrameWork.Editor
{
    public static class HybridCLRPackageValidation
    {
        public static void Validate()
        {
            try
            {
                var installer = new InstallerController();
                foreach (string name in new[] { "netstandard2.0.dll", "netstandard2.1.dll" })
                {
                    string file = Path.Combine(SettingsUtil.HybridCLRDataPathInPackage, "NetStandard", name);
                    if (!File.Exists(file) || new FileInfo(file).Length == 0)
                        throw new FileNotFoundException("HybridCLR reference assembly missing", file);
                }
                Debug.Log($"HYBRIDCLR_PACKAGE_VALID: package={installer.PackageVersion}, hybridclr={installer.HybridclrLocalVersion}, il2cpp_plus={installer.Il2cppPlusLocalVersion}");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }
    }
}
