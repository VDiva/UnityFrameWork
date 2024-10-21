//-----------------------------------------------------------------------------
// Copyright 2012-2024 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

#if (UNITY_IOS || UNITY_TVOS || UNITY_VISIONOS) && UNITY_2017_1_OR_NEWER

// Unity versions where xcframework support was added
// 2023.2.18f1
// 2022.3.23f1
// 2021.3.37f1

// There has to be a better way...
#if UNITY_2023_2_OR_NEWER && !(UNITY_2023_2_0 || UNITY_2023_2_1 || UNITY_2023_2_2 || UNITY_2023_2_3 || UNITY_2023_2_4 || UNITY_2023_2_5 || UNITY_2023_2_6 || UNITY_2023_2_7 || UNITY_2023_2_8 || UNITY_2023_2_9 || UNITY_2023_2_10 || UNITY_2023_2_11 || UNITY_2023_2_12 || UNITY_2023_2_13 || UNITY_2023_2_14 || UNITY_2023_2_15 || UNITY_2023_2_16 || UNITY_2023_2_17)
#define AVPROVIDEO_UNITY_SUPPORTS_XCFRAMEWORKS
#elif UNITY_2023_1_OR_NEWER
#define AVPROVIDEO_UNITY_DOES_NOT_SUPPORT_XCFRAMEWORKS
#elif UNITY_2022_3_OR_NEWER && !(UNITY_2022_3_0 || UNITY_2022_3_1 || UNITY_2022_3_2 || UNITY_2022_3_3 || UNITY_2022_3_4 || UNITY_2022_3_5 || UNITY_2022_3_6 || UNITY_2022_3_7 || UNITY_2022_3_8 || UNITY_2022_3_9 || UNITY_2022_3_10 || UNITY_2022_3_11 || UNITY_2022_3_12 || UNITY_2022_3_13 || UNITY_2022_3_14 || UNITY_2022_3_15 || UNITY_2022_3_16 || UNITY_2022_3_17 || UNITY_2022_3_18 || UNITY_2022_3_19 || UNITY_2022_3_20 || UNITY_2022_3_21 || UNITY_2022_3_22)
#define AVPROVIDEO_UNITY_SUPPORTS_XCFRAMEWORKS
#elif UNITY_2022_1_OR_NEWER
#define AVPROVIDEO_UNITY_DOES_NOT_SUPPORT_XCFRAMEWORKS
#elif UNITY_2021_3_OR_NEWER && !(UNITY_2021_3_0 || UNITY_2021_3_1 || UNITY_2021_3_2 || UNITY_2021_3_3 || UNITY_2021_3_4 || UNITY_2021_3_5 || UNITY_2021_3_6 || UNITY_2021_3_7 || UNITY_2021_3_8 || UNITY_2021_3_9 || UNITY_2021_3_10 || UNITY_2021_3_11 || UNITY_2021_3_12 || UNITY_2021_3_13 || UNITY_2021_3_14 || UNITY_2021_3_15 || UNITY_2021_3_16 || UNITY_2021_3_17 || UNITY_2021_3_18 || UNITY_2021_3_19 || UNITY_2021_3_20 || UNITY_2021_3_21 || UNITY_2021_3_22 || UNITY_2021_3_23 || UNITY_2021_3_24 || UNITY_2021_3_25 || UNITY_2021_3_26 || UNITY_2021_3_27 || UNITY_2021_3_28 || UNITY_2021_3_29 || UNITY_2021_3_30 || UNITY_2021_3_31 || UNITY_2021_3_32 || UNITY_2021_3_33 || UNITY_2021_3_34 || UNITY_2021_3_35 || UNITY_2021_3_36)
#define AVPROVIDEO_UNITY_SUPPORTS_XCFRAMEWORKS
#else
#define AVPROVIDEO_UNITY_DOES_NOT_SUPPORT_XCFRAMEWORKS
#endif

using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System;
using System.IO;

namespace RenderHeads.Media.AVProVideo.Editor
{

	public class PostProcessBuild_iOS
	{
		const string AVProVideoPluginName = "AVProVideo.xcframework";

		const string AVProVideoBootstrap = "extern void AVPPluginUnityRegisterRenderingPlugin(void*);\nvoid AVPPluginBootstrap(void) {\n\tAVPPluginUnityRegisterRenderingPlugin(UnityRegisterRenderingPluginV5);\n}\n";
		const string AVProVideoForceSwift = "import Foundation\n";

		private class Platform
		{
			public BuildTarget target { get; }
			public string name { get; }
			public string guid { get; }

			public static Platform GetPlatformForTarget(BuildTarget target)
			{
				switch (target)
				{
					case BuildTarget.iOS:
						return new Platform(BuildTarget.iOS, "iOS", "a7ee58e0e533849d3a37458bc7df6df7");

					case BuildTarget.tvOS:
						return new Platform(BuildTarget.tvOS, "tvOS", "f83f62879d8fb417cb18d0547c9bfd02");
#if UNITY_2022_3
					case BuildTarget.VisionOS:
						return new Platform(BuildTarget.VisionOS, "visionOS", "fe151797423674af0941aae11c872b90");
#endif
					default:
						return null;
				}
			}

