using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using AshenWoods.Enemy;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Hooks an imported enemy model up to the AI, after the model has been dropped under the
    /// enemy prefab's <c>Visuals</c> child. One menu item, safe to run again:
    ///
    ///  1. finds the model's Animator (adds one, with the model's Avatar, when missing);
    ///  2. removes colliders from the model, so it never fights the CharacterController or
    ///     blocks the enemy's own sight rays;
    ///  3. finds animation clips in the model file and its folder and matches them by name
    ///     (idle / walk / run / attack), ticking Loop Time on the movement clips;
    ///  4. creates Assets/Animations/Enemy/Enemy.controller - a Speed blend tree plus a Caught
    ///     state - or, when it exists, only fills slots that are still empty;
    ///  5. adds EnemyAnimator to the prefab and turns off the placeholder state tint.
    /// </summary>
    public static class EnemyAnimationSetup
    {
        private const string MenuPath = "Ashen Woods/Enemy/Set Up Enemy Animations";
        private const string ControllerFolder = "Assets/Animations/Enemy";
        private const string ControllerPath = ControllerFolder + "/Enemy.controller";

        /// <summary>Where the controller lives; animation files dropped here are found too.</summary>
        internal const string ControllerFolderPath = ControllerFolder;
        private const string LocomotionStateName = "Locomotion";
        private const string CaughtStateName = "Caught";

        // Blend-tree thresholds, matching EnemyBrain's default patrol and chase speeds.
        private const float IdleThreshold = 0f;
        private const float WalkThreshold = 1.5f;
        private const float RunThreshold = 3.3f;

        internal static readonly string[] IdleWords = { "idle", "stand", "breath", "wait" };
        internal static readonly string[] WalkWords = { "walk", "patrol", "stalk" };
        internal static readonly string[] RunWords = { "run", "sprint", "chase", "jog" };
        internal static readonly string[] CaughtWords = { "attack", "grab", "catch", "kill", "bite", "scream", "roar", "hit" };

        /// <summary>True when a clip or take name looks like idle / walk / run / attack.</summary>
        internal static bool IsMovementClipName(string clipName)
        {
            string lower = clipName.ToLowerInvariant();
            foreach (string[] words in new[] { IdleWords, WalkWords, RunWords, CaughtWords })
            {
                foreach (string word in words)
                {
                    if (lower.Contains(word))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        [MenuItem(MenuPath, false, 60)]
        private static void SetUp()
        {
            string prefabPath = Stage3EnemyBuilder.EnemyPrefabPath;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                EditorUtility.DisplayDialog("No enemy prefab",
                    prefabPath + " does not exist yet. Run Ashen Woods > Stages > Stage 3 first.", "OK");
                return;
            }

            // Mixamo files arrive as Generic; make them Humanoid before looking for clips.
            var prepared = new System.Text.StringBuilder();
            EnemyModelInstaller.PrepareAnimationFiles(prepared);

            var loopClips = new List<AnimationClip>();
            string report;
            bool ok;

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                report = Configure(root, loopClips, out ok);
                if (ok)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            // Re-importing a model is done last, once the prefab that uses it is saved and closed.
            foreach (AnimationClip clip in loopClips)
            {
                SetLooping(clip);
            }

            report = prepared + report;
            Debug.Log("[Enemy Animations]\n" + report);
            EditorUtility.DisplayDialog(ok ? "Enemy animations set up" : "Enemy animations not set up", report, "OK");
        }

        internal static string Configure(GameObject root, List<AnimationClip> loopClips, out bool ok)
        {
            var report = new System.Text.StringBuilder();
            ok = false;

            Transform visuals = root.transform.Find("Visuals");
            Transform model = FindModel(visuals);

            if (model == null)
            {
                report.AppendLine("No imported model found under Enemy_Patroller/Visuals.");
                report.AppendLine("Open the prefab, delete the placeholder Body and Face, drag your model " +
                                  "under Visuals, then run this menu item again.");
                return report.ToString();
            }

            report.AppendLine($"Model: {model.name}");

            // 1. Animator.
            Animator animator = model.GetComponentInChildren<Animator>(true);
            string modelPath = ModelAssetPath(model.gameObject);

            if (animator == null)
            {
                animator = model.gameObject.AddComponent<Animator>();
                report.AppendLine("Added an Animator to the model.");
            }

            if (animator.avatar == null && modelPath != null)
            {
                animator.avatar = LoadSubAsset<Avatar>(modelPath);
                if (animator.avatar != null)
                {
                    report.AppendLine($"Avatar: {animator.avatar.name}");
                }
            }

            animator.applyRootMotion = false; // EnemyNavigation moves the body, never the clips.

            // 2. Colliders.
            int removed = 0;
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
                removed++;
            }

            if (removed > 0)
            {
                report.AppendLine($"Removed {removed} collider(s) from the model.");
            }

            // 3. Clips.
            List<AnimationClip> clips = FindClips(modelPath);
            AnimationClip idle = Match(clips, IdleWords);
            AnimationClip walk = Match(clips, WalkWords);
            AnimationClip run = Match(clips, RunWords);
            AnimationClip caught = Match(clips, CaughtWords);

            report.AppendLine($"Clips found: {clips.Count}");
            report.AppendLine($"  idle:   {Describe(idle)}");
            report.AppendLine($"  walk:   {Describe(walk)}");
            report.AppendLine($"  run:    {Describe(run)}");
            report.AppendLine($"  caught: {Describe(caught)}");

            foreach (AnimationClip clip in new[] { idle, walk, run })
            {
                if (clip != null && !loopClips.Contains(clip))
                {
                    loopClips.Add(clip);
                }
            }

            // 4. Controller.
            AnimatorController controller = EnsureController(idle, walk, run, caught);
            animator.runtimeAnimatorController = controller;
            report.AppendLine($"Controller: {ControllerPath}");

            // 5. AI side.
            EnemyAnimator driver = root.GetComponent<EnemyAnimator>();
            if (driver == null)
            {
                driver = root.AddComponent<EnemyAnimator>();
            }

            Stage1PlaygroundBuilder.SetObjectReference(driver, "brain", root.GetComponent<EnemyBrain>());
            Stage1PlaygroundBuilder.SetObjectReference(driver, "animator", animator);

            EnemyStateVisuals tint = root.GetComponent<EnemyStateVisuals>();
            if (tint != null)
            {
                SetBool(tint, "tintBody", false);
            }

            // Size check: the AI's body is a 1.8 m capsule with the Eye near 1.55 m.
            Bounds bounds = RendererBounds(model);
            if (bounds.size.y > 0f)
            {
                float height = bounds.size.y;
                report.AppendLine($"Model height: {height:0.00} m (feet at {bounds.min.y - root.transform.position.y:0.00} m).");

                if (height < 1.4f || height > 2.4f)
                {
                    report.AppendLine("  WARNING: the enemy's capsule is 1.8 m tall. Scale the model to roughly that height.");
                }

                if (Mathf.Abs(bounds.min.y - root.transform.position.y) > 0.1f)
                {
                    report.AppendLine("  WARNING: the feet should be at y = 0 inside the prefab. Move the model up or down.");
                }
            }

            if (idle == null && walk == null && run == null)
            {
                report.AppendLine();
                report.AppendLine("No movement clips matched. Drag your clips into the Locomotion blend tree " +
                                  "of Enemy.controller by hand (Speed 0 idle, 1.5 walk, 3.3 run).");
            }

            ok = true;
            return report.ToString();
        }

        // --------------------------------------------------------------- model

        /// <summary>The first child of Visuals that is not one of the placeholder primitives.</summary>
        private static Transform FindModel(Transform visuals)
        {
            if (visuals == null)
            {
                return null;
            }

            for (int i = 0; i < visuals.childCount; i++)
            {
                Transform child = visuals.GetChild(i);
                if (child.name == "Body" || child.name == "Face")
                {
                    continue;
                }

                if (child.GetComponentInChildren<Renderer>(true) != null)
                {
                    return child;
                }
            }

            return null;
        }

        private static string ModelAssetPath(GameObject modelInstance)
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(modelInstance);
            string path = source != null ? AssetDatabase.GetAssetPath(source) : null;
            return string.IsNullOrEmpty(path) ? null : path;
        }

        private static T LoadSubAsset<T>(string assetPath) where T : Object
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (asset is T typed)
                {
                    return typed;
                }
            }

            return null;
        }

        /// <summary>Clips inside the model file plus every clip in the model's folder (Mixamo style).</summary>
        private static List<AnimationClip> FindClips(string modelPath)
        {
            var clips = new List<AnimationClip>();
            if (modelPath == null)
            {
                return clips;
            }

            // Next to the model, and next to the controller: both are natural places to drop clips.
            var folders = new List<string>();
            string modelFolder = Path.GetDirectoryName(modelPath)?.Replace('\\', '/');

            if (!string.IsNullOrEmpty(modelFolder))
            {
                folders.Add(modelFolder);
            }

            if (AssetDatabase.IsValidFolder(ControllerFolder) && !folders.Contains(ControllerFolder))
            {
                folders.Add(ControllerFolder);
            }

            var paths = new HashSet<string> { modelPath };
            if (folders.Count > 0)
            {
                foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip t:Model", folders.ToArray()))
                {
                    paths.Add(AssetDatabase.GUIDToAssetPath(guid));
                }
            }

            foreach (string path in paths)
            {
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__") && !clips.Contains(clip))
                    {
                        clips.Add(clip);
                    }
                }
            }

            return clips;
        }

        /// <summary>
        /// A clip matches when its name - or, for Mixamo's "mixamo.com" clips, its file name -
        /// contains one of the words.
        /// </summary>
        private static AnimationClip Match(List<AnimationClip> clips, string[] words)
        {
            foreach (AnimationClip clip in clips)
            {
                string name = (clip.name + " " + Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(clip))).ToLowerInvariant();
                foreach (string word in words)
                {
                    if (name.Contains(word))
                    {
                        return clip;
                    }
                }
            }

            return null;
        }

        private static string Describe(AnimationClip clip)
        {
            return clip == null ? "(none)" : $"{clip.name}  ({AssetDatabase.GetAssetPath(clip)})";
        }

        /// <summary>Ticks Loop Time on a clip that lives inside a model file. Plain .anim clips are left alone.</summary>
        private static void SetLooping(AnimationClip clip)
        {
            if (clip == null)
            {
                return;
            }

            string path = AssetDatabase.GetAssetPath(clip);
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
            {
                return;
            }

            ModelImporterClipAnimation[] animations = importer.clipAnimations;
            if (animations == null || animations.Length == 0)
            {
                animations = importer.defaultClipAnimations;
            }

            bool changed = false;
            for (int i = 0; i < animations.Length; i++)
            {
                if (animations[i].name == clip.name && !animations[i].loopTime)
                {
                    animations[i].loopTime = true;
                    changed = true;
                }
            }

            if (changed)
            {
                importer.clipAnimations = animations;
                importer.SaveAndReimport();
            }
        }

        // ---------------------------------------------------------- controller

        private static AnimatorController EnsureController(
            AnimationClip idle, AnimationClip walk, AnimationClip run, AnimationClip caught)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

            if (controller == null)
            {
                Directory.CreateDirectory(ControllerFolder);
                AssetDatabase.Refresh();
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            EnsureParameter(controller, EnemyAnimator.SpeedParameter, AnimatorControllerParameterType.Float);
            EnsureParameter(controller, EnemyAnimator.StateParameter, AnimatorControllerParameterType.Int);
            EnsureParameter(controller, EnemyAnimator.ChasingParameter, AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, EnemyAnimator.CaughtParameter, AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            // Locomotion: idle -> walk -> run by Speed. Empty slots stay empty for hand-filling.
            AnimatorState locomotion = FindState(machine, LocomotionStateName);
            BlendTree tree;

            if (locomotion == null)
            {
                locomotion = controller.CreateBlendTreeInController(LocomotionStateName, out tree, 0);
                tree.blendType = BlendTreeType.Simple1D;
                tree.blendParameter = EnemyAnimator.SpeedParameter;
                tree.useAutomaticThresholds = false;
                tree.AddChild(idle, IdleThreshold);
                tree.AddChild(walk, WalkThreshold);
                tree.AddChild(run, RunThreshold);
                machine.defaultState = locomotion;
            }
            else if (locomotion.motion is BlendTree existingTree)
            {
                tree = existingTree;
                ChildMotion[] children = tree.children;
                for (int i = 0; i < children.Length; i++)
                {
                    if (children[i].motion != null)
                    {
                        continue;
                    }

                    if (Mathf.Approximately(children[i].threshold, IdleThreshold))
                    {
                        children[i].motion = idle;
                    }
                    else if (Mathf.Approximately(children[i].threshold, WalkThreshold))
                    {
                        children[i].motion = walk;
                    }
                    else if (Mathf.Approximately(children[i].threshold, RunThreshold))
                    {
                        children[i].motion = run;
                    }
                }

                tree.children = children;
                EditorUtility.SetDirty(tree);
            }

            // Caught: from anywhere on the trigger, back to locomotion when the clip ends.
            AnimatorState caughtState = FindState(machine, CaughtStateName);
            if (caughtState == null)
            {
                caughtState = machine.AddState(CaughtStateName);

                AnimatorStateTransition enter = machine.AddAnyStateTransition(caughtState);
                enter.AddCondition(AnimatorConditionMode.If, 0f, EnemyAnimator.CaughtParameter);
                enter.duration = 0.1f;
                enter.canTransitionToSelf = false;

                AnimatorStateTransition exit = caughtState.AddTransition(locomotion);
                exit.hasExitTime = true;
                exit.exitTime = 0.95f;
                exit.duration = 0.2f;
            }

            if (caughtState.motion == null)
            {
                caughtState.motion = caught;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            foreach (AnimatorControllerParameter parameter in controller.parameters)
            {
                if (parameter.name == name)
                {
                    return;
                }
            }

            controller.AddParameter(name, type);
        }

        private static AnimatorState FindState(AnimatorStateMachine machine, string name)
        {
            foreach (ChildAnimatorState child in machine.states)
            {
                if (child.state.name == name)
                {
                    return child.state;
                }
            }

            return null;
        }

        // --------------------------------------------------------------- misc

        private static Bounds RendererBounds(Transform model)
        {
            return EnemyModelInstaller.MeshBounds(model.gameObject);
        }

        /// <summary>Ticks Loop Time on a clip inside a model file. Used by the model installer.</summary>
        internal static void SetLoopingPublic(AnimationClip clip)
        {
            SetLooping(clip);
        }

        private static void SetBool(Object target, string field, bool value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                return;
            }

            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
