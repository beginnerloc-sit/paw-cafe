using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace PawCollector.EditorTools
{
    /// <summary>One-click, reproducible assembly of the whole game from the generated art.</summary>
    public static class GameBuilder
    {
        const string Root = "Assets/PawCollector";
        static string[] Breeds => GameData.Cats.Select(c => c.Id).ToArray();
        static readonly HashSet<string> Looping = new() { "Idle_Sit", "Idle_Stand", "Walk", "Sleep", "Eat", "Sniff", "Bat" };

        [MenuItem("Paw Collector/Build Everything")]
        public static void BuildAll()
        {
            MoveRuntimeAssets();
            var mats = Materials();
            var ids = Breeds;
            for (int i = 0; i < ids.Length; i++) { BuildCat(ids[i], mats); BuildCatIcon(ids[i]); }
            BuildItems(mats);
            BuildScene(mats);
            PlayerSetup();
            AssetDatabase.SaveAssets();
            Debug.Log("[PawCollector] Build Everything complete");
        }

        static void Folder(string path)
        {
            var parts = path.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        static void MoveRuntimeAssets()
        {
            Folder(Root + "/Resources/Icons"); Folder(Root + "/Resources/Audio"); Folder(Root + "/Resources/Cats");
            Folder(Root + "/Resources/Items"); Folder(Root + "/Resources/Fx"); Folder(Root + "/Animation"); Folder(Root + "/UI");
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/UI/Icons" }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                AssetDatabase.MoveAsset(p, Root + "/Resources/Icons/" + Path.GetFileName(p));
            }
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Root + "/Audio" }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                AssetDatabase.MoveAsset(p, Root + "/Resources/Audio/" + Path.GetFileName(p));
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Resources/Icons" }))
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                ti.textureType = TextureImporterType.Default; ti.alphaIsTransparency = true; ti.mipmapEnabled = false;
                ti.npotScale = TextureImporterNPOTScale.None; ti.textureCompression = TextureImporterCompression.CompressedHQ; ti.SaveAndReimport();
            }
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Root + "/Resources/Audio" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ai = (AudioImporter)AssetImporter.GetAtPath(path);
                var s = ai.defaultSampleSettings;
                bool longClip = path.Contains("music") || path.Contains("amb_");
                s.loadType = longClip ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = longClip ? 0.6f : 0.8f;
                ai.defaultSampleSettings = s; ai.forceToMono = !longClip; ai.SaveAndReimport();
            }
        }

        static Material Mat(string path, Shader sh)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            m.shader = sh; return m;
        }

        static Dictionary<string, Material> Materials()
        {
            var sh = Shader.Find("PawCollector/StylizedFur");
            var d = new Dictionary<string, Material>();
            var fur = d["fur"] = Mat(Root + "/Materials/CatFur.mat", sh);
            fur.SetFloat("_Wrap", 0.45f); fur.SetFloat("_RimStrength", 0.45f); fur.SetFloat("_Smoothness", 0.25f); fur.SetFloat("_SpecStrength", 0.1f);
            var gl = d["gloss"] = Mat(Root + "/Materials/CatGloss.mat", sh);
            gl.SetFloat("_Wrap", 0.2f); gl.SetFloat("_RimStrength", 0.12f); gl.SetFloat("_Smoothness", 0.93f); gl.SetFloat("_SpecStrength", 2.2f);
            gl.SetColor("_ShadowTint", new Color(0.55f, 0.5f, 0.5f));
            var prop = d["prop"] = Mat(Root + "/Materials/Prop.mat", sh);
            prop.SetFloat("_Wrap", 0.4f); prop.SetFloat("_RimStrength", 0.18f); prop.SetFloat("_Smoothness", 0.2f); prop.SetFloat("_SpecStrength", 0.05f);
            prop.SetColor("_ShadowTint", new Color(0.72f, 0.66f, 0.74f));
            var glow = d["glow"] = Mat(Root + "/Materials/Glow.mat", sh);
            glow.SetColor("_EmissionColor", new Color(1.6f, 1.25f, 0.7f)); glow.SetFloat("_RimStrength", 0f);
            var water = d["water"] = Mat(Root + "/Materials/Water.mat", sh);
            water.SetFloat("_Smoothness", 0.95f); water.SetFloat("_SpecStrength", 3f); water.SetFloat("_RimStrength", 0.6f);
            water.SetColor("_RimColor", new Color(0.85f, 0.95f, 1f));
            var toonSh = Shader.Find("PawCollector/ToonCat");
            var toon = d["toon"] = Mat(Root + "/Materials/CatToon.mat", toonSh);
            toon.SetColor("_ShadowTint", new Color(0.78f, 0.76f, 0.88f)); toon.SetFloat("_RampMin", 0.38f); toon.SetFloat("_RampMax", 0.6f); toon.SetFloat("_AmbientStrength", 0.12f);
            var face = d["face"] = Mat(Root + "/Materials/CatFace.mat", toonSh); face.SetFloat("_Unlit", 1f);
            var fxSh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var fx = d["fx"] = Mat(Root + "/Resources/Fx/FxParticle.mat", fxSh);
            fx.SetFloat("_Surface", 1); fx.SetFloat("_Blend", 0);
            fx.SetOverrideTag("RenderType", "Transparent"); fx.renderQueue = 3000;
            fx.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); fx.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha); fx.SetFloat("_ZWrite", 0);
            fx.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            foreach (var m in d.Values) EditorUtility.SetDirty(m);
            return d;
        }

        // ------------------------------------------------------------------ cats
        static void BuildCat(string breed, Dictionary<string, Material> mats)
        {
            string fbx = $"{Root}/Models/Cats/{breed}_Rigged.fbx";
            var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
            if (imp == null) { Debug.LogError("Missing " + fbx); return; }
            imp.globalScale = 1f;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = true;
            imp.SaveAndReimport();
            var clips = imp.defaultClipAnimations;
            foreach (var c in clips)
            {
                var n = c.takeName.Contains("|") ? c.takeName.Substring(c.takeName.LastIndexOf('|') + 1) : c.takeName;
                c.name = n; c.loopTime = Looping.Contains(n); c.lockRootRotation = true; c.lockRootHeightY = true; c.lockRootPositionXZ = true;
                c.keepOriginalPositionY = true; c.keepOriginalOrientation = true; c.keepOriginalPositionXZ = true;
            }
            imp.clipAnimations = clips;
            imp.SaveAndReimport();

            var clipAssets = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToList();
            string ctrlPath = $"{Root}/Animation/Cat_{breed}.controller";
            AssetDatabase.DeleteAsset(ctrlPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            var sm = ctrl.layers[0].stateMachine;
            foreach (var c in clipAssets)
            {
                var st = sm.AddState(c.name); st.motion = c;
                if (c.name == "Idle_Stand") sm.defaultState = st;
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var rootGo = new GameObject("Cat_" + breed);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.transform.SetParent(rootGo.transform, false);
            var anim = inst.GetComponent<Animator>() ?? inst.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl; anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                if (r.sharedMaterials.Length >= 2) r.sharedMaterials = new[] { mats["toon"], mats["face"] };   // new toon cats: body + face
                else { bool g = r.name.Contains("Eye") || r.name.Contains("Nose"); r.sharedMaterial = g ? mats["gloss"] : mats["fur"]; }
                r.shadowCastingMode = r.name.Contains("Whisker") ? ShadowCastingMode.Off : ShadowCastingMode.On;
                if (r is SkinnedMeshRenderer smr) { smr.updateWhenOffscreen = true; smr.quality = SkinQuality.Bone2; }
            }
            var col = rootGo.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 0.45f, -0.05f); col.radius = 0.32f; col.height = 1.0f; col.direction = 1;
            inst.AddComponent<CatFace>();
            var def = GameData.Cat(breed); if (def != null) inst.transform.localScale = Vector3.one * def.Scale;
            rootGo.AddComponent<CatVisitor>();
            PrefabUtility.SaveAsPrefabAsset(rootGo, $"{Root}/Resources/Cats/Cat_{breed}.prefab");
            Object.DestroyImmediate(rootGo);
            Debug.Log($"[PawCollector] {breed}: {clipAssets.Count} clips ({string.Join(", ", clipAssets.Select(c => c.name))})");
        }


        // ------------------------------------------------------------------ portrait icons (rendered from the real model)
        static void BuildCatIcon(string id)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Resources/Cats/Cat_{id}.prefab");
            if (!prefab) return;
            var go = (GameObject)Object.Instantiate(prefab);
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>()) mb.enabled = false;
            var stage = new Vector3(500, 0, 500);
            go.transform.position = stage; go.transform.rotation = Quaternion.Euler(0, 200, 0);
            var clip = AssetDatabase.LoadAllAssetsAtPath($"{Root}/Models/Cats/{id}_Rigged.fbx").OfType<AnimationClip>().FirstOrDefault(c => c.name == "Idle_Sit");
            var animGo = go.GetComponentInChildren<Animator>().gameObject;
            if (clip) clip.SampleAnimation(animGo, 0.1f);
            Bounds b = new Bounds(stage, Vector3.zero); bool init = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) { if (!init) { b = r.bounds; init = true; } else b.Encapsulate(r.bounds); }
            var camGo = new GameObject("IconCam"); var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0); cam.fieldOfView = 22;
            var head = b.center + Vector3.up * b.extents.y * 0.2f;
            cam.transform.position = head + new Vector3(0.0f, 0.25f, -1f).normalized * (b.extents.magnitude * 2.55f);
            cam.transform.LookAt(head);
            var light = new GameObject("IconLight").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.3f;
            light.color = new Color(1f, 0.95f, 0.88f); light.transform.rotation = Quaternion.Euler(35, 160, 0);
            var rt = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt; var tex = new Texture2D(256, 256, TextureFormat.RGBA32, false); tex.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); tex.Apply();
            string path = $"{Root}/Resources/Icons/cat_{id}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = null; Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo); Object.DestroyImmediate(light.gameObject); Object.DestroyImmediate(go); Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti) { ti.textureType = TextureImporterType.Default; ti.alphaIsTransparency = true; ti.mipmapEnabled = false; ti.SaveAndReimport(); }
        }

        // ------------------------------------------------------------------ items
        static void BuildItems(Dictionary<string, Material> mats)
        {
            string fbx = Root + "/Models/Items.fbx";
            var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
            imp.materialImportMode = ModelImporterMaterialImportMode.None; imp.importAnimation = false; imp.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            foreach (Transform child in model.transform)
            {
                if (!child.name.StartsWith("Item_")) continue;
                var go = Object.Instantiate(child.gameObject); go.name = child.name;
                go.transform.position = Vector3.zero; go.transform.rotation = child.rotation;
                foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mats["prop"];
                var holder = new GameObject(child.name); go.transform.SetParent(holder.transform, true);
                PrefabUtility.SaveAsPrefabAsset(holder, $"{Root}/Resources/Items/{child.name}.prefab");
                Object.DestroyImmediate(holder);
            }
        }

        // ------------------------------------------------------------------ scene
        static void BuildScene(Dictionary<string, Material> mats)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            string gfbx = Root + "/Models/Garden.fbx";
            var gimp = (ModelImporter)AssetImporter.GetAtPath(gfbx);
            gimp.materialImportMode = ModelImporterMaterialImportMode.None; gimp.importAnimation = false; gimp.SaveAndReimport();
            var garden = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(gfbx));
            garden.name = "Garden";
            foreach (var r in garden.GetComponentsInChildren<MeshRenderer>())
            {
                r.sharedMaterial = r.name.Contains("Glow") ? mats["glow"] : r.name.Contains("PondWater") ? mats["water"] : mats["prop"];
                r.shadowCastingMode = r.name.Contains("Ground") || r.name.Contains("GrassTufts") || r.name.Contains("Path") ? ShadowCastingMode.Off : ShadowCastingMode.On;
            }
            foreach (var t in garden.GetComponentsInChildren<Transform>())
            {
                if (!t.name.StartsWith("Spot_")) continue;
                var s = t.gameObject.AddComponent<Spot>();
                s.Index = int.Parse(t.name.Substring(5)) - 1;
                var mc = t.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = t.GetComponent<MeshFilter>().sharedMesh;
            }
            GameObjectUtility.SetStaticEditorFlags(garden, StaticEditorFlags.BatchingStatic);
            foreach (var t in garden.GetComponentsInChildren<Transform>())
                if (t.name.StartsWith("CatFlap") && t.name != "CatFlapFrame") GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);   // the flap swings

            // light + ambience
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.45f; sun.color = new Color(1f, 0.9f, 0.78f);
            sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.62f; sun.shadowBias = 0.04f; sun.shadowNormalBias = 0.3f;
            sun.transform.rotation = Quaternion.Euler(48f, 145f, 0f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.86f, 0.96f);
            RenderSettings.ambientEquatorColor = new Color(0.88f, 0.82f, 0.74f);
            RenderSettings.ambientGroundColor = new Color(0.46f, 0.52f, 0.36f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogColor = new Color(0.8f, 0.89f, 0.95f);
            RenderSettings.fogStartDistance = 26f; RenderSettings.fogEndDistance = 70f;

            // camera
            var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>(); cam.nearClipPlane = 0.1f; cam.farClipPlane = 80f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.76f, 0.87f, 0.94f);
            var urp = camGo.AddComponent<UniversalAdditionalCameraData>();
            urp.renderPostProcessing = true; urp.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            urp.antialiasingQuality = AntialiasingQuality.High;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CameraRig>();

            // post-processing
            var volGo = new GameObject("Global Volume"); var vol = volGo.AddComponent<Volume>(); vol.isGlobal = true;
            string profPath = Root + "/Materials/GardenPost.asset";
            AssetDatabase.DeleteAsset(profPath);
            var prof = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(prof, profPath);
            var tm = prof.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.Neutral);
            var bl = prof.Add<Bloom>(true); bl.intensity.Override(0.35f); bl.threshold.Override(1.05f); bl.scatter.Override(0.6f);
            var ca = prof.Add<ColorAdjustments>(true); ca.postExposure.Override(0f); ca.contrast.Override(10f); ca.saturation.Override(14f);
            var wb = prof.Add<WhiteBalance>(true); wb.temperature.Override(6f);
            var vg = prof.Add<Vignette>(true); vg.intensity.Override(0.18f); vg.smoothness.Override(0.5f);
            foreach (var c in prof.components) AssetDatabase.AddObjectToAsset(c, prof);
            EditorUtility.SetDirty(prof);
            vol.sharedProfile = prof;

            // game systems
            var game = new GameObject("Game");
            game.AddComponent<GameManager>(); game.AddComponent<GardenMap>(); game.AddComponent<VisitorSpawner>(); game.AddComponent<InputController>(); game.AddComponent<AudioManager>();
            game.AddComponent<PhotoMode>();
            game.AddComponent<Lure>().Mat = mats["prop"];
            game.AddComponent<Butterflies>().Mat = mats["prop"];

            // UI
            string psPath = Root + "/UI/PanelSettings.asset";
            var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(psPath);
            if (!ps) { ps = ScriptableObject.CreateInstance<PanelSettings>(); AssetDatabase.CreateAsset(ps, psPath); }
            string tssPath = Root + "/UI/PawTheme.tss";
            if (!File.Exists(tssPath)) { File.WriteAllText(tssPath, "@import url(\"unity-theme://default\");\n"); AssetDatabase.ImportAsset(tssPath); }
            ps.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(tssPath);
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize; ps.referenceResolution = new Vector2Int(1080, 2340);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight; ps.match = 0f;
            EditorUtility.SetDirty(ps);
            var ui = new GameObject("UI"); var doc = ui.AddComponent<UIDocument>(); doc.panelSettings = ps;
            ui.AddComponent<UIController>();
            var es = new GameObject("EventSystem"); es.AddComponent<UnityEngine.EventSystems.EventSystem>(); es.AddComponent<InputSystemUIInputModule>();

            EditorSceneManager.SaveScene(scene, Root + "/Scenes/Garden.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Root + "/Scenes/Garden.unity", true) };
        }

        static void PlayerSetup()
        {
            PlayerSettings.productName = "Paw Collector";
            PlayerSettings.companyName = "Kenyon AI Innovation";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "com.kenyon.pawcollector");
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.kenyon.pawcollector");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true; PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false; PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.bundleVersion = "0.1.0"; PlayerSettings.runInBackground = true;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { "Assets" }))
            {
                var a = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                a.shadowDistance = 24f; a.shadowCascadeCount = 2; a.supportsHDR = true; a.msaaSampleCount = 4;
                EditorUtility.SetDirty(a);
            }
        }
    }
}
