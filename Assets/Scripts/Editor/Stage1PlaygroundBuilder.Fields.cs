using UnityEditor;
using UnityEngine;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Serialized-field helpers shared by the stage builders.
    ///
    /// Setting a private serialized field from an editor script needs SerializedObject: a
    /// direct C# assignment would be thrown away the next time Unity deserialises the
    /// component. These two helpers also record a prefab-instance override when the target
    /// lives in a prefab instance, so an edit on a scene object sticks.
    /// </summary>
    public static partial class Stage1PlaygroundBuilder
    {
        /// <summary>Writes a float field by name. Logs an error when the field is missing.</summary>
        internal static void SetFloat(Object target, string fieldName, float value)
        {
            if (target == null)
            {
                Debug.LogError($"Cannot set '{fieldName}': the target object is null.");
                return;
            }

            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogError(
                    $"{target.GetType().Name} has no serialized field '{fieldName}', so it was not set.",
                    target);
                return;
            }

            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            RecordOverride(target);
        }

        /// <summary>Reads a float field by name, falling back when the field does not exist.</summary>
        internal static float GetFloat(Object target, string fieldName, float fallback = 0f)
        {
            if (target == null)
            {
                return fallback;
            }

            SerializedProperty property = new SerializedObject(target).FindProperty(fieldName);
            return property != null ? property.floatValue : fallback;
        }

        /// <summary>
        /// Marks the change as an override when the component belongs to a prefab instance.
        /// Without this, the value can silently revert when the scene reloads.
        /// </summary>
        private static void RecordOverride(Object target)
        {
            if (target is Component component && PrefabUtility.IsPartOfPrefabInstance(component))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
            else if (target is GameObject gameObject && PrefabUtility.IsPartOfPrefabInstance(gameObject))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(gameObject);
            }
        }
    }
}
