using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace JumpCraft.EditorTools
{
    /// <summary>
    /// Builds Assets/Scenes/SpriteTest.unity: a row of platforms, a floating platform, a background
    /// mountain with no collider, and a wizard and skeleton that fall onto the platforms under gravity.
    /// </summary>
    public static class SpriteTestSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/SpriteTest.unity";
        static readonly string[] SpriteNames = { "Platform", "Wizard", "Skeleton", "Mountain" };

        [MenuItem("JumpCraft/Build Sprite Test Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
                !EditorUtility.DisplayDialog("Sprite Test Scene", $"{ScenePath} already exists. Replace it?", "Replace", "Cancel"))
                return;

            // Make sure the pixel-art import settings are applied before the sprites are used.
            foreach (var name in SpriteNames)
                AssetDatabase.ImportAsset($"{PixelArtSpriteImporter.Folder}{name}.png", ImportAssetOptions.ForceUpdate);

            var platform = LoadSprite("Platform");
            var wizard = LoadSprite("Wizard");
            var skeleton = LoadSprite("Skeleton");
            var mountain = LoadSprite("Mountain");
            if (!platform || !wizard || !skeleton || !mountain) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            camera.transform.position = new Vector3(0f, 0f, -10f);
            var cam = camera.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(18, 14, 24, 255);

            var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;

            // Background: no collider, drawn behind everything.
            Place(CreateSpriteObject("Mountain", mountain, -10), mountain, -1.5f, -4.6f);

            var platforms = new GameObject("Platforms").transform;
            for (var i = -2; i <= 2; i++)
                AddPlatform(platform, platforms, i * 3.875f, -5f);
            AddPlatform(platform, platforms, 4.5f, -1.9f);

            // Characters start in the air and land on the platforms; the wizard falls in front of
            // the mountain without touching it.
            AddCharacter("Wizard", wizard, 10, -4f, 0f, x: 9, y: 14, w: 16, h: 34);
            AddCharacter("Skeleton", skeleton, 5, 4.5f, 1.5f, x: 7, y: 3, w: 12, h: 43);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[SpriteTest] Built {ScenePath}. Press Play to watch the characters land.");
        }

        static Sprite LoadSprite(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PixelArtSpriteImporter.Folder}{name}.png");
            if (!sprite) Debug.LogError($"[SpriteTest] Missing sprite {PixelArtSpriteImporter.Folder}{name}.png");
            return sprite;
        }

        static GameObject CreateSpriteObject(string name, Sprite sprite, int sortingOrder, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return go;
        }

        // Positions a sprite by its visual bottom-center, whatever its pivot is.
        static void Place(GameObject go, Sprite sprite, float centerX, float bottomY)
        {
            go.transform.position = new Vector3(centerX - sprite.bounds.center.x, bottomY - sprite.bounds.min.y, 0f);
        }

        static void AddPlatform(Sprite sprite, Transform parent, float centerX, float bottomY)
        {
            var go = CreateSpriteObject("Platform", sprite, 0, parent);
            Place(go, sprite, centerX, bottomY);
            // Solid part of the art: the flat mossy top down to the bottom of the bricks.
            var box = go.AddComponent<BoxCollider2D>();
            (box.size, box.offset) = PixelRect(sprite, x: 1, y: 1, w: 62, h: 12);
        }

        static void AddCharacter(string name, Sprite sprite, int sortingOrder, float centerX, float bottomY, int x, int y, int w, int h)
        {
            // The sprite sits on a child so CharacterAnimator can move it without moving the physics body.
            var go = new GameObject(name);
            var visual = CreateSpriteObject("Sprite", sprite, sortingOrder, go.transform);
            Place(go, sprite, centerX, bottomY);
            var body = go.AddComponent<Rigidbody2D>();
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var capsule = go.AddComponent<CapsuleCollider2D>();
            (capsule.size, capsule.offset) = PixelRect(sprite, x, y, w, h);
            go.AddComponent<CharacterAnimator>().sprite = visual.GetComponent<SpriteRenderer>();
        }

        // Converts a pixel rectangle measured from the texture's top-left corner into a collider
        // size and offset in the sprite's local space.
        static (Vector2 size, Vector2 offset) PixelRect(Sprite sprite, int x, int y, int w, int h)
        {
            var ppu = sprite.pixelsPerUnit;
            var min = sprite.bounds.min;
            var size = new Vector2(w / ppu, h / ppu);
            var offset = new Vector2(min.x + (x + w * 0.5f) / ppu, min.y + (sprite.rect.height - y - h * 0.5f) / ppu);
            return (size, offset);
        }
    }
}
