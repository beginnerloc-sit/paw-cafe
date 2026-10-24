using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PawCollector.EditorTools
{
    /// <summary>One-click App Store setup + Xcode project export.</summary>
    public static class BuildIOS
    {
        public const string BundleId = "com.kenyon.pawcafe";
        public const string Version = "1.0.0";
        public const string OutDir = "Builds/iOS";

        [UnityEditor.MenuItem("Paw Collector/App Store/Apply iOS Player Settings")]
        public static void ApplySettings()
        {
            PlayerSettings.companyName = "Kenyon AI Innovation";
            PlayerSettings.productName = "Paw Café";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            PlayerSettings.bundleVersion = Version;
            if (string.IsNullOrEmpty(PlayerSettings.iOS.buildNumber) || PlayerSettings.iOS.buildNumber == "0") PlayerSettings.iOS.buildNumber = "1";
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.iOS.hideHomeButton = false;
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.statusBarHidden = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(NamedBuildTarget.iOS, 1);   // ARM64
            PlayerSettings.SplashScreen.backgroundColor = new Color(1f, 0.91f, 0.84f);
            PlayerSettings.SplashScreen.showUnityLogo = false;
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/PawCollector/Art/AppIcon/AppIcon_1024.png");
            if (icon) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            AssetDatabase.SaveAssets();
            Debug.Log($"[BuildIOS] settings applied: {BundleId} v{Version} ({PlayerSettings.iOS.buildNumber}), icon {(icon ? "ok" : "MISSING")}");
        }

        [UnityEditor.MenuItem("Paw Collector/App Store/Build Xcode Project")]
        public static void Build()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS)) { Debug.LogError("[BuildIOS] iOS Build Support module is not installed (Unity Hub → Installs → Add modules)."); return; }
            ApplySettings();
            Directory.CreateDirectory(OutDir);
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/PawCollector/Scenes/Cafe.unity" },
                locationPathName = OutDir, target = BuildTarget.iOS, targetGroup = BuildTargetGroup.iOS, options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[BuildIOS] {report.summary.result}  errors {report.summary.totalErrors}  size {report.summary.totalSize / (1024 * 1024)} MB  → {Path.GetFullPath(OutDir)}");
        }

        [UnityEditor.MenuItem("Paw Collector/App Store/Bump Build Number")]
        public static void Bump()
        {
            int.TryParse(PlayerSettings.iOS.buildNumber, out var n); PlayerSettings.iOS.buildNumber = (n + 1).ToString();
            Debug.Log("[BuildIOS] build number " + PlayerSettings.iOS.buildNumber);
        }
    }
}
