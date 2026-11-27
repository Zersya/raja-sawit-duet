using System.IO;
using RajaSawit.Core;
using RajaSawit.Data;
using RajaSawit.UI;
using RajaSawit.View;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RajaSawit.EditorTools
{
    /// <summary>
    /// Menu "Raja Sawit" in the Unity Editor. Builds the project settings and the Home scene
    /// from code, so the repo does not have to carry hand-made scene files yet.
    /// </summary>
    public static class RajaSawitSetup
    {
        private const string ConfigFolder = "Assets/_Project/Config";
        private const string SceneFolder = "Assets/_Project/Scenes";
        private const string ConfigPath = ConfigFolder + "/BalanceConfig.asset";
        private const string ScenePath = SceneFolder + "/Home.unity";

        [MenuItem("Raja Sawit/1. Siapkan Project", priority = 1)]
        public static void SetupProject()
        {
            PlayerSettings.productName = "Raja Sawit";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.rajasawit.game");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.runInBackground = false;

            // Play Store needs 64-bit builds.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            EnsureConfig();
            AssetDatabase.SaveAssets();
            Debug.Log("[RajaSawit] Project disiapkan: portrait, nama Raja Sawit, Android IL2CPP ARM64, BalanceConfig dibuat.");
        }

        [MenuItem("Raja Sawit/2. Buat Scene Home", priority = 2)]
        public static void BuildHomeScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateHomeScene();
        }

        /// <summary>Creates Home.unity and puts it in Build Settings. No dialogs, so CI can call it.</summary>
        public static void CreateHomeScene()
        {
            BalanceConfigAsset config = EnsureConfig();
            EnsureFolder(SceneFolder);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 7f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x1C, 0x45, 0x21, 0xFF);
            camGo.AddComponent<AudioListener>();

            var sessionGo = new GameObject("GameSession");
            var session = sessionGo.AddComponent<GameSession>();
            SetRef(session, "config", config);

            var mapGo = new GameObject("Map", typeof(Grid));
            var map = mapGo.AddComponent<IsoMapView>();

            var controller = camGo.AddComponent<CameraController>();
            SetRef(controller, "map", map);

            var hudGo = new GameObject("HUD");
            var hud = hudGo.AddComponent<HudView>();
            var interactor = hudGo.AddComponent<MapInteractor>();
            SetRef(interactor, "cameraController", controller);
            SetRef(interactor, "map", map);
            SetRef(interactor, "hud", hud);

            var esGo = new GameObject("EventSystem", typeof(EventSystem));
#if RS_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            esGo.AddComponent<StandaloneInputModule>();
#endif

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log($"[RajaSawit] Scene Home dibuat di {ScenePath} dan dimasukkan ke Build Settings. Tekan Play.");
        }

        [MenuItem("Raja Sawit/Hapus Save Lokal", priority = 20)]
        public static void DeleteSave()
        {
            SaveService.Delete();
            Debug.Log($"[RajaSawit] Save dihapus: {SaveService.FilePath}");
        }

        [MenuItem("Raja Sawit/Buka Folder Save", priority = 21)]
        public static void RevealSave()
        {
            EditorUtility.RevealInFinder(Application.persistentDataPath);
        }

        private static BalanceConfigAsset EnsureConfig()
        {
            var asset = AssetDatabase.LoadAssetAtPath<BalanceConfigAsset>(ConfigPath);
            if (asset != null) return asset;
            EnsureFolder(ConfigFolder);
            asset = ScriptableObject.CreateInstance<BalanceConfigAsset>();
            AssetDatabase.CreateAsset(asset, ConfigPath);
            AssetDatabase.SaveAssets();
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[RajaSawit] Field {field} tidak ditemukan di {target.GetType().Name}");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
