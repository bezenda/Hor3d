using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// One click from "a model in Assets/Models/Enemy" to "the enemy wears it":
    ///
    ///  1. The character file (the one with meshes) is imported as Humanoid. Its own takes are
    ///     dropped when none of them is a movement clip - Blender often exports every
    ///     property/driver action as a take, which only clutters the clip list.
    ///  2. Every other model file in the folder (Mixamo "Without Skin" downloads) is imported as
    ///     a Humanoid animation source, so its clips retarget onto the character.
    ///  3. The model replaces the placeholder capsule under the enemy prefab's Visuals, scaled to
    ///     the 1.8 m body, feet on the floor, turned to face +Z. The Eye moves to head height.
    ///  4. EnemyAnimationSetup wires the Animator, the controller and EnemyAnimator.
    ///
    /// Safe to run again: an installed model is re-fitted, not duplicated, and new animation
    /// files dropped into the folder later are picked up.
    /// </summary>
    public static class EnemyModelInstaller
    {
        private const string ModelFolder = "Assets/Models/Enemy";
        private const float TargetHeight = 1.8f;

        [MenuItem("Ashen Woods/Enemy/Install Enemy Model From Assets/Models/Enemy", false, 59)]
        private static void Install()
        {
            string report = Run(out bool ok);
            Debug.Log("[Enemy Model]\n" + report);
            EditorUtility.DisplayDialog(ok ? "Enemy model installed" : "Enemy model not installed", report, "OK");
        }

        /// <summary>Also callable from the command line with -executeMethod.</summary>
        public static void InstallInBatchMode()
        {
            string report = Run(out bool ok);
            Debug.Log("[Enemy Model]\n" + report);

            if (!ok)
            {
                EditorApplication.Exit(1);
            }
        }

        private static string Run(out bool ok)
        {
            var report = new System.Text.StringBuilder();
            ok = false;

            string prefabPath = Stage3EnemyBuilder.EnemyPrefabPath;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                report.AppendLine(prefabPath + " does not exist. Run Ashen Woods > Stages > Stage 3 first.");
                return report.ToString();
            }

            // ------------------------------------------------------- find files
            string character = null;
            int mostRenderers = 0;
            var animationFiles = new List<string>();

            if (AssetDatabase.IsValidFolder(ModelFolder))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ModelFolder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    int renderers = asset != null ? asset.GetComponentsInChildren<Renderer>(true).Length : 0;

                    if (renderers > mostRenderers)
                    {
                        if (character != null)
                        {
                            animationFiles.Add(character);
                        }

                        character = path;
                        mostRenderers = renderers;
                    }
                    else
                    {
                        animationFiles.Add(path);
                    }
                }
            }

            if (character == null)
            {
                report.AppendLine("No model with meshes found in " + ModelFolder + ".");
                report.AppendLine("Put your character's .fbx there and run this again.");
                return report.ToString();
            }

            report.AppendLine("Character: " + Path.GetFileName(character));

            // ------------------------------------------------- import settings
            ConfigureCharacterImport(character, report);

            foreach (string path in animationFiles)
            {
                ConfigureAnimationImport(path, report);
            }

            report.AppendLine(animationFiles.Count == 0
                ? "Animation files: none yet (see the note at the end)."
                : "Animation files: " + animationFiles.Count);

            // ---------------------------------------------------------- prefab
            var loopClips = new List<AnimationClip>();
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            string animationReport;

            try
            {
                if (!PlaceModel(root, character, report))
                {
                    return report.ToString();
                }

                animationReport = EnemyAnimationSetup.Configure(root, loopClips, out bool wired);
                if (wired)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            foreach (AnimationClip clip in loopClips)
            {
                EnemyAnimationSetup.SetLoopingPublic(clip);
            }

            report.AppendLine();
            report.Append(animationReport);

            if (animationFiles.Count == 0)
            {
                report.AppendLine();
                report.AppendLine("The model has a skeleton but no movement animations, so the enemy will glide in its");
                report.AppendLine("default pose. Download Idle, Walking, Running and an attack/scream from mixamo.com");
                report.AppendLine("(FBX for Unity, Without Skin, tick In Place), name the files idle / walk / run / attack,");
                report.AppendLine("drop them into " + ModelFolder + " and run this menu item again.");
            }

            ok = true;
            return report.ToString();
        }

        // ------------------------------------------------------------- import

        private static void ConfigureCharacterImport(string path, System.Text.StringBuilder report)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
            {
                return;
            }

            bool changed = false;

            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                changed = true;
            }

            if (importer.importAnimation)
            {
                bool anyMovement = false;
                int takes = 0;

                foreach (TakeInfo take in importer.importedTakeInfos)
                {
                    takes++;
                    anyMovement |= EnemyAnimationSetup.IsMovementClipName(take.name);
                }

                if (takes > 0 && !anyMovement)
                {
                    importer.importAnimation = false;
                    changed = true;
                    report.AppendLine($"Dropped {takes} non-movement take(s) from the character file " +
                                      "(Blender property actions, not walk/run/idle).");
                }
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }

            Avatar avatar = LoadSubAsset<Avatar>(path);
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                report.AppendLine("WARNING: Unity could not build a Humanoid avatar for this skeleton. Open the " +
                                  "model's Rig tab > Configure and fix the bone mapping, otherwise animations " +
                                  "from other files will not play on it.");
            }
            else
            {
                report.AppendLine("Rig: Humanoid, avatar OK.");
            }
        }

        private static void ConfigureAnimationImport(string path, System.Text.StringBuilder report)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
            {
                return;
            }

            if (importer.animationType == ModelImporterAnimationType.Human && importer.importAnimation)
            {
                return;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.SaveAndReimport();
            report.AppendLine("  set to Humanoid: " + Path.GetFileName(path));
        }

        // -------------------------------------------------------------- place

        private static bool PlaceModel(GameObject root, string characterPath, System.Text.StringBuilder report)
        {
            Transform visuals = root.transform.Find("Visuals");
            if (visuals == null)
            {
                visuals = new GameObject("Visuals").transform;
                visuals.SetParent(root.transform, false);
            }

            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(characterPath);
            GameObject model = null;

            // Remove the capsule; keep (and re-fit) a model that was installed before.
            for (int i = visuals.childCount - 1; i >= 0; i--)
            {
                GameObject child = visuals.GetChild(i).gameObject;

                if (child.name == "Body" || child.name == "Face")
                {
                    Object.DestroyImmediate(child);
                    continue;
                }

                if (model == null && PrefabUtility.GetCorrespondingObjectFromOriginalSource(child) == source)
                {
                    model = child;
                }
            }

            if (model == null)
            {
                model = (GameObject)PrefabUtility.InstantiatePrefab(source, visuals);
                report.AppendLine("Placed the model under Enemy_Patroller/Visuals.");
            }

            Transform modelTransform = model.transform;
            modelTransform.localPosition = Vector3.zero;
            modelTransform.localRotation = Quaternion.identity;
            modelTransform.localScale = Vector3.one;

            Animator animator = model.GetComponentInChildren<Animator>(true);

            // Face +Z: the humanoid's right is from the left thigh to the right thigh.
            if (animator != null && animator.isHuman)
            {
                Transform left = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                Transform right = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);

                if (left != null && right != null)
                {
                    Vector3 side = right.position - left.position;
                    side.y = 0f;
                    Vector3 forward = Vector3.Cross(side.normalized, Vector3.up);

                    if (forward.sqrMagnitude > 0.01f)
                    {
                        // Snap to a quarter turn: a model only ever arrives facing an axis, and a
                        // posed rest pose (crossed legs, a step) would otherwise skew it by degrees.
                        float yaw = Vector3.SignedAngle(forward, visuals.forward, Vector3.up);
                        yaw = Mathf.Round(yaw / 90f) * 90f;
                        modelTransform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                    }
                }
            }

            Bounds bounds = MeshBounds(model);
            if (bounds.size.y <= 0.01f)
            {
                report.AppendLine("WARNING: could not measure the model; it was placed at scale 1.");
                return true;
            }

            float scale = TargetHeight / bounds.size.y;
            modelTransform.localScale = Vector3.one * scale;

            bounds = MeshBounds(model);
            float floor = root.transform.position.y;
            modelTransform.position += Vector3.up * (floor - bounds.min.y);

            // Centre it on the capsule: on the hips for a humanoid (a tail or a cape would drag the
            // mesh centre off the body), otherwise on the mesh.
            Transform hips = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.Hips)
                : null;
            Vector3 offset = (hips != null ? hips.position : bounds.center) - root.transform.position;
            modelTransform.position -= new Vector3(offset.x, 0f, offset.z);

            report.AppendLine($"Fitted: scale {scale:0.###}, height {TargetHeight:0.0} m, feet on the floor, facing +Z.");

            // Eyes at the head bone when there is one, else at 88% of the height.
            Transform eye = root.transform.Find("Eye");
            if (eye != null)
            {
                float eyeHeight = TargetHeight * 0.88f;
                Transform head = animator != null && animator.isHuman
                    ? animator.GetBoneTransform(HumanBodyBones.Head)
                    : null;

                if (head != null)
                {
                    eyeHeight = Mathf.Clamp(head.position.y - floor + 0.08f, 1.2f, TargetHeight - 0.05f);
                }

                Vector3 local = eye.localPosition;
                eye.localPosition = new Vector3(local.x, eyeHeight, local.z);
                report.AppendLine($"Eye moved to {eyeHeight:0.00} m.");
            }

            return true;
        }

        /// <summary>
        /// Bounds of what is actually drawn, in the bind pose. Hidden and empty renderers are
        /// skipped: Blender rigs export dozens of control shapes (cs_*, CTRL_*, WGT.*) as disabled
        /// or vertex-less renderers, and counting them made a 1.8 m character measure 3.7 m wide.
        /// </summary>
        internal static Bounds MeshBounds(GameObject model)
        {
            bool any = false;
            var bounds = new Bounds();

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(false))
            {
                if (!renderer.enabled)
                {
                    continue;
                }

                Bounds rendererBounds;

                if (renderer is SkinnedMeshRenderer skinned)
                {
                    if (skinned.sharedMesh == null || skinned.sharedMesh.vertexCount == 0)
                    {
                        continue;
                    }

                    rendererBounds = TransformBounds(skinned.transform, skinned.sharedMesh.bounds);
                }
                else if (renderer is MeshRenderer)
                {
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0)
                    {
                        continue;
                    }

                    rendererBounds = renderer.bounds;
                }
                else
                {
                    continue;
                }

                if (!any)
                {
                    bounds = rendererBounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(rendererBounds);
                }
            }

            return bounds;
        }

        private static Bounds TransformBounds(Transform transform, Bounds local)
        {
            Vector3 min = local.min;
            Vector3 max = local.max;
            var result = new Bounds(transform.TransformPoint(local.center), Vector3.zero);

            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? min.x : max.x,
                    (i & 2) == 0 ? min.y : max.y,
                    (i & 4) == 0 ? min.z : max.z);
                result.Encapsulate(transform.TransformPoint(corner));
            }

            return result;
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
    }
}
