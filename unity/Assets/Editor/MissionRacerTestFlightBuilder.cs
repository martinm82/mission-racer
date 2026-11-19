#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MissionRacer.EditorTools
{
    public static class MissionRacerTestFlightBuilder
    {
        public const string BUNDLE_ID = "com.migasiew.missionracer3d";
        public const string PRODUCT_NAME = "Mission Racer 3D";
        public const string COMPANY_NAME = "migasiew";
        public const string VERSION = "1.0.0";
        public const string BUILD_NUMBER = "1";
        public const string MIN_IOS_VERSION = "16.0";

        [MenuItem("Mission Racer 3D/🍎 Configure & Export iOS Project for TestFlight", false, 3)]
        public static void ConfigureAndExportIosForTestFlight()
        {
            ConfigureIosPlayerSettings();

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            string standaloneXcodeProj = Path.Combine(projectRoot, "ios", "MissionRacer3D.xcodeproj");
            string deployScript = Path.Combine(projectRoot, "scripts", "deploy_testflight.sh");

            bool iosModuleInstalled = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS);

            if (iosModuleInstalled)
            {
                string exportPath = Path.Combine(projectRoot, "build", "unity-ios-xcode");
                Directory.CreateDirectory(exportPath);

                string[] scenes = GetEnabledScenes();
                BuildPlayerOptions opts = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = exportPath,
                    target = BuildTarget.iOS,
                    options = BuildOptions.None
                };

                BuildReport report = BuildPipeline.BuildPlayer(opts);
                if (report.summary.result == BuildResult.Succeeded)
                {
                    UnityEngine.Debug.Log($"[MissionRacerTestFlight] Unity 6 iOS Xcode project exported to: {exportPath}");
                    if (!Application.isBatchMode)
                    {
                        EditorUtility.RevealInFinder(exportPath);
                    }
                }
                else
                {
                    UnityEngine.Debug.LogError($"[MissionRacerTestFlight] iOS export finished with status: {report.summary.result}");
                }
            }
            else
            {
                UnityEngine.Debug.Log(
                    $"[MissionRacerTestFlight] Configured iOS PlayerSettings ({BUNDLE_ID}, v{VERSION} ({BUILD_NUMBER}), IL2CPP, ARM64, iOS {MIN_IOS_VERSION}+). " +
                    $"Using standalone native iOS Xcode project at: {standaloneXcodeProj}");

                if (!Application.isBatchMode)
                {
                    int choice = EditorUtility.DisplayDialogComplex(
                        "Apple TestFlight iOS Pipeline Ready",
                        $"Unity 6 iOS PlayerSettings are configured!\n\n" +
                        $"• Bundle ID: {BUNDLE_ID}\n" +
                        $"• Version: {VERSION} (Build {BUILD_NUMBER})\n" +
                        $"• Target: iOS {MIN_IOS_VERSION}+ (Universal iPhone & iPad)\n\n" +
                        $"Note: The optional Unity Hub 'iOS Build Support' module is not installed in /Applications/Unity/Hub/Editor/6000.6.8f1/PlaybackEngines/iOSSupport, " +
                        $"so Mission Racer 3D includes a complete, standalone, ready-to-archive Native iOS Xcode project at:\n" +
                        $"{standaloneXcodeProj}",
                        "Archive & Upload to TestFlight",
                        "Open in Xcode 16",
                        "Cancel");

                    if (choice == 0 && File.Exists(deployScript))
                    {
                        RunDeployScriptInTerminal(deployScript);
                    }
                    else if (choice == 1 && Directory.Exists(standaloneXcodeProj))
                    {
                        Process.Start("open", $"\"{standaloneXcodeProj}\"");
                    }
                }
            }
        }

        [MenuItem("Mission Racer 3D/🚀 1-Click Archive & Upload to Apple TestFlight", false, 4)]
        public static void RunOneClickTestFlightDeploy()
        {
            ConfigureIosPlayerSettings();
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            string deployScript = Path.Combine(projectRoot, "scripts", "deploy_testflight.sh");
            if (File.Exists(deployScript))
            {
                RunDeployScriptInTerminal(deployScript);
            }
            else
            {
                UnityEngine.Debug.LogError($"[MissionRacerTestFlight] Deploy script not found at: {deployScript}");
            }
        }

        public static void ConfigureIosPlayerSettings()
        {
            PlayerSettings.companyName = COMPANY_NAME;
            PlayerSettings.productName = PRODUCT_NAME;
            PlayerSettings.bundleVersion = VERSION;

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BUNDLE_ID);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(NamedBuildTarget.iOS, 1); // ARM64

            PlayerSettings.iOS.buildNumber = BUILD_NUMBER;
            PlayerSettings.iOS.targetOSVersionString = MIN_IOS_VERSION;
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.iOS.requiresFullScreen = true;

            string teamId = Environment.GetEnvironmentVariable("APPLE_TEAM_ID");
            if (!string.IsNullOrEmpty(teamId))
            {
                PlayerSettings.iOS.appleDeveloperTeamID = teamId;
            }

            PlayerSettings.statusBarHidden = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log($"[MissionRacerTestFlight] Configured iOS PlayerSettings for {BUNDLE_ID} v{VERSION} ({BUILD_NUMBER}).");
        }

        private static string[] GetEnabledScenes()
        {
            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            if (buildScenes != null && buildScenes.Length > 0)
            {
                string[] paths = new string[buildScenes.Length];
                for (int i = 0; i < buildScenes.Length; i++)
                {
                    paths[i] = buildScenes[i].path;
                }
                return paths;
            }
            return new string[] { "Assets/Scenes/MissionRacer_MonacoGP_3D.unity" };
        }

        private static void RunDeployScriptInTerminal(string scriptPath)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "/usr/bin/open",
                    Arguments = $"-a Terminal \"{scriptPath}\"",
                    UseShellExecute = false
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[MissionRacerTestFlight] Failed to launch deploy script: {ex.Message}");
            }
        }
    }
}
#endif
