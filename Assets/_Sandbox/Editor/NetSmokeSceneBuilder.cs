using Musketeers.Sandbox;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Musketeers.EditorTools
{
    // Idempotent builder for the throwaway Day 0 smoke-test scene (delete with _Sandbox after Day 4).
    public static class NetSmokeSceneBuilder
    {
        public const string ScenePath = "Assets/_Sandbox/NetSmoke.unity";

        [MenuItem("Tools/Musketeers/Sandbox/Build NetSmoke Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.1f, 0.14f);
            camGo.transform.position = new Vector3(0, 0, -10);

            var nmGo = new GameObject("NetworkManager");
            var nm = nmGo.AddComponent<NetworkManager>();
            var utp = nmGo.AddComponent<UnityTransport>();
            if (nm.NetworkConfig == null)
                nm.NetworkConfig = new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = utp;

            var pingGo = new GameObject("NetSmokePing");
            pingGo.AddComponent<NetworkObject>();
            pingGo.AddComponent<NetSmokePing>();

            var uiGo = new GameObject("NetSmokeUI");
            uiGo.AddComponent<NetSmokeUI>();
            uiGo.AddComponent<NetSmokeAutoTest>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            // NGO marks a NetworkObject as in-scene placed (and hashes it) in OnValidate, but only
            // once the scene is saved and in Build Settings. Reopen so that runs, then save again.
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var no in Object.FindObjectsByType<NetworkObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                typeof(NetworkObject).GetMethod("OnValidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.Invoke(no, null);
                EditorUtility.SetDirty(no);
                Debug.Log($"[NetSmoke] {no.name}: InScenePlaced={no.InScenePlaced} hash={no.PrefabIdHash}");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[NetSmoke] Built " + ScenePath + " and set it as the only build scene.");
        }
    }
}
