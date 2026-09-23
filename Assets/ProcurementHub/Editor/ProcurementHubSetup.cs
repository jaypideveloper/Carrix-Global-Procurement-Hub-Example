using System.IO;
using ProcurementHub.Globe;
using ProcurementHub.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace ProcurementHub.EditorTools
{
    /// <summary>One-click, reproducible setup of the hub scene, materials, panel settings and build settings.</summary>
    public static class ProcurementHubSetup
    {
        const string Root = "Assets/ProcurementHub";
        const string ScenePath = Root + "/Scenes/ProcurementHub.unity";
        const string BuildFolder = "Builds/ProcurementHub";

        [MenuItem("Tools/Procurement Hub/Build Scene and Assets", priority = 1)]
        public static void BuildAll()
        {
            EnsureFolder(Root + "/Materials");
            EnsureFolder(Root + "/Scenes");
            AssetDatabase.Refresh();

            var shader = Shader.Find("ProcurementHub/Unlit");
            if (shader == null) { Debug.LogError("Shader ProcurementHub/Unlit not found - is the project compiled?"); return; }

            var ocean = Mat(shader, "M_Ocean", new Color32(0x0A, 0x16, 0x26, 0xFF), BlendMode.One, BlendMode.Zero, true, CullMode.Back, 2000,
                rim: new Color(0.16f, 0.47f, 0.84f, 0.65f), rimPower: 2.4f);
            var atmosphere = Mat(shader, "M_Atmosphere", new Color(0.25f, 0.55f, 1f, 0.85f), BlendMode.SrcAlpha, BlendMode.One, false, CullMode.Front, 3000,
                rimPower: 1.6f, rimInvert: true, rimAlpha: true);
            var land = Mat(shader, "M_LandDots", new Color32(0x6F, 0x92, 0xC4, 0xE6), BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, false, CullMode.Off, 2500);
            var grid = Mat(shader, "M_Graticule", new Color(1f, 1f, 1f, 0.06f), BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, false, CullMode.Off, 2450);
            var pin = Mat(shader, "M_Pin", Color.white, BlendMode.One, BlendMode.Zero, true, CullMode.Off, 2000);
            var glow = Mat(shader, "M_Glow", Color.white, BlendMode.SrcAlpha, BlendMode.One, false, CullMode.Off, 3100);

            CreatePanelSettings();
            AssetDatabase.SaveAssets();

            // NewScene unloads assets that are only referenced from locals, so (re)load everything after it.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(Root + "/UI/HubPanelSettings.asset");
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(Root + "/UI/Hub.uss");
            ocean = Load("M_Ocean"); atmosphere = Load("M_Atmosphere"); land = Load("M_LandDots");
            grid = Load("M_Graticule"); pin = Load("M_Pin"); glow = Load("M_Glow");
            if (uss == null || panel == null) Debug.LogError("Procurement Hub: Hub.uss or HubPanelSettings.asset could not be loaded.");

            var screenCam = new GameObject("Screen Camera").AddComponent<Camera>();
            screenCam.clearFlags = CameraClearFlags.SolidColor;
            screenCam.backgroundColor = GlobeController.PageBackground;
            screenCam.cullingMask = 0;
            screenCam.depth = -10;
            screenCam.allowHDR = false;
            screenCam.allowMSAA = false;

            var globeGo = new GameObject("Network Globe");
            var globeCam = new GameObject("Globe Camera").AddComponent<Camera>();
            globeCam.transform.SetParent(globeGo.transform, false);
            globeCam.clearFlags = CameraClearFlags.SolidColor;
            globeCam.backgroundColor = GlobeController.PageBackground;
            globeCam.fieldOfView = 32f;
            globeCam.nearClipPlane = 0.05f;
            globeCam.farClipPlane = 50f;
            globeCam.allowHDR = false;
            globeCam.allowMSAA = true;
            globeCam.enabled = false;
            var controller = globeGo.AddComponent<GlobeController>();
            var so = new SerializedObject(controller);
            so.FindProperty("globeCamera").objectReferenceValue = globeCam;
            so.FindProperty("oceanMaterial").objectReferenceValue = ocean;
            so.FindProperty("atmosphereMaterial").objectReferenceValue = atmosphere;
            so.FindProperty("landMaterial").objectReferenceValue = land;
            so.FindProperty("gridMaterial").objectReferenceValue = grid;
            so.FindProperty("pinMaterial").objectReferenceValue = pin;
            so.FindProperty("glowMaterial").objectReferenceValue = glow;
            so.ApplyModifiedPropertiesWithoutUndo();

            var hubGo = new GameObject("Procurement Hub");
            var doc = hubGo.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            var app = hubGo.AddComponent<HubApp>();
            var appSo = new SerializedObject(app);
            appSo.FindProperty("styleSheet").objectReferenceValue = uss;
            appSo.FindProperty("globe").objectReferenceValue = controller;
            appSo.ApplyModifiedPropertiesWithoutUndo();

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.productName = "Global Procurement Ops Hub";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;

            AssetDatabase.SaveAssets();
            Debug.Log("Procurement Hub: scene, materials and panel settings created at " + ScenePath);
        }

        [MenuItem("Tools/Procurement Hub/Build Windows Player", priority = 20)]
        public static void BuildWindows()
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = BuildFolder + "/GlobalProcurementHub.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log("Procurement Hub build: " + report.summary.result + " -> " + Path.GetFullPath(options.locationPathName));
        }

        [MenuItem("Tools/Procurement Hub/Open Saved-State Folder", priority = 40)]
        public static void OpenStateFolder() => EditorUtility.RevealInFinder(Application.persistentDataPath);

        static Material Mat(Shader shader, string name, Color color, BlendMode src, BlendMode dst, bool zwrite, CullMode cull, int queue,
            Color? rim = null, float rimPower = 3f, bool rimInvert = false, bool rimAlpha = false)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetColor("_BaseColor", color);
            m.SetColor("_RimColor", rim ?? new Color(0, 0, 0, 0));
            m.SetFloat("_RimPower", rimPower);
            m.SetFloat("_RimInvert", rimInvert ? 1 : 0);
            m.SetFloat("_RimAlpha", rimAlpha ? 1 : 0);
            m.SetFloat("_SrcBlend", (float)src);
            m.SetFloat("_DstBlend", (float)dst);
            m.SetFloat("_ZWrite", zwrite ? 1 : 0);
            m.SetFloat("_Cull", (float)cull);
            m.renderQueue = queue;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Load(string name) => AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/" + name + ".mat");

        static PanelSettings CreatePanelSettings()
        {
            string path = Root + "/UI/HubPanelSettings.asset";
            var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (ps == null)
            {
                ps = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(ps, path);
            }
            ps.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(Root + "/UI/HubTheme.tss");
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1600, 900);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0.5f;
            ps.sortingOrder = 0;
            EditorUtility.SetDirty(ps);
            return ps;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
