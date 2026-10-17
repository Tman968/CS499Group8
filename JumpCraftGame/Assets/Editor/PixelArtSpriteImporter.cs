using UnityEditor;
using UnityEngine;

namespace JumpCraft.EditorTools
{
    /// <summary>
    /// Every texture in Assets/Art/Sprites is imported as crisp pixel art:
    /// 16 pixels per unit, point filtering, no compression, no mipmaps.
    /// The pivot (bottom center) is only set on first import, so it can be changed per sprite.
    /// </summary>
    public class PixelArtSpriteImporter : AssetPostprocessor
    {
        public const string Folder = "Assets/Art/Sprites/";
        public const int PixelsPerUnit = 16;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;

            var importer = (TextureImporter)assetImporter;
            var firstImport = importer.importSettingsMissing;

            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;

            if (!firstImport) return;

            importer.spriteImportMode = SpriteImportMode.Single;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }
    }
}
