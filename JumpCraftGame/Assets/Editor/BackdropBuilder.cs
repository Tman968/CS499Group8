using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JumpCraft.EditorTools
{
    /// <summary>
    /// Adds the infinite parallax backdrop to the open scene, replacing the single Mountain sprite:
    /// a mountain set (sky, moon, two ranges, forested hills, near forest) and a spooky forest set
    /// (sky, moon, three depths of trees, forest floor), with BiomeBackdrop zooming and fogging
    /// between them. Rerunning replaces the existing Backdrop object. The art comes from
    /// Tools/generate_backdrop.py.
    /// Command line (Unity must be closed for this project):
    ///   Unity.exe -batchmode -quit -projectPath "path/to/JumpCraftGame"
    ///     -executeMethod JumpCraft.EditorTools.BackdropBuilder.BuildFromCommandLine
    /// </summary>
    public static class BackdropBuilder
    {
        const string Folder = PixelArtSpriteImporter.Folder + "Backdrop/";
        const string GameScene = "Assets/Scenes/SpriteTest.unity";
        static readonly Color FogColor = new Color32(40, 46, 56, 255);

        // Bottom Y is relative to the camera's starting height. The vertical parallax is lower than
        // the horizontal so that climbing to the highest platforms (camera y 0 -> 3.5) lifts the
        // view over the trees: the mountain summits come into view, and in the forest the canopy
        // closes in overhead.
        // Zoom: for the mountains, how much bigger the layer gets by a forest edge (near layers
        // most, like walking into the trees); for the forest, how much smaller it starts there.
        static readonly (string sprite, int order, Vector2 parallax, float x, float bottomY, bool repeat, float zoom)[] Mountains =
        {
            ("Backdrop_Sky", -100, new Vector2(0.95f, 1f), 0f, -6f, true, 0.05f),
            ("Backdrop_Moon", -95, new Vector2(1f, 0.9f), -5.5f, 2.4f, false, 0.1f),
            ("Backdrop_FarRange", -90, new Vector2(0.8f, 0.3f), 0f, -5.5f, true, 0.15f),
            ("Backdrop_MainRange", -80, new Vector2(0.6f, 0.15f), 0f, -5.5f, true, 0.35f),
            ("Backdrop_Hills", -70, new Vector2(0.4f, 0.1f), 0f, -5.5f, true, 1.6f),
            ("Backdrop_NearForest", -60, new Vector2(0.2f, 0.05f), 0f, -5.6f, true, 4f),
        };

        static readonly (string sprite, int order, Vector2 parallax, float x, float bottomY, bool repeat, float zoom)[] Forest =
        {
            ("Forest_Sky", -55, new Vector2(0.95f, 1f), 0f, -6f, true, 0f),
            ("Forest_Moon", -50, new Vector2(1f, 0.9f), 4.5f, 0.7f, false, 0.05f),
            ("Forest_FarTrees", -45, new Vector2(0.8f, 0.3f), 0f, -5.5f, true, 0.1f),
            ("Forest_MidTrees", -40, new Vector2(0.6f, 0.15f), 0f, -5.5f, true, 0.2f),
            ("Forest_Trunks", -35, new Vector2(0.4f, 0.1f), 0f, -5.5f, true, 0.3f),
            ("Forest_Ground", -30, new Vector2(0.2f, 0.05f), 0f, -5.6f, true, 0.4f),
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
            var sprites = Mountains.Concat(Forest).Select(l => l.sprite).Append("Backdrop_Fade").ToArray();
            foreach (var name in sprites)
                AssetDatabase.ImportAsset($"{Folder}{name}.png", ImportAssetOptions.ForceUpdate);
            foreach (var name in sprites)
            {
                if (LoadSprite(name)) continue;
                Debug.LogError($"[Backdrop] Missing sprite {Folder}{name}.png. Run Tools/generate_backdrop.py.");
                return false;
            }

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

            var biomes = backdrop.AddComponent<BiomeBackdrop>();
            biomes.targetCamera = camera;
            biomes.mountainLayers = AddLayers(backdrop.transform, "Mountains", Mountains, camera);
            biomes.forestLayers = AddLayers(backdrop.transform, "Forest", Forest, camera);

            var fog = new GameObject("Fog").AddComponent<SpriteRenderer>();
            fog.transform.SetParent(backdrop.transform, false);
            fog.sprite = LoadSprite("Backdrop_Fade");
            fog.color = new Color(FogColor.r, FogColor.g, FogColor.b, 0f);
            fog.sortingOrder = -20;
            fog.enabled = false;
            biomes.fog = fog;

            EditorSceneManager.MarkSceneDirty(scene);
            return true;
        }

        static BiomeBackdrop.Layer[] AddLayers(Transform backdrop, string groupName,
            (string sprite, int order, Vector2 parallax, float x, float bottomY, bool repeat, float zoom)[] layers, Camera camera)
        {
            var group = new GameObject(groupName).transform;
            group.SetParent(backdrop, false);
            var cameraPosition = camera.transform.position;

            return layers.Select(layer =>
            {
                var go = new GameObject(layer.sprite.Replace("Backdrop_", "").Replace("Forest_", ""));
                go.transform.SetParent(group, false);
                // Sprites are pivoted at the bottom center.
                go.transform.position = new Vector3(cameraPosition.x + layer.x, cameraPosition.y + layer.bottomY, 0f);

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = LoadSprite(layer.sprite);
                renderer.sortingOrder = layer.order;

                var parallax = go.AddComponent<ParallaxLayer>();
                parallax.targetCamera = camera;
                parallax.parallax = layer.parallax;
                parallax.repeat = layer.repeat;
                return new BiomeBackdrop.Layer { layer = parallax, zoom = layer.zoom };
            }).ToArray();
        }

        static Sprite LoadSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}{name}.png");
    }
}