			private Platform(BuildTarget target, string name, string guid)
			{
				this.target = target;
				this.name = name;
				this.guid = guid;
			}
		}

		/// <summary>
		/// Get the plugin path for the platform specified
		/// </summary>
		/// <param name="platform">The platform</param>
		/// <param name="pluginName">The plugin's file name</param>
		/// <returns>The path of the plugin within Unity's assets folder</returns>
		private static string PluginPathForPlatform(Platform platform, string pluginName)
		{
			// See if we can find the plugin by GUID
			string pluginPath = AssetDatabase.GUIDToAssetPath(platform.guid);

			// If not, try and find it by name
			if (pluginPath.Length == 0)
			{
				Debug.LogWarningFormat("[AVProVideo] Failed to find plugin by GUID, will attempt to find it by name.");
				string[] guids = AssetDatabase.FindAssets(pluginName);
				if (guids != null && guids.Length > 0)
				{
					foreach (string guid in guids)
					{
						string assetPath = AssetDatabase.GUIDToAssetPath(guid);
						if (assetPath.Contains(platform.name))
						{
							pluginPath = assetPath;
							break;
						}
					}
				}
			}

			if (pluginPath.Length > 0)
			{
				Debug.LogFormat("[AVProVideo] Found plugin at '{0}'", pluginPath);
			}

			return pluginPath;
		}

		/// <summary>
		/// Gets the target guid if Unity's framework target from the project provided
		/// </summary>
		/// <param name="project">The project to get the guid from</param>
		/// <returns></returns>
		private static string GetUnityFrameworkTargetGuid(PBXProject project)
		{
			return project.GetUnityFrameworkTargetGuid();
		}

		/// <summary>
		/// Copies a directory.
		/// </summary>
		/// <remarks>
		/// Intended for use outside of Unity's project structure, this will skip meta files when copying.
		/// </remarks>
		/// <param name="src">The directory info of the directory to copy</param>
		/// <param name="dst">The directory info of the destination directory</param>
		private static void CopyDirectory(DirectoryInfo srcDirInfo, DirectoryInfo dstDirInfo)
		{
			// Make sure the target directory exists
			Directory.CreateDirectory(dstDirInfo.FullName);

			// Copy over the sub-directories
			foreach (DirectoryInfo subSrcDirInfo in srcDirInfo.GetDirectories())
			{
				DirectoryInfo subDstDirInfo = dstDirInfo.CreateSubdirectory(subSrcDirInfo.Name);
				CopyDirectory(subSrcDirInfo, subDstDirInfo);
			}

			// Copy over the files
			foreach (FileInfo srcFileInfo in srcDirInfo.GetFiles())
			{
				if (srcFileInfo.Extension == ".meta")
				{
					// Do not want to copy Unity's meta files into the built project
					continue;
				}

				srcFileInfo.CopyTo(Path.Combine(dstDirInfo.FullName, srcFileInfo.Name), true);
			}
		}

		/// <summary>
		/// Copies a directory.
		/// </summary>
		/// <remarks>
		/// Intended for use outside of Unity's project structure, this will skip meta files when copying.
		/// </remarks>
		/// <param name="src">The path of the directory to copy</param>
		/// <param name="dst">The path where the directory will be copied to</param>
		private static void CopyDirectory(string src, string dst)
		{
			CopyDirectory(new DirectoryInfo(src), new DirectoryInfo(dst));
		}

		/// <summary>
		/// Tests the target build platform to see if it's supported by this script
		/// </summary>
		/// <param name="target">The target build platform</param>
		/// <returns>true if the build target is supported, false otherwise</returns>
		private static bool IsBuildTargetSupported(BuildTarget target)
		{
			switch (target)
			{
				case BuildTarget.iOS:
				case BuildTarget.tvOS:
#if UNITY_2022_3
				case BuildTarget.VisionOS:
#endif
					return true;
				default:
					return false;
			}
		}

		/// <summary>
		/// Gets the Xcode project name for the target specified.
		/// </summary>
		/// <param name="target">The build target</param>
		/// <returns>The Xcode project name</returns>
		private static string GetXcodeProjectNameForBuildTarget(BuildTarget target)
		{
			switch (target)
			{
				case BuildTarget.iOS:
				case BuildTarget.tvOS:
					return "Unity-iPhone.xcodeproj";
#if UNITY_2022_3
				case BuildTarget.VisionOS:
					return "Unity-VisionOS.xcodeproj";
#endif
				default:
					Debug.LogError($"[AVProVideo] GetXcodeProjectNameForBuildTarget - unrecognised build target: {target}");
					return null;
			}
		}

