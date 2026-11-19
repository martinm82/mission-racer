using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MissionRacer.Editor
{
    /// <summary>
    /// Configures Unity 6 (6000.x) for Android Tablet & Phone APK / AAB builds.
    /// </summary>
    public static class MissionRacerAndroidBuilder
    {
        private const string BundleId = "com.migasiew.missionracer3d";
        private const string ProductName = "Mission Racer 3D";

        [MenuItem("Mission Racer 3D/🤖 Configure & Build Android Tablet APK", false, 30)]
        public static void BuildAndroidTabletApk()
        {
            ConfigureAndroidPlayerSettings();

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            string outputApk = Path.Combine(projectRoot, "build", "MissionRacer3D-Android.apk");
            Directory.CreateDirectory(Path.GetDirectoryName(outputApk));

            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                EditorUtility.DisplayDialog(
                    "Android Build Support",
                    "Android Player Settings have been configured for tablets and phones (ARM64, IL2CPP, Vulkan/OpenGLES3).\n\n" +
                    "To export a native Unity APK, install the 'Android Build Support' module in Unity Hub for Unity 6000.6.8f1, " +
                    "or install the instant Wi-Fi WebAPK on your Android Tablet via Chrome at http://<Mac-IP>:3000.",
                    "OK"
                );
                return;
            }

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/MonacoGrandPrix.unity" },
                locationPathName = outputApk,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[MissionRacerAndroidBuilder] Android APK built at: {outputApk}");
                EditorUtility.RevealInFinder(outputApk);
            }
        }

        public static void ConfigureAndroidPlayerSettings()
        {
            PlayerSettings.companyName = "Migasiew Family Racing";
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;

            AssetDatabase.SaveAssets();
            Debug.Log("[MissionRacerAndroidBuilder] Configured Unity 6 Android Tablet & Phone settings.");
        }
    }
}
