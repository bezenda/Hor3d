using UnityEditor;
using UnityEngine;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Runs after the Mixamo MCP's MixamoPostprocessor (Assets/MixamoMcp), which decides Loop Time
    /// from the clip's name. Every Mixamo take is called "mixamo.com", so it switched looping off for
    /// idle, walk and run alike: each clip played once and froze on its last frame, and the enemy
    /// glided along in one pose (2026-11-11). This puts back what the importer settings say (the
    /// Loop Time box, which "Set Up Enemy Animations" ticks), or, with no clip settings, judges by
    /// the file name instead of the take name.
    /// </summary>
    public sealed class MixamoLoopFix : AssetPostprocessor
    {
        private static readonly string[] LoopingWords = { "idle", "walk", "run", "jog", "sprint", "crouch", "breath" };

        public override int GetPostprocessOrder() => 1000; // After every other animation postprocessor.

        private void OnPostprocessAnimation(GameObject root, AnimationClip clip)
        {
            if (!(assetImporter is ModelImporter importer)
                || !assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            bool? wanted = null;
            foreach (ModelImporterClipAnimation settings in importer.clipAnimations)
            {
                if (settings.name == clip.name)
                {
                    wanted = settings.loopTime;
                    break;
                }
            }

            if (wanted == null)
            {
                if (clip.name != "mixamo.com")
                {
                    return; // A named take: whatever set it knew the name.
                }

                string file = System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
                bool looping = false;
                foreach (string word in LoopingWords)
                {
                    looping |= file.Contains(word);
                }

                wanted = looping;
            }

            AnimationClipSettings current = AnimationUtility.GetAnimationClipSettings(clip);
            if (current.loopTime != wanted.Value)
            {
                current.loopTime = wanted.Value;
                AnimationUtility.SetAnimationClipSettings(clip, current);
            }
        }
    }
}