		/// <summary>
		/// Post-process the generated Xcode project to add the plugin and any build configuration required.
		/// </summary>
		/// <param name="target">The target build platform</param>
		/// <param name="path">The path to the built project</param>
		[PostProcessBuild]
		public static void ModifyProject(BuildTarget target, string path)
		{
			if (!IsBuildTargetSupported(target))
			{
				// Nothing to be done
				return;
			}

			Debug.Log("[AVProVideo] Post-processing generated Xcode project...");
			Platform platform = Platform.GetPlatformForTarget(target);
			if (platform == null)
			{
				Debug.LogWarningFormat("[AVProVideo] Unknown build target: {0}, stopping", target);
				return;
			}

			// Create the path to the generated Xcode project file
			string xcodeProjectName = GetXcodeProjectNameForBuildTarget(target);
			if (xcodeProjectName == null)
			{
				return;
			}

			string xcodeProjectPath = Path.Combine(path, xcodeProjectName, "project.pbxproj");
			Debug.Log($"[AVProVideo] Opening Xcode project at: {path}");

			// Open the project
			PBXProject project = new PBXProject();
			project.ReadFromFile(xcodeProjectPath);

			// Attempt to find the plugin path
			string pluginPath = PluginPathForPlatform(platform, AVProVideoPluginName);
			if (pluginPath.Length == 0)
			{
				Debug.LogErrorFormat("[AVProVideo] Failed to find '{0}' for '{1}' in the Unity project. Something is horribly wrong, please reinstall AVPro Video.", AVProVideoPluginName, platform);
				return;
			}

			string destPluginPath = Path.Join("Libraries", "AVProVideo");
			Directory.CreateDirectory(Path.Join(path, destPluginPath));

			// Get the Unity framework target GUID
			string unityFrameworkTargetGuid = GetUnityFrameworkTargetGuid(project);

#if AVPROVIDEO_UNITY_DOES_NOT_SUPPORT_XCFRAMEWORKS
			// Get the path to the xcframework
			// string xcframeworkPath = ConvertPluginAssetPathToXcodeProjectPath(pluginPath, "Libraries");
			string xcframeworkPath = Path.Join(destPluginPath, AVProVideoPluginName);

			// Copy over the xcframework to the generated xcode project
			Debug.Log($"[AVProVideo] Copying AVProVideo.xcframework into the Xcode project at {destPluginPath}");
			CopyDirectory(pluginPath, Path.Combine(path, xcframeworkPath));

			if (!project.ContainsFileByProjectPath(xcframeworkPath))
			{
				Debug.Log("[AVProVideo] Adding AVProVideo.xcframework to the UnityFramework target");
				// Add the xcframework and sundry files to the project
				string xcframeworkGuid = project.AddFile(xcframeworkPath, xcframeworkPath);
				// Get the frameworks build phase and add the xcframework to it
				string frameworksBuildPhaseForUnityFrameworkTarget = project.GetFrameworksBuildPhaseByTarget(unityFrameworkTargetGuid);
				project.AddFileToBuildSection(unityFrameworkTargetGuid, frameworksBuildPhaseForUnityFrameworkTarget, xcframeworkGuid);
			}
#endif

			Debug.Log("[AVProVideo] Writing AVProVideoBootstrap.m to the UnityFramework target");
			string bootstrapPath = Path.Join(destPluginPath, "AVProVideoBootstrap.m");
			File.WriteAllText(Path.Combine(path, bootstrapPath), AVProVideoBootstrap);
			string bootstrapGuid = project.AddFile(bootstrapPath, bootstrapPath);
			project.AddFileToBuild(unityFrameworkTargetGuid, bootstrapGuid);

			string forceSwiftPath = Path.Join(destPluginPath, "AVProVideoForceSwift.swift");
			Debug.Log("[AVProVideo] Writing AVProVideoForceSwift.swift to the UnityFramework target");
			File.WriteAllText(Path.Combine(path, forceSwiftPath), AVProVideoForceSwift);
			string forceSwiftGuid = project.AddFile(forceSwiftPath, forceSwiftPath);
			project.AddFileToBuild(unityFrameworkTargetGuid, forceSwiftGuid);

			// Make sure the swift version is set to 5.0
			string swiftVersionStr = project.GetBuildPropertyForAnyConfig(unityFrameworkTargetGuid, "SWIFT_VERSION");
			decimal swiftVersion;
			if (!Decimal.TryParse(swiftVersionStr, out swiftVersion) || (swiftVersion < 5))
			{
				Debug.Log("[AVProVideo] setting SWIFT_VERSION to 5.0 for the UnityFramework target");
				project.SetBuildProperty(unityFrameworkTargetGuid, "SWIFT_VERSION", "5.0");
			}

			// Done
			project.WriteToFile(xcodeProjectPath);
			Debug.Log("[AVProVideo] Finished modifying Xcode project");
		}
	}
}

#endif
