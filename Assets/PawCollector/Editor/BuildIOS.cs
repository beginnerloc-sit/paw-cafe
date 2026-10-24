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
            // Landscape game: either way round, never portrait.
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;     // turn landscape on before portrait off
            PlayerSettings.allowedAutorotateToLandscapeRight = true;    // (Unity re-enables portrait if all are off for a moment)
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(NamedBuildTarget.iOS, 1);   // ARM64
            // No Unity splash: iOS shows our launch screen, then the game's own animated title screen takes over seamlessly.
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.backgroundColor = new Color(1f, 0.84f, 0.886f);
            ApplyLaunchScreen();
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/PawCollector/Art/AppIcon/AppIcon_1024.png");
            if (icon) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            AssetDatabase.SaveAssets();
            Debug.Log($"[BuildIOS] settings applied: {BundleId} v{Version} ({PlayerSettings.iOS.buildNumber}), icon {(icon ? "ok" : "MISSING")}");
        }

        /// <summary>iOS launch screen = our art (logo + cats) scaled to fill, on the matching pink background.</summary>
        static void ApplyLaunchScreen()
        {
            const string path = "Assets/PawCollector/Art/Launch/launch_portrait.png", pathL = "Assets/PawCollector/Art/Launch/launch_landscape.png";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path); var texL = AssetDatabase.LoadAssetAtPath<Texture2D>(pathL);
            if (!tex || !texL) { Debug.LogWarning("[BuildIOS] launch image missing"); return; }
            PlayerSettings.iOS.SetiPhoneLaunchScreenType(iOSLaunchScreenType.ImageAndBackgroundRelative);
            PlayerSettings.iOS.SetLaunchScreenImage(tex, iOSLaunchScreenImageType.iPhonePortraitImage);
            PlayerSettings.iOS.SetLaunchScreenImage(texL, iOSLaunchScreenImageType.iPhoneLandscapeImage);
            var ps = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var bgc = ps.FindProperty("iOSLaunchScreenBackgroundColor"); if (bgc != null) bgc.colorValue = new Color(1f, 0.84f, 0.886f);
            var fill = ps.FindProperty("iOSLaunchScreenFillPct"); if (fill != null) fill.floatValue = 100f;
            var size = ps.FindProperty("iOSLaunchScreenSize"); if (size != null) size.floatValue = 100f;
            ps.ApplyModifiedPropertiesWithoutUndo();
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
