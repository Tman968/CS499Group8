using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JumpCraft.EditorTools
{
    /// <summary>
    /// Adds the infinite parallax backdrop (sky, moon, two mountain ranges, forested hills and a
    /// near forest) to the open scene, replacing the single Mountain sprite. Rerunning replaces
    /// the existing Backdrop object. The art comes from Tools/generate_backdrop.py.
    /// Command line (Unity must be closed for this project):
    ///   Unity.exe -batchmode -quit -projectPath "path/to/JumpCraftGame"
    ///     -executeMethod JumpCraft.EditorTools.BackdropBuilder.BuildFromCommandLine
    /// </summary>
    public static class BackdropBuilder
    {
        const string Folder = PixelArtSpriteImporter.Folder + "Backdrop/";
        const string GameScene = "Assets/Scenes/SpriteTest.unity";

        // Bottom Y is relative to the camera's starting height. The vertical parallax is lower than
        // the horizontal so that climbing to the highest platforms (camera y 0 -> 3.5) lifts the
        // view over the forest and reveals the summits of the main range.
        static readonly (string sprite, int order, Vector2 parallax, float x, float bottomY, bool repeat)[] Layers =
        {
            ("Backdrop_Sky", -100, new Vector2(0.95f, 1f), 0f, -6f, true),
            ("Backdrop_Moon", -95, new Vector2(1f, 0.9f), -5.5f, 2.4f, false),
            ("Backdrop_FarRange", -90, new Vector2(0.8f, 0.3f), 0f, -5.5f, true),
            ("Backdrop_MainRange", -80, new Vector2(0.6f, 0.15f), 0f, -5.5f, true),
            ("Backdrop_Hills", -70, new Vector2(0.4f, 0.1f), 0f, -5.5f, true),
            ("Backdrop_NearForest", -60, new Vector2(0.2f, 0.05f), 0f, -5.6f, true),
        };

        [MenuItem("JumpCraft/Build Backdrop In Open Scene")]
        public static void BuildInOpenScene()
        {
            if (Build(SceneManager.GetActiveScene()))
                Debug.Log("[Backdrop] Built. Save the scene to keep it.");
        }

        public static void BuildFromCommandLine()
        {
            var scene = EditorSceneManager.OpenScene(GameScene);
            var succeeded = Build(scene) && EditorSceneManager.SaveScene(scene);
            EditorApplication.Exit(succeeded ? 0 : 1);
        }

        static bool Build(Scene scene)
        {
            foreach (var layer in Layers)
                AssetDatabase.ImportAsset($"{Folder}{layer.sprite}.png", ImportAssetOptions.ForceUpdate);

            var camera = Object.FindAnyObjectByType<Camera>();
            if (!camera)
            {
                Debug.LogError("[Backdrop] The scene has no camera.");
                return false;
            }

            foreach (var root in scene.GetRootGameObjects())
                if (root.name is "Backdrop" or "Mountain")
                    Undo.DestroyObjectImmediate(root);

            var backdrop = new GameObject("Backdrop");
            Undo.RegisterCreatedObjectUndo(backdrop, "Build Backdrop");
            var cameraPosition = camera.transform.position;

            foreach (var layer in Layers)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}{layer.sprite}.png");
                if (!sprite)
                {
                    Debug.LogError($"[Backdrop] Missing sprite {Folder}{layer.sprite}.png. Run Tools/generate_backdrop.py.");
                    Undo.DestroyObjectImmediate(backdrop);
                    return false;
                }

                var go = new GameObject(layer.sprite.Replace("Backdrop_", ""));
                go.transform.SetParent(backdrop.transform, false);
                // Sprites are pivoted at the bottom center.
                go.transform.position = new Vector3(cameraPosition.x + layer.x, cameraPosition.y + layer.bottomY, 0f);

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = layer.order;

                var parallax = go.AddComponent<ParallaxLayer>();
                parallax.targetCamera = camera;
                parallax.parallax = layer.parallax;
                parallax.repeat = layer.repeat;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            return true;
        }
    }
}
