using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using PawCollector.Cafe;

namespace PawCollector.EditorTools
{
    /// <summary>One-click assembly of Paw Café from the Blender exports.</summary>
    public static class CafeBuilder
    {
        const string Root = "Assets/PawCollector";
        const string CafeFbx = Root + "/Models/Cafe/Cafe.fbx", LayoutFbx = Root + "/Models/Cafe/CafeLayout.fbx";
        static readonly HashSet<string> CatLoop = new() { "Idle_Sit", "Idle_Stand", "Walk", "Sleep", "Eat", "Sniff", "Bat" };
        static readonly HashSet<string> PeopleLoop = new() { "Idle", "Walk", "Sit_Idle" };
        static Vector3 axis = Vector3.one;   // Blender -> Unity sign per axis (x, -, z), filled from the layout

        [UnityEditor.MenuItem("Paw Collector/Build Café")]
        public static void Build()
        {
            Folder(Root + "/Resources/Furniture"); Folder(Root + "/Resources/Props"); Folder(Root + "/Resources/CafeCats"); Folder(Root + "/Resources/Customers");
            Folder(Root + "/Animation"); Folder(Root + "/Scenes"); Folder(Root + "/Resources/Icons");
            var mats = Materials();
            Import(CafeFbx); Import(LayoutFbx);
            FindAxis();
            BuildFurniture(mats);
            foreach (var c in GameData.Cats) BuildCat(c, mats);
            foreach (var v in CafeData.CustomerVariants) BuildCustomer(v, mats);
            BuildScene(mats);
            AssetDatabase.SaveAssets();
            Debug.Log("[PawCafe] Build Café complete");
        }

