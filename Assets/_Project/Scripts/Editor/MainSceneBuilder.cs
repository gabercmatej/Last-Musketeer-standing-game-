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
            // Day 4: themes, maps, cosmetics, shared art and audio come from Tools/Musketeers/Import Art.
            var theme = AssetDatabase.LoadAssetAtPath<BoardTheme>($"{DataDir}/Themes/Courtyard.asset") ?? EnsureAsset<BoardTheme>($"{DataDir}/BoardTheme.asset");
            var gameArt = AssetDatabase.LoadAssetAtPath<GameArt>($"{DataDir}/GameArt.asset");
            var cosmetics = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>($"{DataDir}/CosmeticCatalog.asset");
            var maps = AssetDatabase.LoadAssetAtPath<MapCatalog>($"{DataDir}/MapCatalog.asset");
            var audioLib = AssetDatabase.LoadAssetAtPath<AudioLibrary>($"{DataDir}/AudioLibrary.asset");
            if (gameArt == null || cosmetics == null || maps == null || audioLib == null)
                Debug.LogWarning("[MainSceneBuilder] Run Tools/Musketeers/Import Art: some art/audio data assets are missing (placeholders will be used).");
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
            Set(art, "_gameArt", gameArt);
            Set(art, "_cosmetics", cosmetics);
            Set(art, "_maps", maps);
            Set(art, "_audio", audioLib);
            Set(art, "_spriteMaterial", material);

            var labels = new GameObject("WorldLabels").AddComponent<WorldLabels>();

            var board = new GameObject("Board").AddComponent<BoardView>();
            Set(board, "_art", art);
            Set(board, "_labels", labels);

            var stage = new GameObject("Stage").AddComponent<StageView>();
            Set(stage, "_art", art);
            Set(stage, "_labels", labels);

            var audio = new GameObject("Audio").AddComponent<AudioService>();
            Set(audio, "_library", audioLib);
            audio.gameObject.AddComponent<AudioListener>();

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
            Set(director, "_audio", audio);

            var uiGo = new GameObject("UI");
            var doc = uiGo.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            var flow = uiGo.AddComponent<AppFlow>();
            Set(flow, "_document", doc);
            Set(flow, "_menuUxml", Load<VisualTreeAsset>($"{UiDir}/MainMenu.uxml"));
            Set(flow, "_lobbyUxml", Load<VisualTreeAsset>($"{UiDir}/Lobby.uxml"));
            Set(flow, "_loadingUxml", Load<VisualTreeAsset>($"{UiDir}/Loading.uxml"));
            Set(flow, "_settingsUxml", Load<VisualTreeAsset>($"{UiDir}/Settings.uxml"));
            Set(flow, "_tips", Load<TextAsset>($"{DataDir}/Text/tips.txt"));
            Set(flow, "_credits", Load<TextAsset>($"{DataDir}/Text/credits.txt"));
            Set(flow, "_lore", Load<TextAsset>($"{DataDir}/Text/lore.txt"));
            Set(flow, "_howToPlay", Load<TextAsset>($"{DataDir}/Text/howtoplay.txt"));
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
            Set(flow, "_stage", stage);
            Set(flow, "_fx", fx);
            Set(flow, "_audio", audio);

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

        // GAME_DESIGN §4 roster. Colors match Matej's art (Day 4): Matej purple, Matic crimson, Aljaž blue, Nik green.
        static CharacterRoster EnsureRoster()
        {
            var defs = new[]
            {
                Character("Matej", "Matej", new Color(0.62f, 0.38f, 0.98f), Ability.Grappin, "Grappin",
                    "Throw a hook in a straight line (5 tiles). Hit a musketeer and you drag them toward you: if they're dragged across a pit or water, they fall in. Otherwise they land next to you. Hit a pillar, wall or crate and you pull yourself to it (the crate is not destroyed)."),
                Character("Matic", "Matic", new Color(0.93f, 0.27f, 0.30f), Ability.Switcheroo, "Switcheroo",
                    "Click any musketeer to swap places with them, wherever they are when the swap happens. Fails if you're standing on a cracked tile."),
                Character("Aljaz", "Aljaž", new Color(0.27f, 0.55f, 1.00f), Ability.TripleCharge, "Triple Charge",
                    "Fire 3 shots down one line (5 tiles). Each hit deals 1 damage and knocks back 1 tile, so up to 3 damage and 3 tiles of knockback. A Parry reflects them. Needs a loaded musket."),
                Character("Nik", "Nik", new Color(0.27f, 0.80f, 0.40f), Ability.BullRush, "Bull Rush",
                    "Charge up to 3 tiles. Whoever you hit takes 1 damage and flies back 5 tiles."),
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
            c.Id = file.ToLowerInvariant();
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
