using System.IO;
using Musketeers.Data;
using Musketeers.Presentation;
using Musketeers.Sim;
using Musketeers.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;

namespace Musketeers.EditorTools
{
    // Builds Assets/_Project/Scenes/Main.unity from scratch (CLAUDE.md rule 7) and makes it the only
    // build scene. Idempotent: the scene is regenerated every run; data assets (characters, theme,
    // panel settings, material) are only created when missing, so Matej's edits to them survive.
    public static class MainSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        const string CharactersDir = "Assets/_Project/Data/Characters";
        const string DataDir = "Assets/_Project/Data";
        const string UiDir = "Assets/_Project/UI";
        const string ArtDir = "Assets/_Project/Art/Placeholder";

        [MenuItem("Tools/Musketeers/Build Main Scene")]
        public static void Build()
        {
            // New scene first: opening it unloads unused assets, which would destroy anything loaded before.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var material = EnsureMaterial();
            var roster = EnsureRoster();
            var theme = EnsureAsset<BoardTheme>($"{DataDir}/BoardTheme.asset");
            var panel = EnsurePanelSettings();

            // Camera rig: the rig moves to frame the board, the camera child shakes.
            var rigGo = new GameObject("CameraRig");
            var rig = rigGo.AddComponent<CameraRig>();
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(rigGo.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 7f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = theme.Background;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
            rigGo.transform.position = new Vector3(0, 0, -10);
            Set(rig, "_camera", cam);

            var art = new GameObject("Art").AddComponent<ArtLibrary>();
            Set(art, "_roster", roster);
            Set(art, "_theme", theme);
            Set(art, "_spriteMaterial", material);

            var labels = new GameObject("WorldLabels").AddComponent<WorldLabels>();

            var board = new GameObject("Board").AddComponent<BoardView>();
            Set(board, "_art", art);
            Set(board, "_labels", labels);

            var fx = new GameObject("Fx").AddComponent<Fx>();
            Set(fx, "_art", art);
            Set(fx, "_labels", labels);
            Set(fx, "_camera", rig);

            var preview = new GameObject("PlanPreview").AddComponent<PlanPreview>();
            Set(preview, "_art", art);
            Set(preview, "_labels", labels);

            var input = new GameObject("PlanningInput").AddComponent<PlanningInput>();
            Set(input, "_rig", rig);

            var director = new GameObject("Playback").AddComponent<PlaybackDirector>();
            Set(director, "_board", board);
            Set(director, "_fx", fx);

            var uiGo = new GameObject("UI");
            var doc = uiGo.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            var flow = uiGo.AddComponent<AppFlow>();
            Set(flow, "_document", doc);
            Set(flow, "_menuUxml", Load<VisualTreeAsset>($"{UiDir}/MainMenu.uxml"));
            Set(flow, "_hudUxml", Load<VisualTreeAsset>($"{UiDir}/Hud.uxml"));
            Set(flow, "_scoreboardUxml", Load<VisualTreeAsset>($"{UiDir}/Scoreboard.uxml"));
            Set(flow, "_resultsUxml", Load<VisualTreeAsset>($"{UiDir}/Results.uxml"));
            Set(flow, "_styles", Load<StyleSheet>($"{UiDir}/Main.uss"));
            Set(flow, "_art", art);
            Set(flow, "_board", board);
            Set(flow, "_rig", rig);
            Set(flow, "_preview", preview);
            Set(flow, "_input", input);
            Set(flow, "_director", director);
            Set(flow, "_labels", labels);

            // UI Toolkit reads input through the EventSystem when the project uses the Input System only.
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            // Main is the only scene in the build; NetSmoke stays in the project until Day 4 is done.
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[MainSceneBuilder] Built " + ScenePath + " and set it as the only build scene.");
        }

        static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[MainSceneBuilder] {target.GetType().Name} has no field {field}");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static T Load<T>(string path) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null)
                Debug.LogError($"[MainSceneBuilder] Missing asset {path}");
            return a;
        }

        static T EnsureAsset<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null)
                return a;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        // Unlit sprites need no 2D lights. A real material asset keeps the shader in the Web build.
        static Material EnsureMaterial()
        {
            string path = $"{ArtDir}/SpriteUnlit.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null)
                return m;
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null)
            {
                Debug.LogError("[MainSceneBuilder] URP 2D Sprite-Unlit-Default shader not found.");
                shader = Shader.Find("Sprites/Default");
            }
            Directory.CreateDirectory(ArtDir);
            m = new Material(shader) { name = "SpriteUnlit" };
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // GAME_DESIGN §4 roster. Colors are strong and distinct, and none is red (red means danger).
        static CharacterRoster EnsureRoster()
        {
            var defs = new[]
            {
                Character("Matej", "Matej", new Color(0.20f, 0.52f, 1.00f), Ability.Grappin, "Grappin",
                    "Hook the first thing in a line (5). Units get yanked to you; walls pull you in."),
                Character("Matic", "Matic", new Color(0.20f, 0.80f, 0.36f), Ability.Switcheroo, "Switcheroo",
                    "Swap places with the first unit or keg in a line (5). Passes over pits."),
                Character("Aljaz", "Aljaž", new Color(0.70f, 0.38f, 1.00f), Ability.DoubleCharge, "Double Charge",
                    "Range 7 shot that pierces 2 units and ignores Parry. Needs a loaded musket."),
                Character("Nik", "Nik", new Color(1.00f, 0.60f, 0.12f), Ability.BullRush, "Bull Rush",
                    "Charge up to 3 tiles. A unit you crash into is pushed 2."),
            };
            var roster = EnsureAsset<CharacterRoster>($"{DataDir}/CharacterRoster.asset");
            if (roster.Characters == null || roster.Characters.Length == 0)
            {
                roster.Characters = defs;
                EditorUtility.SetDirty(roster);
            }
            return roster;
        }

        static CharacterDef Character(string file, string name, Color color, Ability ability, string abilityName, string desc)
        {
            string path = $"{CharactersDir}/{file}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<CharacterDef>(path);
            if (existing != null)
                return existing;
            var c = EnsureAsset<CharacterDef>(path);
            c.DisplayName = name;
            c.Color = color;
            c.Ability = ability;
            c.AbilityName = abilityName;
            c.AbilityDescription = desc;
            EditorUtility.SetDirty(c);
            return c;
        }

        // Scales the 1280x720 layout to fit any window (Expand: never overflows on narrow windows).
        static PanelSettings EnsurePanelSettings()
        {
            string path = $"{UiDir}/PanelSettings.asset";
            var p = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (p != null)
                return p;
            p = ScriptableObject.CreateInstance<PanelSettings>();
            p.themeStyleSheet = Load<ThemeStyleSheet>($"{UiDir}/MusketeersTheme.tss");
            p.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            p.referenceResolution = new Vector2Int(1280, 720);
            p.screenMatchMode = PanelScreenMatchMode.Expand;
            AssetDatabase.CreateAsset(p, path);
            return p;
        }
    }
}