        static void Folder(string path) { var parts = path.Split('/'); string cur = parts[0]; for (int i = 1; i < parts.Length; i++) { var n = cur + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(n)) AssetDatabase.CreateFolder(cur, parts[i]); cur = n; } }
        static Material Mat(string path, Shader sh) { var m = AssetDatabase.LoadAssetAtPath<Material>(path); if (!m) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); } m.shader = sh; EditorUtility.SetDirty(m); return m; }

        static Dictionary<string, Material> Materials()
        {
            var toon = Shader.Find("PawCollector/ToonCat");
            var d = new Dictionary<string, Material>();
            var prop = d["prop"] = Mat(Root + "/Materials/CafeProp.mat", toon);
            prop.SetColor("_ShadowTint", new Color(0.82f, 0.79f, 0.9f)); prop.SetFloat("_RampMin", 0.3f); prop.SetFloat("_RampMax", 0.62f); prop.SetFloat("_AmbientStrength", 0.1f); prop.SetFloat("_LightInfluence", 0.25f);
            var locked = d["locked"] = Mat(Root + "/Materials/CafeLocked.mat", toon);
            locked.SetColor("_Tint", new Color(0.62f, 0.6f, 0.66f)); locked.SetColor("_ShadowTint", new Color(0.8f, 0.78f, 0.88f));
            var cat = d["toon"] = Mat(Root + "/Materials/CatToon.mat", toon); cat.SetColor("_ShadowTint", new Color(0.78f, 0.76f, 0.88f)); cat.SetFloat("_RampMin", 0.38f); cat.SetFloat("_RampMax", 0.6f); cat.SetFloat("_AmbientStrength", 0.12f);
            var face = d["face"] = Mat(Root + "/Materials/CatFace.mat", toon); face.SetFloat("_Unlit", 1f);
            var sub = d["sub"] = Mat(Root + "/Materials/Subfloor.mat", Shader.Find("Universal Render Pipeline/Unlit")); sub.SetColor("_BaseColor", new Color(0.36f, 0.24f, 0.17f));
            var glass = d["glass"] = Mat(Root + "/Materials/Glass.mat", Shader.Find("Universal Render Pipeline/Unlit"));
            glass.SetFloat("_Surface", 1f); glass.SetFloat("_Blend", 0f); glass.SetFloat("_ZWrite", 0f); glass.SetFloat("_Cull", 0f);
            glass.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); glass.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); glass.SetOverrideTag("RenderType", "Transparent"); glass.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            glass.SetColor("_BaseColor", new Color(0.82f, 0.93f, 1f, 0.16f));
            var pic = d["pic"] = Mat(Root + "/Materials/PhotoPic.mat", Shader.Find("Universal Render Pipeline/Unlit"));
            return d;
        }

        static void Import(string path)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(path); if (imp == null) { Debug.LogError("missing " + path); return; }
            imp.materialImportMode = ModelImporterMaterialImportMode.None; imp.importAnimation = false; imp.animationType = ModelImporterAnimationType.None;
            imp.SaveAndReimport();
        }

        static Transform Child(GameObject model, string name) => model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        // Blender layout knows the counter sits at (0.85, 1.62) -> work out the axis signs of the FBX conversion
        static void FindAxis()
        {
            var lay = AssetDatabase.LoadAssetAtPath<GameObject>(LayoutFbx);
            var counter = lay.GetComponentsInChildren<Transform>().First(t => t.name.StartsWith("Furn_Counter"));
            axis = new Vector3(Mathf.Sign(counter.position.x / 0.85f), 1f, Mathf.Sign(counter.position.z / 1.62f));
            Debug.Log($"[PawCafe] axis map {axis} counter at {counter.position}");
        }
        const int DoorArea = 3;   // NavMesh area used only by the shop-front doorway
        static Vector3 B2U(float bx, float by, float bz) => new Vector3(bx * CafeData.S * axis.x, bz * CafeData.S, by * CafeData.S * axis.z);

        // ------------------------------------------------------------ furniture & props
        static void BuildFurniture(Dictionary<string, Material> mats)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CafeFbx);
            foreach (var def in CafeData.Furniture)
            {
                var src = Child(model, def.Model); if (!src) { Debug.LogWarning("no model " + def.Model); continue; }
                var root = new GameObject(def.Id);
                var m = Object.Instantiate(src.gameObject, root.transform); m.name = "Model"; m.transform.localPosition = Vector3.zero; m.transform.localRotation = src.localRotation;
                foreach (var r in m.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = mats["prop"]; r.shadowCastingMode = def.Id == "Rug" ? ShadowCastingMode.Off : ShadowCastingMode.On; }
                if (def.Id == "PhotoFrame")
                {
                    var pic = Object.Instantiate(Child(model, "Furn_PhotoFrame_Pic").gameObject, root.transform); pic.name = "Pic"; pic.transform.localPosition = Vector3.zero;
                    pic.GetComponent<Renderer>().sharedMaterial = mats["pic"]; pic.GetComponent<Renderer>().enabled = false;
                }
                var b = Bounds(root);
                var col = root.AddComponent<BoxCollider>(); col.center = b.center - root.transform.position; col.size = Vector3.Max(b.size, new Vector3(0.2f, 0.2f, 0.2f));
                if (def.Carve && !def.WallMounted)
                {
                    var ob = root.AddComponent<NavMeshObstacle>(); ob.carving = true; ob.shape = NavMeshObstacleShape.Box;
                    ob.center = new Vector3(col.center.x, 0.5f, col.center.z); ob.size = new Vector3(b.size.x, 1f, b.size.z);
                }
                root.AddComponent<FurnitureInstance>();
                PrefabUtility.SaveAsPrefabAsset(root, $"{Root}/Resources/Furniture/{def.Id}.prefab");
                RenderIcon(root, "furn_" + def.Id);
                Object.DestroyImmediate(root);
            }
            foreach (var n in new[] { "Prop_Cup", "Prop_Cake", "Prop_Cookies", "Prop_Matcha" })
            {
                var src = Child(model, n); if (!src) continue;
                var root = new GameObject(n); var m = Object.Instantiate(src.gameObject, root.transform); m.transform.localPosition = Vector3.zero;
                foreach (var r in m.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mats["prop"];
                PrefabUtility.SaveAsPrefabAsset(root, $"{Root}/Resources/Props/{n}.prefab"); RenderIcon(root, "furn_" + n); Object.DestroyImmediate(root);
            }
        }
        static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
        }
        static void RenderIcon(GameObject go, string name)
        {
            var stage = new Vector3(600, 0, 600); var inst = Object.Instantiate(go, stage, Quaternion.Euler(0, 200, 0));
            var b = Bounds(inst);
            var camGo = new GameObject("IconCam"); var cam = camGo.AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0); cam.fieldOfView = 24;
            cam.transform.position = b.center + new Vector3(0, 0.55f, -1f).normalized * (b.extents.magnitude * 3.1f); cam.transform.LookAt(b.center);
            var light = new GameObject("L").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.3f; light.transform.rotation = Quaternion.Euler(40, 150, 0);
            var rt = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt; var tex = new Texture2D(256, 256, TextureFormat.RGBA32, false); tex.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); tex.Apply();
            string path = $"{Root}/Resources/Icons/{name}.png"; File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(camGo); Object.DestroyImmediate(light.gameObject); Object.DestroyImmediate(inst);
            AssetDatabase.ImportAsset(path); var ti = (TextureImporter)AssetImporter.GetAtPath(path); if (ti) { ti.alphaIsTransparency = true; ti.mipmapEnabled = false; ti.SaveAndReimport(); }
        }

        // ------------------------------------------------------------ rigged characters
        static AnimatorController Rigged(string fbx, HashSet<string> loop, string ctrlPath, string defaultState)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(fbx); if (imp == null) return null;
            imp.globalScale = 1f; imp.materialImportMode = ModelImporterMaterialImportMode.None; imp.animationType = ModelImporterAnimationType.Generic;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; imp.importAnimation = true; imp.importBlendShapes = true; imp.SaveAndReimport();
            var clips = imp.defaultClipAnimations;
            foreach (var c in clips)
            {
                var n = c.takeName.Contains("|") ? c.takeName.Substring(c.takeName.LastIndexOf('|') + 1) : c.takeName;
                c.name = n; c.loopTime = loop.Contains(n); c.lockRootRotation = c.lockRootHeightY = c.lockRootPositionXZ = true; c.keepOriginalPositionY = c.keepOriginalOrientation = c.keepOriginalPositionXZ = true;
            }
            imp.clipAnimations = clips; imp.SaveAndReimport();
            var assets = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToList();
            AssetDatabase.DeleteAsset(ctrlPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath); var sm = ctrl.layers[0].stateMachine;
            foreach (var c in assets) { var st = sm.AddState(c.name); st.motion = c; if (c.name == defaultState) sm.defaultState = st; }
            return ctrl;
        }
        static GameObject Character(string fbx, AnimatorController ctrl, Dictionary<string, Material> mats, float scale)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var root = new GameObject(); var inst = (GameObject)PrefabUtility.InstantiatePrefab(model); inst.transform.SetParent(root.transform, false); inst.transform.localScale = Vector3.one * scale;
            var anim = inst.GetComponent<Animator>() ?? inst.AddComponent<Animator>(); anim.runtimeAnimatorController = ctrl; anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterials = r.sharedMaterials.Length >= 2 ? new[] { mats["toon"], mats["face"] } : new[] { mats["toon"] };
                if (r is SkinnedMeshRenderer smr) { smr.updateWhenOffscreen = false; smr.quality = SkinQuality.Bone2; }
            }
            inst.AddComponent<CatFace>();
            return root;
        }
        static void BuildCat(CatDef def, Dictionary<string, Material> mats)
        {
            string fbx = $"{Root}/Models/Cats/{def.Id}_Rigged.fbx";
            var ctrl = Rigged(fbx, CatLoop, $"{Root}/Animation/CafeCat_{def.Id}.controller", "Idle_Stand"); if (!ctrl) return;
            var root = Character(fbx, ctrl, mats, def.Scale); root.name = "CafeCat_" + def.Id;
            var ag = root.AddComponent<NavMeshAgent>(); ag.radius = 0.32f; ag.height = 1f; ag.baseOffset = 0f; ag.areaMask = ~(1 << DoorArea);   // cats stay inside: street + doorway sill are people-only
            var col = root.AddComponent<CapsuleCollider>(); col.center = new Vector3(0, 0.45f * def.Scale, 0); col.radius = 0.38f * def.Scale; col.height = 1f * def.Scale;
            root.AddComponent<CafeCat>();
            PrefabUtility.SaveAsPrefabAsset(root, $"{Root}/Resources/CafeCats/CafeCat_{def.Id}.prefab"); Object.DestroyImmediate(root);
        }
        static void BuildCustomer(string v, Dictionary<string, Material> mats)
        {
            string fbx = $"{Root}/Models/People/Customer_{v}.fbx";
            var ctrl = Rigged(fbx, PeopleLoop, $"{Root}/Animation/Customer_{v}.controller", "Idle"); if (!ctrl) return;
            var root = Character(fbx, ctrl, mats, 1f); root.name = "Customer_" + v;
            var ag = root.AddComponent<NavMeshAgent>(); ag.radius = 0.3f; ag.height = 1.8f;
            var col = root.AddComponent<CapsuleCollider>(); col.center = new Vector3(0, 0.9f, 0); col.radius = 0.35f; col.height = 1.8f;
            root.AddComponent<Customer>();
            PrefabUtility.SaveAsPrefabAsset(root, $"{Root}/Resources/Customers/Customer_{v}.prefab"); Object.DestroyImmediate(root);
        }

        // ------------------------------------------------------------ scene
        static void BuildScene(Dictionary<string, Material> mats)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CafeFbx);
            var nav = new GameObject("NavRoot");
            foreach (var r in CafeData.Rooms)
            {
                var src = Child(model, r.Model); var go = Object.Instantiate(src.gameObject, nav.transform); go.name = r.Model;
                go.transform.SetPositionAndRotation(src.position, src.rotation);
                foreach (var rr in go.GetComponentsInChildren<Renderer>())
                {
                    bool glassy = rr.name.StartsWith("Glass_");
                    rr.sharedMaterial = glassy ? mats["glass"] : mats["prop"];
                    rr.shadowCastingMode = glassy ? ShadowCastingMode.Off : ShadowCastingMode.TwoSided;
                }
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }
            var ext = Object.Instantiate(Child(model, "Exterior").gameObject); ext.name = "Exterior"; ext.transform.SetPositionAndRotation(Child(model, "Exterior").position, Child(model, "Exterior").rotation);
            foreach (var rr in ext.GetComponentsInChildren<Renderer>()) rr.sharedMaterial = mats["prop"];
            GameObjectUtility.SetStaticEditorFlags(ext, StaticEditorFlags.BatchingStatic);
            // static counter + tip jar from the layout
            var lay = AssetDatabase.LoadAssetAtPath<GameObject>(LayoutFbx);
            var layout = new List<PlacedFurn>(); Transform counterT = null, jarT = null;
            foreach (Transform t in lay.GetComponentsInChildren<Transform>())
            {
                if (t == lay.transform) continue;
                string baseName = t.name.Split('.')[0];
                if (baseName == "Furn_Counter") counterT = t;
                else if (baseName == "Furn_TipJar") jarT = t;
                else if (baseName.StartsWith("Furn_") && baseName != "Furn_PhotoFrame_Pic")
                {
                    var def = CafeData.Furniture.FirstOrDefault(f => f.Model == baseName); if (def == null) continue;
                    var srcRot = Child(model, def.Model).rotation;
                    float yaw = (t.rotation * Quaternion.Inverse(srcRot)).eulerAngles.y;
                    layout.Add(new PlacedFurn { id = def.Id, x = t.position.x, y = t.position.y, z = t.position.z, yaw = yaw, room = null });
                }
            }
            var counter = Object.Instantiate(Child(model, "Furn_Counter").gameObject); counter.name = "Counter";
            counter.transform.SetPositionAndRotation(counterT.position, counterT.rotation);
            foreach (var rr in counter.GetComponentsInChildren<Renderer>()) rr.sharedMaterial = mats["prop"];
            var cb = Bounds(counter); var cob = counter.AddComponent<NavMeshObstacle>(); cob.carving = true; cob.shape = NavMeshObstacleShape.Box; cob.center = counter.transform.InverseTransformPoint(cb.center); cob.size = counter.transform.InverseTransformVector(cb.size); cob.size = new Vector3(Mathf.Abs(cob.size.x), Mathf.Abs(cob.size.y), Mathf.Abs(cob.size.z));
            var jar = Object.Instantiate(Child(model, "Furn_TipJar").gameObject); jar.name = "TipJar"; jar.transform.SetPositionAndRotation(jarT.position, jarT.rotation);
            foreach (var rr in jar.GetComponentsInChildren<Renderer>()) rr.sharedMaterial = mats["prop"];
            var jc = jar.AddComponent<SphereCollider>(); jc.radius = 0.45f; jc.center = new Vector3(0, 0.15f, 0); jar.AddComponent<TipJar>();

            // room ids for the default layout
            Bounds RB(string m) => GameObject.Find(m).GetComponent<Renderer>().bounds;
            foreach (var p in layout) { foreach (var r in CafeData.Rooms) { var b = RB(r.Model); if (p.x > b.min.x && p.x < b.max.x && p.z > b.min.z && p.z < b.max.z) { p.room = r.Id; break; } } p.room ??= "counter"; }

            // subfloor so plank seams read as dark grooves instead of showing the background
            foreach (var r in CafeData.Rooms)
            {
                var fb = RB(r.Model); var sub = GameObject.CreatePrimitive(PrimitiveType.Cube); sub.name = "Subfloor_" + r.Id; Object.DestroyImmediate(sub.GetComponent<Collider>());
                sub.transform.position = new Vector3(fb.center.x, -0.09f, fb.center.z); sub.transform.localScale = new Vector3(Mathf.Min(fb.size.x, 8.6f), 0.1f, 8.6f);
                sub.GetComponent<Renderer>().sharedMaterial = mats["sub"];
            }
            // door, points, locks, strays
            // customers stroll in along the front pavement and step in through the open front of the café
            var door = new GameObject("Door").transform; door.position = B2U(-0.3f, -1.55f, 0);
            var doorOut = new GameObject("DoorOutside").transform; doorOut.position = B2U(-7.5f, -2.8f, 0);
            var doorOut2 = new GameObject("DoorOutside2").transform; doorOut2.position = B2U(10.5f, -2.8f, 0);
            var outside = GameObject.CreatePrimitive(PrimitiveType.Cube); outside.name = "Street"; outside.transform.position = B2U(1.5f, -2.75f, -0.03f); outside.transform.localScale = new Vector3(42f, 0.05f, 2.6f);
            outside.GetComponent<Renderer>().sharedMaterial = mats["locked"]; Object.DestroyImmediate(outside.GetComponent<Collider>()); outside.transform.SetParent(nav.transform, true); var om = outside.AddComponent<NavMeshModifier>(); om.overrideArea = true; om.area = DoorArea;   // street = people only
            // walkable threshold through the shop-front doorway (bridges café floor → pavement)
            var sill = GameObject.CreatePrimitive(PrimitiveType.Cube); sill.name = "DoorSill"; sill.transform.position = B2U(-0.3f, -2.15f, -0.012f); sill.transform.localScale = new Vector3(0.74f * CafeData.S, 0.05f, 0.5f * CafeData.S);
            sill.GetComponent<Renderer>().sharedMaterial = mats["locked"]; Object.DestroyImmediate(sill.GetComponent<Collider>()); sill.transform.SetParent(nav.transform, true); var sm = sill.AddComponent<NavMeshModifier>(); sm.overrideArea = true; sm.area = DoorArea;
            var block = new GameObject("Block_nook"); block.transform.position = B2U(2f, 0.5f, 0.5f);
            var bo = block.AddComponent<NavMeshObstacle>(); bo.carving = true; bo.shape = NavMeshObstacleShape.Box; bo.size = new Vector3(1.2f, 2f, 3.4f);
            var sign = new GameObject("Sign_nook"); sign.transform.position = B2U(3.5f, 0f, 1.4f); var sc = sign.AddComponent<BoxCollider>(); sc.size = new Vector3(4f, 3f, 4f); sign.AddComponent<RoomSign>().Room = "nook";
            foreach (var (rid, archX, cx) in new[] { ("reading", 5f, 6.5f), ("lounge", 8f, 9.5f), ("terrace", -2f, -4f) })
            {
                var b2 = new GameObject("Block_" + rid); b2.transform.position = B2U(archX, 0.5f, 0.5f);
                var o2 = b2.AddComponent<NavMeshObstacle>(); o2.carving = true; o2.shape = NavMeshObstacleShape.Box; o2.size = new Vector3(1.2f, 2f, 3.4f);
                var s2 = new GameObject("Sign_" + rid); s2.transform.position = B2U(cx, 0f, 1.4f); var c2 = s2.AddComponent<BoxCollider>(); c2.size = new Vector3(4f, 3f, 4f); s2.AddComponent<RoomSign>().Room = rid;
            }
            var ledge = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                var l = new GameObject("Ledge" + i).transform; l.position = B2U(2.9f + 1.2f * i, 2.26f, 0.55f);
                l.rotation = Quaternion.LookRotation(new Vector3(0, 0, -axis.z)); ledge[i] = l;   // face into the café (Blender -Y)
            }

            // navmesh from the room floors (the street is outside)
            // agents are small (customers r=0.3) — bake for that so the shop doorway is walkable
            var navSo = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0]);
            var agentSettings = navSo.FindProperty("m_Settings");
            if (agentSettings != null && agentSettings.arraySize > 0) { agentSettings.GetArrayElementAtIndex(0).FindPropertyRelative("agentRadius").floatValue = 0.3f; navSo.ApplyModifiedPropertiesWithoutUndo(); }
            var surf = nav.AddComponent<NavMeshSurface>(); surf.collectObjects = CollectObjects.Children; surf.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            surf.BuildNavMesh();
            outside.GetComponent<Renderer>().enabled = false; sill.GetComponent<Renderer>().enabled = false;   // walkable street for customers, drawn by the Exterior model
            string navPath = Root + "/Scenes/CafeNav.asset"; AssetDatabase.DeleteAsset(navPath); AssetDatabase.CreateAsset(surf.navMeshData, navPath);

            new GameObject("StreetLife").AddComponent<StreetLife>();   // passers-by + neighbourhood cats across the road

            // light
            var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.45f; sun.color = new Color(1f, 0.91f, 0.80f);
            sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.7f; sun.transform.rotation = Quaternion.Euler(55f, axis.z > 0 ? 160f : -20f, 0f);
            RenderSettings.ambientMode = AmbientMode.Trilight; RenderSettings.ambientSkyColor = new Color(0.78f, 0.82f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.80f, 0.74f, 0.68f); RenderSettings.ambientGroundColor = new Color(0.55f, 0.48f, 0.42f); RenderSettings.fog = false;

            // camera
            var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera"; var cam = camGo.AddComponent<Camera>(); cam.nearClipPlane = 0.2f; cam.farClipPlane = 80f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.8f, 0.89f, 0.96f);
            var urp = camGo.AddComponent<UniversalAdditionalCameraData>(); urp.renderPostProcessing = true; urp.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camGo.AddComponent<AudioListener>(); var cc = camGo.AddComponent<CafeCamera>(); cc.LookDir = new Vector3(0, 0, axis.z); camGo.AddComponent<CafeCutaway>();
            var rb = RB("Room_Counter"); var area = new Bounds(rb.center, new Vector3(rb.size.x, 0, rb.size.z) * 0.6f); cc.Area = area;
            camGo.AddComponent<CameraAreaFromRooms>();

            var vol = new GameObject("Global Volume").AddComponent<Volume>(); vol.isGlobal = true;
            string profPath = Root + "/Materials/CafePost.asset"; AssetDatabase.DeleteAsset(profPath);
            var prof = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(prof, profPath);
            var tm = prof.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.None); // Neutral washed out the pastel toon colours
            var bl = prof.Add<Bloom>(true); bl.intensity.Override(0.25f); bl.threshold.Override(1.1f);
            var ca = prof.Add<ColorAdjustments>(true); ca.saturation.Override(12f); ca.contrast.Override(14f); ca.postExposure.Override(0.05f);
            var vg = prof.Add<Vignette>(true); vg.intensity.Override(0.15f);
            foreach (var c in prof.components) AssetDatabase.AddObjectToAsset(c, prof);
            vol.sharedProfile = prof;

            // game systems
            var game = new GameObject("Game");
            var cm = game.AddComponent<CafeManager>(); cm.Door = door; cm.DoorOutside = doorOut; cm.DoorOutside2 = doorOut2; cm.TipJarPoint = jar.transform; cm.CounterPoint = counter.transform;
            game.AddComponent<DefaultLayout>().Items = layout;
            var refs = game.AddComponent<CafeRefs>(); refs.Prop = mats["prop"]; refs.Locked = mats["locked"]; refs.Glass = mats["glass"];
            game.AddComponent<CafeInput>(); game.AddComponent<CafePhoto>(); game.AddComponent<CafeDecorate>();
            game.AddComponent<StraySpawner>().Ledge = ledge; game.AddComponent<AudioManager>();

            // UI
            string psPath = Root + "/UI/CafePanel.asset";
            var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(psPath); if (!ps) { ps = ScriptableObject.CreateInstance<PanelSettings>(); AssetDatabase.CreateAsset(ps, psPath); }
            ps.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(Root + "/UI/PawTheme.tss");
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize; ps.referenceResolution = new Vector2Int(760, 1646); ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight; ps.match = 0f;
            EditorUtility.SetDirty(ps);
            var ui = new GameObject("UI"); ui.AddComponent<UIDocument>().panelSettings = ps; ui.AddComponent<CafeUI>();
            var es = new GameObject("EventSystem"); es.AddComponent<UnityEngine.EventSystems.EventSystem>(); es.AddComponent<InputSystemUIInputModule>();

            EditorSceneManager.SaveScene(scene, Root + "/Scenes/Cafe.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Root + "/Scenes/Cafe.unity", true) };
            PlayerSettings.productName = "Paw Café";
            Debug.Log($"[PawCafe] scene built: {layout.Count} default furniture, door {door.position}, axis {axis}");
        }
    }
}
