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
    ///     state, and a Stunned state when a stun clip exists - or, when it exists, only fills
    ///     slots that are still empty; Foot IK is switched on for the locomotion;
    ///  5. adds EnemyAnimator to the prefab, gives it the walk/run clips' own speeds (so the
    ///     feet keep pace with the body) and turns off the placeholder state tint.
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
        private const string StunnedStateName = "Stunned";
        private const string GettingUpStateName = "GettingUp";

        // Blend-tree thresholds, matching EnemyBrain's default patrol and chase speeds.
        private const float IdleThreshold = 0f;
        private const float WalkThreshold = 1.5f;
        private const float RunThreshold = 3.3f;

        internal static readonly string[] IdleWords = { "idle", "stand", "breath", "wait" };
        internal static readonly string[] WalkWords = { "walk", "patrol", "stalk" };
        internal static readonly string[] RunWords = { "run", "sprint", "chase", "jog" };
        internal static readonly string[] GetUpWords = { "getting_up", "getting up", "gettingup", "get_up", "get up", "getup", "stand_up", "stand up", "standup" };
        internal static readonly string[] StunWords = { "stun", "daze", "blind", "stagger", "flinch", "reaction", "cover", "dizzy" };
        internal static readonly string[] CaughtWords = { "attack", "grab", "catch", "kill", "bite", "scream", "roar", "hit" };
        internal static readonly string[] NoticeWords = { "notice", "alert", "shout" };
        internal static readonly string[] SearchWords = { "search", "look_around", "look around" };
        internal static readonly string[] TurnWords = { "turn" };
        internal static readonly string[] ExtraWords = { "climb" };

        /// <summary>The clip played while EnemyNavigation lifts the body over a low obstacle (not the ladder clip).</summary>
        internal static readonly string[] ClimbWords = { "climb_over", "vault" };
        private const string ClimbStateName = "Climb";

        private const string TurnStateName = "Turn";
        private const string UpperLayerName = EnemyAnimator.UpperLayerName;
        private const string NoticeStateName = "Notice";
        private const string SearchStateName = "Search";
        private const string UpperMaskPath = ControllerFolder + "/EnemyUpperBody.mask";

        /// <summary>Looping clips that must stay over their spot (rotation and drift baked into the pose).</summary>
        internal static readonly List<AnimationClip> InPlaceLoopsToPrepare = new List<AnimationClip>();

        /// <summary>True when a clip or take name looks like idle / walk / run / attack.</summary>
        internal static bool IsMovementClipName(string clipName)
        {
            string lower = clipName.ToLowerInvariant();
            foreach (string[] words in new[] { IdleWords, WalkWords, RunWords, StunWords, GetUpWords, CaughtWords, NoticeWords, SearchWords, TurnWords, ExtraWords })
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

            string report = Run(out bool ok);
            EditorUtility.DisplayDialog(ok ? "Enemy animations set up" : "Enemy animations not set up", report, "OK");
        }

        /// <summary>The setup without the closing dialog, for automation.</summary>
        internal static string Run(out bool ok)
        {
            string prefabPath = Stage3EnemyBuilder.EnemyPrefabPath;

            // Mixamo files arrive as Generic; make them Humanoid before looking for clips.
            var prepared = new System.Text.StringBuilder();
            EnemyModelInstaller.PrepareAnimationFiles(prepared);

            // Toes on the ball of the foot, not on a toe finger: the auto-mapping tilted the feet (2026-11-19).
            prepared.AppendLine(EnemyAvatarFix.Apply());

            var loopClips = new List<AnimationClip>();
            string report;

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

            PrepareQueuedOneShots();

            foreach (AnimationClip clip in InPlaceLoopsToPrepare)
            {
                PrepareInPlaceLoop(clip);
            }

            InPlaceLoopsToPrepare.Clear();

            PrepareClimbClip(ClimbClipToPrepare);
            ClimbClipToPrepare = null;

            PrepareTurnLoop(TurnClipToPrepare);
            TurnClipToPrepare = null;

            report = prepared + report;
            Debug.Log("[Enemy Animations]\n" + report);
            return report;
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
            // Get-up first: "stand up" must not be taken for an idle ("stand").
            AnimationClip getUp = Match(clips, GetUpWords);
            // The extra clips are taken out first: "notice" must not end up as the catch, nor a
            // "standing turn" as the idle.
            AnimationClip notice = Match(clips, NoticeWords);
            AnimationClip search = Match(clips, SearchWords);
            AnimationClip turn = Match(clips, TurnWords);
            AnimationClip climbOver = Match(clips, ClimbWords);
            List<AnimationClip> rest = clips.FindAll(c => c != getUp && c != notice && c != search && c != turn
                                                          && c != climbOver && Match(new List<AnimationClip> { c }, ExtraWords) == null);
            AnimationClip idle = Match(rest, IdleWords);
            AnimationClip walk = Match(rest, WalkWords);
            AnimationClip run = Match(rest, RunWords);
            AnimationClip stunned = Match(rest, StunWords);
            AnimationClip caught = Match(rest.FindAll(c => c != stunned), CaughtWords);

            report.AppendLine($"Clips found: {clips.Count}");
            report.AppendLine($"  idle:   {Describe(idle)}");
            report.AppendLine($"  walk:   {Describe(walk)}");
            report.AppendLine($"  run:    {Describe(run)}");
            report.AppendLine($"  caught: {Describe(caught)}");
            report.AppendLine($"  stun:   {Describe(stunned)}");
            report.AppendLine($"  get up: {Describe(getUp)}");
            report.AppendLine($"  notice: {Describe(notice)}");
            report.AppendLine($"  search: {Describe(search)}");
            report.AppendLine($"  turn:   {Describe(turn)}");
            report.AppendLine($"  climb:  {Describe(climbOver)}");

            foreach (AnimationClip clip in new[] { idle, walk, run })
            {
                if (clip != null && !loopClips.Contains(clip))
                {
                    loopClips.Add(clip);
                }
            }

            // 4. Controller.
            AnimatorController controller = EnsureController(idle, walk, run, caught, stunned, getUp);
            EnsureExtraStates(controller, notice, search, turn);
            EnsureClimbState(controller, climbOver);
            EnsureLookAtPass(controller);

            // The stun is a fall and the get-up a rise: each plays once, the body's drop or
            // climb baked into the pose.
            OneShotsToPrepare.Clear();
            FallClipToPrepare = stunned;
            if (stunned != null) OneShotsToPrepare.Add(stunned);
            if (getUp != null) OneShotsToPrepare.Add(getUp);
            if (notice != null) OneShotsToPrepare.Add(notice);
            InPlaceLoopsToPrepare.Clear();
            if (search != null) InPlaceLoopsToPrepare.Add(search);
            TurnClipToPrepare = turn;
            ClimbClipToPrepare = climbOver;
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
            Stage1PlaygroundBuilder.SetFloat(driver, "walkThreshold", WalkThreshold);
            Stage1PlaygroundBuilder.SetFloat(driver, "runThreshold", RunThreshold);
            Stage1PlaygroundBuilder.SetFloat(driver, "getUpSeconds", getUp != null ? getUp.length / GetUpPlaybackSpeed : 0f);
            Stage1PlaygroundBuilder.SetFloat(driver, "noticeSeconds", notice != null ? notice.length : 0f);
            Stage1PlaygroundBuilder.SetFloat(driver, "climbClipSeconds", climbOver != null ? climbOver.length : 0f);
            Stage1PlaygroundBuilder.SetObjectReference(driver, "navigation", root.GetComponent<EnemyNavigation>());

            // A clip's own pace; an "in place" clip measures ~0 and keeps the inspector value.
            float walkPace = ClipSpeed(walk);
            float runPace = ClipSpeed(run);
            if (runPace <= 0.3f)
            {
                runPace = InPlaceGroundSpeed(animator, run); // In-place clip: read it off the feet.
            }
            if (walkPace > 0.3f)
            {
                Stage1PlaygroundBuilder.SetFloat(driver, "walkClipSpeed", walkPace);
            }

            if (runPace > 0.3f)
            {
                Stage1PlaygroundBuilder.SetFloat(driver, "runClipSpeed", runPace);
            }

            report.AppendLine($"Clip pace: walk {(walkPace > 0.3f ? walkPace.ToString("0.00") + " m/s" : "in place (inspector value kept)")}, " +
                              $"run {(runPace > 0.3f ? runPace.ToString("0.00") + " m/s" : "in place (inspector value kept)")}.");

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

        /// <summary>Set by Configure; prepared after the prefab is saved (re-importing a model mid-edit is unsafe).</summary>
        internal static readonly List<AnimationClip> OneShotsToPrepare = new List<AnimationClip>();
        internal static AnimationClip FallClipToPrepare;

        private const float GetUpPlaybackSpeed = 1f;

        /// <summary>Blend from running into the grab: long enough that the hips do not jump.</summary>
        private const float CaughtBlendSeconds = 0.25f;

        internal static void PrepareQueuedOneShots()
        {
            foreach (AnimationClip clip in OneShotsToPrepare)
            {
                // The fall stays over its spot; the get-up keeps its authored drift.
                PrepareOneShot(clip, bakeDrift: clip != FallClipToPrepare);
            }

            OneShotsToPrepare.Clear();
            FallClipToPrepare = null;
        }

        /// <summary>The stun clip plays at this fraction of its speed, so the fall lasts most of the stun.</summary>
        private const float StunPlaybackSpeed = 0.7f;

        /// <summary>
        /// A one-shot clip whose body moves up and down (a fall): no loop, and root rotation,
        /// height and drift baked into the pose. Unbaked, the drop to the floor becomes root
        /// motion, which the AI-driven enemy discards - so it lay flat in mid-air.
        ///
        /// <paramref name="bakeDrift"/> false (the fall): the horizontal drift is NOT baked, so the
        /// body falls over its own spot. Baked, the Mixamo fall stumbled 1.08 m backwards, the
        /// get-up starts on the spot, and the 0.3 s blend between them slid the lying body a
        /// metre forward before it rose. Unbaked, both clips lie over the root and meet cleanly
        /// (and the fall can no longer carry the body into a wall).
        /// </summary>
        internal static void PrepareOneShot(AnimationClip clip, bool bakeDrift = true)
        {
            string path = clip != null ? AssetDatabase.GetAssetPath(clip) : null;
            if (path == null || !(AssetImporter.GetAtPath(path) is ModelImporter importer))
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
                ModelImporterClipAnimation a = animations[i];
                if (a.name != clip.name)
                {
                    continue;
                }

                if (a.loopTime || !a.lockRootRotation || !a.lockRootHeightY || a.lockRootPositionXZ != bakeDrift
                    || !a.keepOriginalOrientation || !a.keepOriginalPositionY || a.keepOriginalPositionXZ != bakeDrift)
                {
                    a.loopTime = false;
                    a.lockRootRotation = true;      // Bake Into Pose: rotation
                    a.keepOriginalOrientation = true;
                    a.lockRootHeightY = true;       // Bake Into Pose: Y (the fall itself)
                    a.keepOriginalPositionY = true;
                    a.lockRootPositionXZ = bakeDrift;      // Bake Into Pose: XZ, or drop the drift
                    a.keepOriginalPositionXZ = bakeDrift;  // unbaked: based on centre of mass
                    animations[i] = a;
                    changed = true;
                }
            }

            if (changed)
            {
                importer.clipAnimations = animations;
                importer.SaveAndReimport();
            }
        }

        /// <summary>Metres per second the clip's root covers at 1x (0 for an in-place clip).</summary>
        private static float ClipSpeed(AnimationClip clip)
        {
            if (clip == null)
            {
                return 0f;
            }

            Vector3 average = clip.averageSpeed;
            average.y = 0f;
            return average.magnitude;
        }

        /// <summary>
        /// Ground speed of an in-place clip: how fast a planted foot travels backwards under the
        /// body. Sampled on a hidden copy of the model, so the prefab's pose is never touched.
        /// The guessed 3.6 m/s for the Mixamo run was 10 % off (measured 3.3): the feet skated.
        /// </summary>
        private static float InPlaceGroundSpeed(Animator animator, AnimationClip clip)
        {
            if (animator == null || clip == null || !animator.isHuman)
            {
                return 0f;
            }

            GameObject copy = Object.Instantiate(animator.gameObject);
            copy.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                copy.transform.SetPositionAndRotation(new Vector3(0f, -500f, 0f), Quaternion.identity);
                Animator a = copy.GetComponent<Animator>();
                Transform[] feet =
                {
                    a.GetBoneTransform(HumanBodyBones.LeftFoot), a.GetBoneTransform(HumanBodyBones.RightFoot)
                };
                Transform[] toes =
                {
                    a.GetBoneTransform(HumanBodyBones.LeftToes), a.GetBoneTransform(HumanBodyBones.RightToes)
                };
                if (feet[0] == null || feet[1] == null)
                {
                    return 0f;
                }

                const int samples = 600;
                float step = clip.length / samples;
                var height = new float[2, samples + 1];
                var forward = new float[2, samples + 1];
                float[] lowest = { float.MaxValue, float.MaxValue };

                for (int i = 0; i <= samples; i++)
                {
                    clip.SampleAnimation(copy, i * step);
                    for (int f = 0; f < 2; f++)
                    {
                        float y = feet[f].position.y;
                        if (toes[f] != null)
                        {
                            y = Mathf.Min(y, toes[f].position.y);
                        }

                        height[f, i] = y;
                        forward[f, i] = copy.transform.InverseTransformPoint(feet[f].position).z;
                        lowest[f] = Mathf.Min(lowest[f], y);
                    }
                }

                var speeds = new List<float>();
                for (int f = 0; f < 2; f++)
                {
                    for (int i = 1; i <= samples; i++)
                    {
                        if (height[f, i] < lowest[f] + 0.03f && height[f, i - 1] < lowest[f] + 0.03f)
                        {
                            speeds.Add(-(forward[f, i] - forward[f, i - 1]) / step);
                        }
                    }
                }

                if (speeds.Count < 10)
                {
                    return 0f;
                }

                speeds.Sort();
                float median = speeds[speeds.Count / 2];
                return median > 0.3f ? median : 0f;
            }
            finally
            {
                Object.DestroyImmediate(copy);
            }
        }

        private static AnimatorController EnsureController(
            AnimationClip idle, AnimationClip walk, AnimationClip run, AnimationClip caught, AnimationClip stunned, AnimationClip getUp)
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
            EnsureParameter(controller, EnemyAnimator.StunnedParameter, AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, EnemyAnimator.GettingUpParameter, AnimatorControllerParameterType.Bool);

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

            // A 0.1 s blend snapped a running body into the grab (the hips jumped). Re-applied
            // every run so older controllers get it too.
            foreach (AnimatorStateTransition anyToCaught in machine.anyStateTransitions)
            {
                if (anyToCaught.destinationState == caughtState)
                {
                    anyToCaught.duration = CaughtBlendSeconds;
                    anyToCaught.hasFixedDuration = true;
                }
            }

            // Retargeted Mixamo legs splay and cross without Foot IK.
            locomotion.iKOnFeet = true;

            // Stunned: only with a clip - an empty state on a Humanoid snaps to the T-pose.
            AnimatorState stunnedState = FindState(machine, StunnedStateName);
            if (stunnedState == null && stunned != null)
            {
                stunnedState = machine.AddState(StunnedStateName);
                stunnedState.speed = StunPlaybackSpeed;

                AnimatorStateTransition enter = machine.AddAnyStateTransition(stunnedState);
                enter.AddCondition(AnimatorConditionMode.If, 0f, EnemyAnimator.StunnedParameter);
                enter.duration = 0.1f;
                enter.canTransitionToSelf = false;

                AnimatorStateTransition exit = stunnedState.AddTransition(locomotion);
                exit.AddCondition(AnimatorConditionMode.IfNot, 0f, EnemyAnimator.StunnedParameter);
                exit.hasExitTime = false;
                exit.duration = 0.3f;
            }

            if (stunnedState != null && stunnedState.motion == null)
            {
                stunnedState.motion = stunned;
            }

            if (stunnedState != null)
            {
                // Lying on the floor: foot IK would drag the feet to standing targets.
                stunnedState.iKOnFeet = false;
                if (Mathf.Approximately(stunnedState.speed, 1f))
                {
                    stunnedState.speed = StunPlaybackSpeed; // Hand-set speeds other than 1 are kept.
                }
            }

            // Getting up: Stunned -> GettingUp -> Locomotion, so it rises instead of popping up.
            if (stunnedState != null && getUp != null)
            {
                AnimatorState rising = FindState(machine, GettingUpStateName);
                if (rising == null)
                {
                    rising = machine.AddState(GettingUpStateName);
                    rising.iKOnFeet = false;
                    rising.speed = GetUpPlaybackSpeed;

                    AnimatorStateTransition done = rising.AddTransition(locomotion);
                    done.hasExitTime = true;
                    done.exitTime = 0.9f;
                    done.duration = 0.35f;
                }

                if (rising.motion == null)
                {
                    rising.motion = getUp;
                }

                // The old "not stunned -> walk" exit would skip the get-up: it now also waits
                // for GettingUp to be off (a fallback for a stun cut short).
                bool hasRise = false;
                foreach (AnimatorStateTransition exit in stunnedState.transitions)
                {
                    if (exit.destinationState == rising)
                    {
                        hasRise = true;
                    }
                    else if (exit.destinationState == locomotion)
                    {
                        bool guarded = false;
                        foreach (AnimatorCondition condition in exit.conditions)
                        {
                            guarded |= condition.parameter == EnemyAnimator.GettingUpParameter;
                        }

                        if (!guarded)
                        {
                            exit.AddCondition(AnimatorConditionMode.IfNot, 0f, EnemyAnimator.GettingUpParameter);
                        }
                    }
                }

                if (!hasRise)
                {
                    AnimatorStateTransition rise = stunnedState.AddTransition(rising);
                    rise.AddCondition(AnimatorConditionMode.If, 0f, EnemyAnimator.GettingUpParameter);
                    rise.hasExitTime = false;
                    rise.duration = 0.3f;
                }
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        /// <summary>
        /// Notice (a scream when a chase starts) and Search (looking around) play on an upper-body
        /// layer, so the legs keep walking or running with the AI and never slide. EnemyAnimator
        /// fades the layer in and out. Turn is a base-layer state for turning on the spot; the
        /// clip's own rotation is baked out (the AI turns the body) and TurnLeft mirrors it.
        /// </summary>
        private static void EnsureExtraStates(AnimatorController controller, AnimationClip notice, AnimationClip search, AnimationClip turn)
        {
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState locomotion = FindState(machine, LocomotionStateName);

            if (turn != null && locomotion != null)
            {
                EnsureParameter(controller, EnemyAnimator.TurningParameter, AnimatorControllerParameterType.Bool);
                EnsureParameter(controller, EnemyAnimator.TurnLeftParameter, AnimatorControllerParameterType.Bool);

                AnimatorState turnState = FindState(machine, TurnStateName);
                if (turnState == null)
                {
                    turnState = machine.AddState(TurnStateName);
                    turnState.mirrorParameterActive = true;
                    turnState.mirrorParameter = EnemyAnimator.TurnLeftParameter;
                    turnState.iKOnFeet = true;

                    AnimatorStateTransition into = locomotion.AddTransition(turnState);
                    into.AddCondition(AnimatorConditionMode.If, 0f, EnemyAnimator.TurningParameter);
                    into.hasExitTime = false;
                    into.duration = 0.2f;

                    AnimatorStateTransition back = turnState.AddTransition(locomotion);
                    back.AddCondition(AnimatorConditionMode.IfNot, 0f, EnemyAnimator.TurningParameter);
                    back.hasExitTime = false;
                    back.duration = 0.25f;
                }

                if (turnState.motion == null)
                {
                    turnState.motion = turn;
                }
            }

            if (notice == null && search == null)
            {
                return;
            }

            EnsureParameter(controller, EnemyAnimator.NoticeParameter, AnimatorControllerParameterType.Trigger);

            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperMaskPath);
            if (mask == null)
            {
                mask = new AvatarMask();
                for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                {
                    var part = (AvatarMaskBodyPart)i;
                    bool lower = part == AvatarMaskBodyPart.Root || part == AvatarMaskBodyPart.LeftLeg ||
                                 part == AvatarMaskBodyPart.RightLeg || part == AvatarMaskBodyPart.LeftFootIK ||
                                 part == AvatarMaskBodyPart.RightFootIK;
                    mask.SetHumanoidBodyPartActive(part, !lower);
                }

                AssetDatabase.CreateAsset(mask, UpperMaskPath);
            }

            int layerIndex = System.Array.FindIndex(controller.layers, l => l.name == UpperLayerName);
            if (layerIndex < 0)
            {
                controller.AddLayer(UpperLayerName);
                layerIndex = controller.layers.Length - 1;
            }

            AnimatorControllerLayer[] layers = controller.layers;
            layers[layerIndex].avatarMask = mask;
            layers[layerIndex].blendingMode = AnimatorLayerBlendingMode.Override;
            layers[layerIndex].defaultWeight = 0f; // EnemyAnimator fades it in.
            controller.layers = layers;

            AnimatorStateMachine upper = controller.layers[layerIndex].stateMachine;

            // Search is the resting state of the layer: at weight 0 it shows nothing.
            AnimatorState searchState = FindState(upper, SearchStateName);
            if (searchState == null)
            {
                searchState = upper.AddState(SearchStateName);
                upper.defaultState = searchState;
            }

            if (searchState.motion == null)
            {
                searchState.motion = search != null ? search : notice;
            }

            if (notice != null)
            {
                AnimatorState noticeState = FindState(upper, NoticeStateName);
                if (noticeState == null)
                {
                    noticeState = upper.AddState(NoticeStateName);

                    AnimatorStateTransition enter = upper.AddAnyStateTransition(noticeState);
                    enter.AddCondition(AnimatorConditionMode.If, 0f, EnemyAnimator.NoticeParameter);
                    enter.duration = 0.15f;
                    enter.canTransitionToSelf = false;

                    AnimatorStateTransition done = noticeState.AddTransition(searchState);
                    done.hasExitTime = true;
                    done.exitTime = 0.95f;
                    done.duration = 0.2f;
                }

                if (noticeState.motion == null)
                {
                    noticeState.motion = notice;
                }
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        /// <summary>EnemyLookAt turns the head in OnAnimatorIK, which runs only with IK Pass on the base layer.</summary>
        internal static void EnsureLookAtPass(AnimatorController controller)
        {
            AnimatorControllerLayer[] layers = controller.layers;
            if (layers.Length == 0 || layers[0].iKPass)
            {
                return;
            }

            layers[0].iKPass = true;
            controller.layers = layers;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        internal static AnimationClip ClimbClipToPrepare;

        /// <summary>
        /// Climb: a base-layer one-shot entered from any state while EnemyNavigation climbs (bool Climbing) and
        /// left for Locomotion when it lands. Its speed follows ClimbSpeed, so the clip lasts exactly the climb.
        /// </summary>
        private static void EnsureClimbState(AnimatorController controller, AnimationClip climb)
        {
            if (climb == null)
            {
                return;
            }

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState locomotion = FindState(machine, LocomotionStateName);
            if (locomotion == null)
            {
                return;
            }

            EnsureParameter(controller, EnemyAnimator.ClimbingParameter, AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, EnemyAnimator.ClimbSpeedParameter, AnimatorControllerParameterType.Float);

            AnimatorState state = FindState(machine, ClimbStateName);
            if (state == null)
            {
                state = machine.AddState(ClimbStateName);
                state.speedParameterActive = true;
                state.speedParameter = EnemyAnimator.ClimbSpeedParameter;

                AnimatorStateTransition into = machine.AddAnyStateTransition(state);
                into.AddCondition(AnimatorConditionMode.If, 0f, EnemyAnimator.ClimbingParameter);
                into.hasExitTime = false;
                into.duration = 0.12f;
                into.canTransitionToSelf = false;

                AnimatorStateTransition back = state.AddTransition(locomotion);
                back.AddCondition(AnimatorConditionMode.IfNot, 0f, EnemyAnimator.ClimbingParameter);
                back.hasExitTime = false;
                back.duration = 0.2f;
            }

            if (state.motion == null)
            {
                state.motion = climb;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// The climb plays once, rotation and drift baked into the pose but NOT the height: the vertical part
        /// becomes root motion, which the AI-moved enemy discards, so the body is lifted only once - by
        /// EnemyNavigation's arc - instead of by both the clip and the code.
        /// </summary>
        private static void PrepareClimbClip(AnimationClip clip)
        {
            string path = clip != null ? AssetDatabase.GetAssetPath(clip) : null;
            if (path == null || !(AssetImporter.GetAtPath(path) is ModelImporter importer))
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
                ModelImporterClipAnimation a = animations[i];
                if (a.name != clip.name)
                {
                    continue;
                }

                if (a.loopTime || !a.lockRootRotation || a.lockRootHeightY || !a.lockRootPositionXZ)
                {
                    a.loopTime = false;
                    a.lockRootRotation = true;
                    a.keepOriginalOrientation = true;
                    a.lockRootHeightY = false;
                    a.keepOriginalPositionY = false; // root height follows the feet: the leap leaves the pose
                    a.lockRootPositionXZ = true;
                    a.keepOriginalPositionXZ = true;
                    animations[i] = a;
                    changed = true;
                }
            }

            if (changed)
            {
                importer.clipAnimations = animations;
                importer.SaveAndReimport();
            }
        }

        /// <summary>The turn-on-the-spot clip (see <see cref="PrepareTurnLoop"/>).</summary>
        internal static AnimationClip TurnClipToPrepare;

        /// <summary>
        /// The turn on the spot loops with its yaw NOT baked into the pose: Mixamo's turn rotates the body ~120 deg
        /// per cycle, and baked into the pose the model swung round and snapped back every 0.93 s (the "turn left
        /// that keeps looping" bug in Search). Left as root motion, the yaw is discarded (applyRootMotion is off,
        /// the AI turns the body) and only the stepping feet remain. Height and drift stay baked.
        /// </summary>
        internal static void PrepareTurnLoop(AnimationClip clip)
        {
            string path = clip != null ? AssetDatabase.GetAssetPath(clip) : null;
            if (path == null || !(AssetImporter.GetAtPath(path) is ModelImporter importer))
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
                ModelImporterClipAnimation a = animations[i];
                if (a.name != clip.name)
                {
                    continue;
                }

                if (!a.loopTime || a.lockRootRotation || !a.lockRootHeightY || !a.lockRootPositionXZ)
                {
                    a.loopTime = true;
                    a.lockRootRotation = false;
                    a.lockRootHeightY = true;
                    a.keepOriginalPositionY = true;
                    a.lockRootPositionXZ = true;
                    a.keepOriginalPositionXZ = true;
                    animations[i] = a;
                    changed = true;
                }
            }

            if (changed)
            {
                importer.clipAnimations = animations;
                importer.SaveAndReimport();
            }
        }

        /// <summary>A looping clip that stays over its spot: loop, root rotation and drift baked into the pose.</summary>
        internal static void PrepareInPlaceLoop(AnimationClip clip)
        {
            string path = clip != null ? AssetDatabase.GetAssetPath(clip) : null;
            if (path == null || !(AssetImporter.GetAtPath(path) is ModelImporter importer))
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
                ModelImporterClipAnimation a = animations[i];
                if (a.name != clip.name)
                {
                    continue;
                }

                if (!a.loopTime || !a.lockRootRotation || !a.lockRootHeightY || !a.lockRootPositionXZ)
                {
                    a.loopTime = true;
                    a.lockRootRotation = true;
                    a.keepOriginalOrientation = true;
                    a.lockRootHeightY = true;
                    a.keepOriginalPositionY = true;
                    a.lockRootPositionXZ = true;
                    a.keepOriginalPositionXZ = true;
                    animations[i] = a;
                    changed = true;
                }
            }

            if (changed)
            {
                importer.clipAnimations = animations;
                importer.SaveAndReimport();
            }
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
