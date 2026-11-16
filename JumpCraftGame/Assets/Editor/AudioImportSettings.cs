using UnityEditor;
using UnityEngine;

namespace JumpCraft.EditorTools
{
    /// <summary>
    /// Import settings for the generated audio (Tools/generate_sounds.py, Tools/generate_music.py):
    ///   Resources/Audio      sound effects: mono, decompressed when loaded so they start instantly
    ///   Resources/Music      the score: kept compressed in memory, loaded in the background
    ///   Resources/Ambience   the same as the music
    /// All of it is Vorbis-compressed in builds, which keeps the WebGL download small.
    /// </summary>
    public class AudioImportSettings : AssetPostprocessor
    {
        const string Sfx = "Assets/Resources/Audio/";
        const string Music = "Assets/Resources/Music/";
        const string Ambience = "Assets/Resources/Ambience/";

        // Bump to reimport every clip with changed settings.
        public override uint GetVersion() => 1;

        void OnPreprocessAudio()
        {
            var sfx = assetPath.StartsWith(Sfx);
            var score = assetPath.StartsWith(Music) || assetPath.StartsWith(Ambience);
            if (!sfx && !score) return;

            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = sfx;
            importer.loadInBackground = score;

            var settings = importer.defaultSampleSettings;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = score ? 0.5f : 0.7f;
            settings.loadType = score ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
        }

        [MenuItem("Tools/Audio/Reimport Generated Audio")]
        static void Reimport()
        {
            foreach (var folder in new[] { Sfx, Music, Ambience })
            {
                foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder.TrimEnd('/') }))
                    AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            }
            Debug.Log("Reimported the generated audio.");
        }
    }
}
