using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using AshenWoods.World;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Keeps the owner's hand edits safe from the builders, without anyone having to remember.
    ///
    ///  - Edit a builder-made decor object in the scene (move, rotate, scale it in the Scene view
    ///    or the Inspector, or change one of its components) and the item gets a
    ///    <see cref="HandEdited"/> marker. Builders leave marked items alone.
    ///  - Delete one and its path goes into the scene's <see cref="HandEditLedger"/>, so builders
    ///    that "add what is missing" do not bring it back.
    ///
    /// Only decor is tracked: everything under House/Dressing, House/Atmosphere and Menu_Set, and
    /// the menu's camera, lights, volume and canvas. Gameplay pieces (doors, keys, enemy, player)
    /// are not, because a builder recreating them is what keeps the level playable.
    /// Builders write through SerializedObject or plain assignment without Undo, so their own
    /// changes never trip this.
    /// </summary>
    [InitializeOnLoad]
    public static class HandEditTracker
    {
        private const string LedgerName = "HandEdits";

        /// <summary>Containers whose children are separate decor items.</summary>
        private static readonly string[] Containers = { "Dressing", "Atmosphere", "Menu_Set" };

        /// <summary>Root objects that are themselves one item each.</summary>
        private static readonly string[] RootItems =
        {
            "MenuCamera", "Moonlight", "FrontFill", "Menu_Canvas", "Menu_Volume", "Chase_Volume"
        };

        private static readonly Dictionary<EntityId, string> Paths = new Dictionary<EntityId, string>();
        private static bool pathsDirty = true;

        static HandEditTracker()
        {
            Undo.postprocessModifications += OnModifications;
            ObjectChangeEvents.changesPublished += OnChanges;
            EditorApplication.hierarchyChanged += () => pathsDirty = true;
            EditorSceneManager.sceneOpened += (scene, mode) => pathsDirty = true;
        }

        // ------------------------------------------------------------- builder API

        /// <summary>True when builders must not touch this object (it or a parent/child was hand-edited).</summary>
        public static bool Protected(Transform target) => HandEdited.Protects(target);

        /// <summary>True when the owner deleted <paramref name="childName"/> under <paramref name="parent"/>.</summary>
        public static bool WasDeleted(Transform parent, string childName)
        {
            HandEditLedger ledger = FindLedger(parent != null ? parent.gameObject.scene : SceneManager.GetActiveScene(), false);
            return ledger != null && ledger.WasDeleted(PathOf(parent) + "/" + childName);
        }

        /// <summary>Same for a root object of the active scene.</summary>
        public static bool WasDeletedRoot(string name)
        {
            HandEditLedger ledger = FindLedger(SceneManager.GetActiveScene(), false);
            return ledger != null && ledger.WasDeleted(name);
        }

        // --------------------------------------------------------------- tracking

        private static UndoPropertyModification[] OnModifications(UndoPropertyModification[] modifications)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return modifications;
            }

            foreach (UndoPropertyModification modification in modifications)
            {
                Object target = modification.currentValue?.target;
                GameObject go = target is Component component ? component.gameObject : target as GameObject;
                if (go == null || !go.scene.IsValid() || EditorUtility.IsPersistent(go))
                {
                    continue;
                }

                Mark(go);
            }

            return modifications;
        }

        /// <summary>Marks the decor item <paramref name="go"/> belongs to (later in the frame).</summary>
        private static void Mark(GameObject go)
        {
            if (go == null || !go.scene.IsValid() || EditorUtility.IsPersistent(go))
            {
                return;
            }

            Transform item = ItemOf(go.transform);
            if (item == null || item.GetComponent<HandEdited>() != null || item.GetComponent<HandEditLedger>() != null)
            {
                return;
            }

            // Queued: components are not added inside an editor callback; the next editor
            // update does it (outside Play mode, so the marker is never lost with a play copy).
            if (!Pending.Contains(item))
            {
                Pending.Add(item);
            }
        }

        private static readonly List<Transform> Pending = new List<Transform>();

        private static void ProcessPending()
        {
            if (Pending.Count == 0 || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            foreach (Transform item in Pending)
            {
                if (item != null && item.GetComponent<HandEdited>() == null)
                {
                    Undo.AddComponent<HandEdited>(item.gameObject);
                    EditorSceneManager.MarkSceneDirty(item.gameObject.scene);
                }
            }

            Pending.Clear();
        }

        private static void OnChanges(ref ObjectChangeEventStream stream)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            for (int i = 0; i < stream.length; i++)
            {
                ObjectChangeKind kind = stream.GetEventType(i);
                if (kind == ObjectChangeKind.ChangeGameObjectOrComponentProperties)
                {
                    // Inspector and Scene-view edits (move, rotate, scale, any component field).
                    stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out ChangeGameObjectOrComponentPropertiesEventArgs change);
                    Object changed = EditorUtility.EntityIdToObject(change.entityId);
                    Mark(changed is Component component ? component.gameObject : changed as GameObject);
                    continue;
                }

                if (kind != ObjectChangeKind.DestroyGameObjectHierarchy)
                {
                    continue;
                }

                stream.GetDestroyGameObjectHierarchyEvent(i, out DestroyGameObjectHierarchyEventArgs args);
                if (!Paths.TryGetValue(args.entityId, out string path))
                {
                    continue;
                }

                HandEditLedger ledger = FindLedger(args.scene, true);
                if (ledger != null && ledger.Add(path))
                {
                    EditorUtility.SetDirty(ledger);
                    EditorSceneManager.MarkSceneDirty(args.scene);
                }
            }
        }

        /// <summary>Caches instance id -> path for tracked items; clears ledger entries that exist again.</summary>
        [InitializeOnLoadMethod]
        private static void HookRefresh()
        {
            EditorApplication.update += ProcessPending;
            EditorApplication.update += () =>
            {
                if (!pathsDirty || EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    return;
                }

                pathsDirty = false;
                Paths.Clear();
                for (int s = 0; s < SceneManager.sceneCount; s++)
                {
                    Scene scene = SceneManager.GetSceneAt(s);
                    if (!scene.isLoaded)
                    {
                        continue;
                    }

                    foreach (GameObject root in scene.GetRootGameObjects())
                    {
                        Collect(root.transform, false);
                    }

                    HandEditLedger ledger = FindLedger(scene, false);
                    if (ledger != null)
                    {
                        var back = new List<string>();
                        foreach (string path in ledger.Deleted)
                        {
                            if (Find(scene, path) != null)
                            {
                                back.Add(path);
                            }
                        }

                        foreach (string path in back)
                        {
                            ledger.Remove(path);
                            EditorUtility.SetDirty(ledger);
                        }
                    }
                }
            };
        }

        private static void Collect(Transform t, bool insideContainer)
        {
            bool isContainer = System.Array.IndexOf(Containers, t.name) >= 0;
            bool isRootItem = t.parent == null && System.Array.IndexOf(RootItems, t.name) >= 0;

            if (insideContainer || isRootItem)
            {
                Paths[t.gameObject.GetEntityId()] = PathOf(t);
            }

            foreach (Transform child in t)
            {
                Collect(child, insideContainer || isContainer);
            }
        }

        /// <summary>
        /// The decor item an edited object belongs to: the ancestor that sits directly inside the
        /// innermost container group (Dressing/Furn_X, Atmosphere/Windows/Window_Y,
        /// Menu_Set/Mansion/Window_Z), or a tracked root object. Null when not decor.
        /// </summary>
        private static Transform ItemOf(Transform t)
        {
            Transform previous = null;
            Transform previous2 = null;
            for (Transform current = t; current != null; current = current.parent)
            {
                if (current.parent == null && System.Array.IndexOf(RootItems, current.name) >= 0)
                {
                    return current;
                }

                if (System.Array.IndexOf(Containers, current.name) >= 0)
                {
                    if (previous == null)
                    {
                        return current; // The group itself was edited.
                    }

                    // Atmosphere and Menu_Set hold groups (Windows, Mansion...); items sit one deeper.
                    bool grouped = current.name != "Dressing";
                    return grouped && previous2 != null ? previous2 : previous;
                }

                previous2 = previous;
                previous = current;
            }

            return null;
        }

        // ----------------------------------------------------------------- helpers

        internal static string PathOf(Transform t)
        {
            if (t == null)
            {
                return string.Empty;
            }

            string path = t.name;
            for (Transform p = t.parent; p != null; p = p.parent)
            {
                path = p.name + "/" + path;
            }

            return path;
        }

        private static Transform Find(Scene scene, string path)
        {
            int slash = path.IndexOf('/');
            string rootName = slash < 0 ? path : path.Substring(0, slash);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != rootName)
                {
                    continue;
                }

                Transform found = slash < 0 ? root.transform : root.transform.Find(path.Substring(slash + 1));
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static HandEditLedger FindLedger(Scene scene, bool create)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == LedgerName && root.TryGetComponent(out HandEditLedger existing))
                {
                    return existing;
                }
            }

            if (!create)
            {
                return null;
            }

            var holder = new GameObject(LedgerName) { tag = "EditorOnly" };
            SceneManager.MoveGameObjectToScene(holder, scene);
            return holder.AddComponent<HandEditLedger>();
        }
    }
}
