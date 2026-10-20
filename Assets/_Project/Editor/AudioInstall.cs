using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Semblance.EditorTools
{
    /// <summary>
    /// Installs the audio review's finals (Assets/_Project/Audio/Final, main versions only: no "__" alternatives) into
    /// Resources/Audio under the names the game plays, over the generated placeholders. Copying over an existing file keeps
    /// its .meta, so every reference to the sound survives. Each installed file is marked "recorded" in its importer, and
    /// Generate Audio skips anything marked that way.
    /// </summary>
    public static class AudioInstall
    {
        public const string FinalDir = "Assets/_Project/Audio/Final";
        public const string GameDir = "Assets/_Project/Resources/Audio";

        [MenuItem("Semblance/Audio/Install Finals")]
        public static string Install()
        {
            var finals = Directory.GetFiles(FinalDir, "*.wav").Where(f => !Path.GetFileName(f).Contains("__")).ToArray();
            int replaced = 0, added = 0;
            foreach (var src in finals)
            {
                string dst = Path.Combine(GameDir, Path.GetFileName(src)).Replace('\\', '/');
                if (File.Exists(dst)) replaced++; else added++;
                File.Copy(src, dst, true);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            foreach (var src in finals)
            {
                string dst = Path.Combine(GameDir, Path.GetFileName(src)).Replace('\\', '/');
                var imp = AssetImporter.GetAtPath(dst);
                if (imp == null || imp.userData == "recorded") continue;
                imp.userData = "recorded";
                imp.SaveAndReimport();
            }
            return $"installed {finals.Length} recordings ({replaced} replaced placeholders, {added} new)";
        }
    }

    /// <summary>
    /// Import settings for the game's sounds, by what they are: music streams; loops and beds stay compressed in memory;
    /// one-shots are decoded when loaded (GameAudio warms them up front). Beds and music keep their stereo; everything
    /// heard at a place in the world is mono.
    /// </summary>
    public class AudioImportRules : AssetPostprocessor
    {
        private static readonly string[] Stereo = { "wind_loop", "crickets_loop", "room_tone_loop", "drone_loop", "drone_deep_loop", "music_theme", "chase_loop" };
        private static readonly string[] Streamed = { "music_theme", "chase_loop", "jukebox_loop" };

        private void OnPreprocessAudio()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(AudioInstall.GameDir)) return;
            var imp = (AudioImporter)assetImporter;
            if (imp.userData != "recorded" && !File.Exists(Path.Combine(AudioInstall.FinalDir, Path.GetFileName(assetPath)))) return; // placeholders keep theirs
            string name = Path.GetFileNameWithoutExtension(assetPath);
            imp.forceToMono = !Stereo.Any(name.StartsWith);
            imp.loadInBackground = false;
            var s = imp.defaultSampleSettings;
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            if (Streamed.Any(name.StartsWith)) { s.loadType = AudioClipLoadType.Streaming; s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = 0.7f; s.preloadAudioData = false; }
            else if (name.Contains("_loop")) { s.loadType = AudioClipLoadType.CompressedInMemory; s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = 0.65f; s.preloadAudioData = true; }
            else { s.loadType = AudioClipLoadType.DecompressOnLoad; s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = 0.8f; s.preloadAudioData = true; }
            imp.defaultSampleSettings = s;
        }
    }
}
