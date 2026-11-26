using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EmotionalEntity.EditorTools
{
    /// <summary>Tools > Emotional Entity menu: check the project, then build ready-to-play scenes.</summary>
    public static class PrototypeSceneBuilder
    {
        const string Root = "Assets/EmotionalEntity/Generated";
        const string MvrManagerPrefab = "Packages/com.middlevr/MVRManager.prefab";
        const string MvrInputPrefab = "Packages/com.middlevr.inputsystem/MVRInputSystem.prefab";
        const string TargetUnity = "6000.2.8f1";

        // ---------------------------------------------------------------- 1. Check
        [MenuItem("Tools/Emotional Entity/1. Check Setup", priority = 1)]
        public static void CheckSetup()
        {
            var sb = new StringBuilder();
            int problems = 0;
            Action<bool, string, string> line = (ok, good, bad) =>
            {
                sb.Append(ok ? "OK    " : "TODO  ").AppendLine(ok ? good : bad);
                if (!ok) problems++;
            };

            line(Application.unityVersion == TargetUnity, "Unity " + Application.unityVersion,
                "Unity " + Application.unityVersion + " (the lab uses " + TargetUnity + "; other 6000.x versions usually work)");

            UnityEngine.Rendering.RenderPipelineAsset rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            line(rp != null && rp.GetType().Name.Contains("Universal"), "URP is the active render pipeline",
                "URP is not active. Create the project from the Universal 3D template.");

            Shader cloud = Shader.Find("EmotionalEntity/ProceduralCloud");
            line(cloud != null && cloud.isSupported, "Particle shader compiled",
                "Particle shader missing or not supported. Check the Console for shader errors.");

#if ENABLE_INPUT_SYSTEM
            line(true, "Input System active", "");
#else
            line(false, "", "Input System not active: Edit > Project Settings > Player > Active Input Handling = Input System Package (New) or Both. MiddleVR needs it.");
#endif

            bool sdk = FindType("Bhaptics.SDK2.BhapticsLibrary") != null;
            line(sdk, "bHaptics SDK2 imported", "bHaptics SDK2 not imported. Import the bHaptics Haptic Plugin.");
            line(FindBhapticsPrefab() != null, "[bhaptics] prefab found", "[bhaptics] prefab not found (comes with the bHaptics plugin).");

            bool mvr = AssetDatabase.LoadAssetAtPath<GameObject>(MvrManagerPrefab) != null;
            sb.Append(mvr ? "OK    MiddleVR installed" : "INFO  MiddleVR not installed (needed only for the CAVE scene)").AppendLine();
            if (mvr) line(FindType("MiddleVR.Unity.URP.MVR_URPSettings") != null, "MiddleVR URP package installed", "com.middlevr.urp missing. Install it with com.middlevr.");

            sb.AppendLine();
            sb.AppendLine("Cannot be checked from Unity: bHaptics Player running with the vest paired, and the app linked in bHaptics > Developer Window.");
            string text = sb.ToString();
            Debug.Log("[Prototype] Setup check\n" + text);
            EditorUtility.DisplayDialog(problems == 0 ? "Setup looks good" : problems + " thing(s) to fix", text, "OK");
        }

        // ---------------------------------------------------------------- 2. Desktop
        [MenuItem("Tools/Emotional Entity/2. Build Desktop Scene", priority = 2)]
        public static void BuildDesktop()
        {
            TrialCondition[] conditions = MakeConditions();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddLightAndEnvironment();
            var boot = new GameObject("Prototype").AddComponent<PrototypeBootstrap>();
            boot.conditions = conditions;
            boot.display = DisplayMode.Desktop;
            // bHaptics also works on a Mac with bHaptics Player, so use the vest whenever the SDK is imported.
            if (AddBhapticsPrefab()) boot.haptics = HapticKind.Bhaptics;
            Save(scene, "EmotionalEntity_Desktop");
            Selection.activeGameObject = boot.gameObject;
            Debug.Log("[Prototype] Desktop scene ready. Press Play. WASD walk, O observer view, F1 panel, Space emergency stop.");
        }

        // ---------------------------------------------------------------- 3. CAVE
        /// <summary>Single-PC CAVE: MiddleVR renders every wall from its .vrx and moves the tracker nodes.</summary>
        [MenuItem("Tools/Emotional Entity/3. Build CAVE Scene (MiddleVR)", priority = 3)]
        public static void BuildMiddleVr()
        {
            var mvrPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MvrManagerPrefab);
            if (mvrPrefab == null)
            {
                EditorUtility.DisplayDialog("MiddleVR not installed",
                    "Packages/com.middlevr was not found. Add the three MiddleVR packages first (README, step 6), then run this again.", "OK");
                return;
            }

            TrialCondition[] conditions = MakeConditions();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddLightAndEnvironment();

            // MiddleVR copies this camera for every wall and eye. Its own position does not matter.
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.01f, 0.012f, 0.02f);
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            camGo.transform.position = new Vector3(0f, 1.65f, 0f);

            var mvr = (GameObject)PrefabUtility.InstantiatePrefab(mvrPrefab);
            Type urpSettings = FindType("MiddleVR.Unity.URP.MVR_URPSettings");
            if (urpSettings != null) mvr.AddComponent(urpSettings);
            else Debug.LogWarning("[Prototype] com.middlevr.urp not found. Install it, or URP walls may render without warping.");

            var inputPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MvrInputPrefab);
            if (inputPrefab != null) PrefabUtility.InstantiatePrefab(inputPrefab);
            else Debug.LogWarning("[Prototype] com.middlevr.inputsystem not found.");

            var boot = new GameObject("Prototype").AddComponent<PrototypeBootstrap>();
            boot.conditions = conditions;
            boot.display = DisplayMode.MiddleVR;
            boot.tracking = TrackingKind.SceneNodes;
            boot.haptics = HapticKind.Bhaptics;
            bool bh = AddBhapticsPrefab();

            Save(scene, "EmotionalEntity_CAVE_MiddleVR");
            Selection.activeGameObject = mvr;
            EditorUtility.DisplayDialog("CAVE scene created",
                "Two settings are still yours to make:\n\n" +
                "1. On MVRManager (selected), set the configuration file to your CAVE's .vrx.\n" +
                "2. On Prototype, check the tracker node names against that .vrx (default HeadNode, HandNode) and the safety area.\n\n" +
                (bh ? "The [bhaptics] prefab was added." : "bHaptics prefab not found: import the bHaptics Haptic Plugin, then build this scene again."), "OK");
        }

        // ---------------------------------------------------------------- helpers
        static GameObject FindBhapticsPrefab()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab bhaptics"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant() != "[bhaptics]") continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) return prefab;
            }
            return null;
        }

        /// <summary>Adds the SDK's [bhaptics] initialisation prefab (Assets/Bhaptics/SDK2/Prefabs) if the plugin is imported.</summary>
        static bool AddBhapticsPrefab()
        {
            GameObject prefab = FindBhapticsPrefab();
            if (prefab == null)
            {
                Debug.LogWarning("[Prototype] [bhaptics] prefab not found. Haptics stay simulated until the bHaptics plugin is imported.");
                return false;
            }
            PrefabUtility.InstantiatePrefab(prefab);
            return true;
        }

        static Type FindType(string fullName)
        {
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = a.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        static void Save(UnityEngine.SceneManagement.Scene scene, string name)
        {
            string path = Root + "/" + name + ".unity";
            EditorSceneManager.SaveScene(scene, path);
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            list.RemoveAll(s => s.path == path);
            list.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
            AssetDatabase.Refresh();
        }

        static TrialCondition[] MakeConditions()
        {
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets/EmotionalEntity", "Generated");
            var conditions = new TrialCondition[PresetLibrary.Names.Length];
            for (int i = 0; i < conditions.Length; i++) conditions[i] = MakeCondition(PresetLibrary.Names[i]);
            AssetDatabase.SaveAssets();
            return conditions;
        }

        /// <summary>Keeps condition assets you already tuned; only missing ones are created.</summary>
        static TrialCondition MakeCondition(string id)
        {
            string dir = Root + "/" + id;
            string condPath = dir + "/" + id + "_Condition.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TrialCondition>(condPath);
            if (existing != null && existing.visual != null && existing.behavior != null && existing.schedule != null && existing.stimulus != null)
                return existing;

            TrialCondition c = PresetLibrary.Create(id);
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(Root, id);
            c.visual.name = id + "_Visual"; c.behavior.name = id + "_Behavior";
            c.schedule.name = id + "_Schedule"; c.stimulus.name = id + "_Stimulus";
            Create(c.visual, dir + "/" + c.visual.name + ".asset");
            Create(c.behavior, dir + "/" + c.behavior.name + ".asset");
            Create(c.schedule, dir + "/" + c.schedule.name + ".asset");
            Create(c.stimulus, dir + "/" + c.stimulus.name + ".asset");
            Create(c, condPath);
            return AssetDatabase.LoadAssetAtPath<TrialCondition>(condPath);
        }

        static void Create(UnityEngine.Object o, string path)
        {
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(o, path);
        }

        /// <summary>Placeholder 3D space: a floor at the CAVE floor height and pillars at 9 m for depth and parallax.
        /// Replace the "Environment" object with your real scene.</summary>
        static void AddLightAndEnvironment()
        {
            var lightGo = new GameObject("Key Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.25f;
            lightGo.transform.rotation = Quaternion.Euler(55f, 30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.07f, 0.08f, 0.1f);

            string matPath = Root + "/Environment.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetColor("_BaseColor", new Color(0.09f, 0.1f, 0.12f));
                AssetDatabase.CreateAsset(mat, matPath);
            }

            var env = new GameObject("Environment");
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(env.transform);
            floor.transform.localScale = new Vector3(4f, 1f, 4f);
            floor.GetComponent<Renderer>().sharedMaterial = mat;

            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f;
                GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                p.name = "Pillar" + i;
                p.transform.SetParent(env.transform);
                p.transform.position = new Vector3(Mathf.Sin(a) * 9f, 2.5f, Mathf.Cos(a) * 9f);
                p.transform.localScale = new Vector3(0.7f, 2.5f, 0.7f);
                p.GetComponent<Renderer>().sharedMaterial = mat;
            }
        }
    }
}
