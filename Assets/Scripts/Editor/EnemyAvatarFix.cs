using UnityEditor;
using UnityEngine;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Unity's Humanoid auto-mapping on Unreal/Fortnite-style rigs (foot -> ball -> toe fingers) picks one of the
    /// toe fingers as "Toes" (e.g. L_SmallToe1) and leaves the ball bone unmapped. The Humanoid then measures the
    /// foot's length and direction towards the outer small toe instead of the ball of the foot, so retargeted
    /// clips turn and tilt the feet (and with Foot IK, the knees): the "skewed legs" of the enemy (2026-11-19).
    /// This remaps Toes to the foot's direct child (the ball bone) whenever the mapped toe sits below it.
    /// Only the model importer's human description changes; run Set Up Enemy Animations afterwards as usual.
    /// Menu: Ashen Woods > Enemy > Fix Foot Bones In Avatar
    /// </summary>
    public static class EnemyAvatarFix
    {
        private const string ModelFolder = "Assets/Models/Enemy";

        [MenuItem("Ashen Woods/Enemy/Fix Foot Bones In Avatar", false, 2)]
        private static void FromMenu()
        {
            Debug.Log("[Enemy Avatar] " + Apply());
        }

        /// <summary>Fixes every Humanoid model in Assets/Models/Enemy. Returns a short report.</summary>
        public static string Apply()
        {
            var report = new System.Text.StringBuilder();
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ModelFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (importer == null || model == null || importer.animationType != ModelImporterAnimationType.Human)
                {
                    continue;
                }

                HumanDescription description = importer.humanDescription;
                HumanBone[] bones = description.human;
                bool changed = false;
                changed |= FixToes(model.transform, bones, "LeftFoot", "LeftToes", report);
                changed |= FixToes(model.transform, bones, "RightFoot", "RightToes", report);
                if (!changed)
                {
                    report.Append(System.IO.Path.GetFileName(path)).Append(": feet already mapped to the ball bones. ");
                    continue;
                }

                description.human = bones;
                importer.humanDescription = description;
                importer.SaveAndReimport();
                report.Append(System.IO.Path.GetFileName(path)).Append(" re-imported. ");
            }

            return report.Length > 0 ? report.ToString() : "No Humanoid model in " + ModelFolder + ".";
        }

        private static bool FixToes(Transform root, HumanBone[] bones, string footName, string toesName, System.Text.StringBuilder report)
        {
            int footIndex = System.Array.FindIndex(bones, b => b.humanName == footName);
            int toesIndex = System.Array.FindIndex(bones, b => b.humanName == toesName);
            if (footIndex < 0 || toesIndex < 0)
            {
                return false;
            }

            Transform foot = FindDeep(root, bones[footIndex].boneName);
            Transform toes = FindDeep(root, bones[toesIndex].boneName);
            if (foot == null || toes == null || toes.parent == foot)
            {
                return false;
            }

            // Walk up from the mapped toe to the foot's direct child: that is the ball of the foot.
            Transform ball = toes;
            while (ball.parent != null && ball.parent != foot)
            {
                ball = ball.parent;
            }

            if (ball.parent != foot)
            {
                return false;
            }

            report.Append(toesName).Append(": ").Append(toes.name).Append(" -> ").Append(ball.name).Append(". ");
            HumanBone bone = bones[toesIndex];
            bone.boneName = ball.name;
            bones[toesIndex] = bone;
            return true;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }

            return null;
        }
    }
}
