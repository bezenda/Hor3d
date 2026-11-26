using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using AshenWoods.Interaction;
using AshenWoods.Player;
using AshenWoods.UI;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Stage 8 authoring: throwable distractions.
    ///
    ///  - PlayerThrower on the player, with a ThrowHand child under the camera.
    ///  - Glass bottles (shatter, loud, used up) and tin cans (clatter, reusable) around the level.
    ///  - A "RMB / G  THROW ..." line with a charge bar above the battery bar (ThrowableUI).
    ///  - The pause screen's controls line gains "RMB throw".
    ///
    /// Additive and idempotent like every other builder: found by name, only missing pieces are
    /// created, references re-pointed every run. A bottle that was moved or deleted by hand is
    /// never put back.
    /// </summary>
    public static class Stage8ThrowableBuilder
    {
        private const string ItemsRootName = "Items";
        private const string HandName = "ThrowHand";
        private const string AtmosphereCanvasPath = "UI/AtmosphereCanvas";
        private const string HintName = "ThrowHint";

        private const string Stage7ControlsLine =
            "WASD  move      Shift  sprint      C  crouch      E  use      F  flashlight      Mouse  look";
        private const string ControlsLine =
            "WASD  move      Shift  sprint      C  crouch      E  use      F  flashlight      RMB / G  throw      Mouse  look";

        private static readonly Color TextColor = new Color(0.93f, 0.9f, 0.85f, 0.85f);

        private struct Spot
        {
            public string Name;
            public Vector3 Surface; // Where the bottom of the item rests.
            public bool Glass;

            public Spot(string name, Vector3 surface, bool glass)
            {
                Name = name;
                Surface = surface;
                Glass = glass;
            }
        }

        /// <summary>
        /// One bottle within reach of the start (foyer sideboard), the rest spread so there is
        /// always something to throw on the way to each objective.
        /// </summary>
        private static readonly Spot[] HouseSpots =
        {
            new Spot("Throwable_Bottle_1", new Vector3(-1.65f, 0.8f, 6.2f), true),   // foyer sideboard
            new Spot("Throwable_Bottle_2", new Vector3(-6f, 0.6f, 4f), true),        // living-room crate
            new Spot("Throwable_Bottle_3", new Vector3(-8.8f, 0.9f, -7.65f), true),  // kitchen counter
            new Spot("Throwable_Bottle_4", new Vector3(4.5f, 0.9f, -7.65f), true),   // dining sideboard
            new Spot("Throwable_Bottle_5", new Vector3(2.6f, 3.2f, -7.4f), true),    // study, by the breaker
            new Spot("Throwable_Bottle_6", new Vector3(-6f, 3.79f, -6.6f), true),    // master bed (hiding bed, blanket top)
            new Spot("Throwable_Bottle_7", new Vector3(7.6f, 0.8f, 2f), true),       // library table
            new Spot("Throwable_Can_1", new Vector3(-3f, 0f, -1.7f), false),         // ground corridor
            new Spot("Throwable_Can_2", new Vector3(6f, 3.2f, -1.7f), false),        // upper corridor
            new Spot("Throwable_Can_3", new Vector3(10f, 0f, -3f), false)            // utility room
        };

        private static readonly Spot[] PlaygroundSpots =
        {
            new Spot("Throwable_Bottle_1", new Vector3(-2.5f, 0f, -1.5f), true),
            new Spot("Throwable_Can_1", new Vector3(1.6f, 0f, 4.2f), false)
        };

        // ------------------------------------------------------------------- menus

        [MenuItem("Ashen Woods/Stages/Stage 8 - Throwable Distractions", false, 27)]
        private static void AddStage8Menu()
        {
            if (!HouseBuilder.IsLevelScene())
            {
                EditorUtility.DisplayDialog(
                    "Open a level scene first",
                    "This tool only edits:\n" + HouseBuilder.HouseScenePath + "\n" +
                    Stage1PlaygroundBuilder.ScenePath + "\n\n" +
                    "Nothing was changed. Open one of those scenes from Assets/Scenes, then run the menu item again.",
                    "OK");
                return;
            }

            int added = HouseBuilder.EnsureLevelPieces()
                        + Stage2InteractionBuilder.EnsureStage2Pieces()
                        + Stage3EnemyBuilder.EnsureStage3Pieces()
                        + Stage4MenuBuilder.EnsureStage4Pieces()
                        + Stage5NoiseBuilder.EnsureStage5Pieces()
                        + Stage6ObjectiveBuilder.EnsureStage6Pieces()
                        + Stage7FlowBuilder.EnsureStage7Pieces()
                        + EnsureStage8Pieces()
                        + Stage9PolishBuilder.EnsureStage9Pieces()
                        + Stage10InterfaceBuilder.EnsureStage10Pieces();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log(added > 0
                ? $"Added {added} Stage 8 piece(s). Save the scene with Ctrl+S, then press Play.\n" +
                  "Test: pick up the bottle on the foyer sideboard (E), hold RMB or G and release to throw. " +
                  "The enemy walks to where it shattered."
                : "Stage 8 is already complete in this scene - only references were refreshed.");
        }

        // ------------------------------------------------------------------- build

        public static int EnsureStage8Pieces()
        {
            int added = 0;
            added += EnsureThrower();
            added += EnsureItems();
            added += EnsureHud();
            UpdatePauseControls();
            return added;
        }

        private static int EnsureThrower()
        {
            PlayerMotor motor = Object.FindAnyObjectByType<PlayerMotor>();
            if (motor == null)
            {
                Debug.LogWarning("Stage 8: no PlayerMotor in this scene, so nobody can throw anything.");
                return 0;
            }

            int added = 0;
            Camera camera = motor.GetComponentInChildren<Camera>(true);
            Transform aim = camera != null ? camera.transform : motor.transform;

            Transform hand = aim.Find(HandName);
            if (hand == null)
            {
                hand = new GameObject(HandName).transform;
                hand.SetParent(aim, false);
                // Left hand, low in view: the flashlight is in the right one.
                hand.localPosition = new Vector3(-0.22f, -0.2f, 0.42f);
                hand.localRotation = Quaternion.Euler(10f, 0f, 8f);
                added++;
            }

            PlayerThrower thrower = motor.GetComponent<PlayerThrower>();
            if (thrower == null)
            {
                thrower = motor.gameObject.AddComponent<PlayerThrower>();
                added++;
            }

            Set(thrower, "input", motor.GetComponent<PlayerInputReader>());
            Set(thrower, "aim", aim);
            Set(thrower, "hand", hand);
            Set(thrower, "hiding", motor.GetComponent<PlayerHidingController>());
            Set(thrower, "body", motor.GetComponent<CharacterController>());
            return added;
        }

        private static int EnsureItems()
        {
            GameObject items = GameObject.Find(ItemsRootName);
            if (items == null)
            {
                items = new GameObject(ItemsRootName);
            }

            Material glass = Stage1PlaygroundBuilder.GetOrCreateMaterial(
                "Placeholder_Glass", new Color(0.16f, 0.42f, 0.24f));
            Material tin = Stage1PlaygroundBuilder.GetOrCreateMaterial(
                "Placeholder_Tin", new Color(0.62f, 0.63f, 0.66f));

            Spot[] spots = HouseBuilder.IsHouseScene() ? HouseSpots : PlaygroundSpots;
            int added = 0;

            foreach (Spot spot in spots)
            {
                if (items.transform.Find(spot.Name) != null)
                {
                    continue;
                }

                if (spot.Glass)
                {
                    CreateBottle(items.transform, spot, glass);
                }
                else
                {
                    CreateCan(items.transform, spot, tin);
                }

                added++;
            }

            return added;
        }

        private static void CreateBottle(Transform parent, Spot spot, Material material)
        {
            const float height = 0.3f;
            GameObject root = CreateItemRoot(parent, spot, height, 0.045f, new Vector3(0.2f, 0.34f, 0.2f), 0.35f);

            var visuals = new GameObject("Visuals").transform;
            visuals.SetParent(root.transform, false);
            AddPart(visuals, PrimitiveType.Cylinder, new Vector3(0f, -0.035f, 0f), new Vector3(0.08f, 0.115f, 0.08f), material);
            AddPart(visuals, PrimitiveType.Cylinder, new Vector3(0f, 0.115f, 0f), new Vector3(0.032f, 0.035f, 0.032f), material);

            ThrowableItem item = root.AddComponent<ThrowableItem>();
            Configure(item, "bottle", true, 0.75f);
        }

        private static void CreateCan(Transform parent, Spot spot, Material material)
        {
            const float height = 0.13f;
            GameObject root = CreateItemRoot(parent, spot, height, 0.04f, new Vector3(0.2f, 0.2f, 0.2f), 0.2f);

            var visuals = new GameObject("Visuals").transform;
            visuals.SetParent(root.transform, false);
            AddPart(visuals, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.075f, 0.065f, 0.075f), material);

            ThrowableItem item = root.AddComponent<ThrowableItem>();
            Configure(item, "tin can", false, 0.55f);
        }

        /// <summary>
        /// Root with the physics body, a thin solid capsule for collisions and a larger trigger
        /// box that only the interaction ray uses, so a small object is easy to aim at.
        /// </summary>
        private static GameObject CreateItemRoot(
            Transform parent, Spot spot, float height, float radius, Vector3 aimBox, float mass)
        {
            var root = new GameObject(spot.Name);
            root.transform.SetParent(parent, false);
            root.transform.position = spot.Surface + Vector3.up * (height * 0.5f + 0.005f);

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = mass;
            body.linearDamping = 0.05f;
            body.angularDamping = 0.4f;

            CapsuleCollider solid = root.AddComponent<CapsuleCollider>();
            solid.direction = 1;
            solid.radius = radius;
            solid.height = height;

            BoxCollider aim = root.AddComponent<BoxCollider>();
            aim.isTrigger = true;
            aim.size = aimBox;

            return root;
        }

        private static void AddPart(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = type.ToString();
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void Configure(ThrowableItem item, string displayName, bool glass, float loudness)
        {
            var serialized = new SerializedObject(item);
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("breaksOnImpact").boolValue = glass;
            serialized.FindProperty("impactLoudness").floatValue = loudness;
            serialized.FindProperty("shardCount").intValue = glass ? 7 : 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // --------------------------------------------------------------------- HUD

        private static int EnsureHud()
        {
            GameObject canvas = GameObject.Find(AtmosphereCanvasPath);
            if (canvas == null)
            {
                Debug.LogWarning("Stage 8: no UI/AtmosphereCanvas (Stage 7), so the throw hint has nowhere to go.");
                return 0;
            }

            int added = 0;
            Transform hint = canvas.transform.Find(HintName);
            if (hint == null)
            {
                hint = CreateHint(canvas.transform).transform;
                added++;
            }

            ThrowableUI ui = canvas.GetComponent<ThrowableUI>();
            if (ui == null)
            {
                ui = canvas.AddComponent<ThrowableUI>();
                added++;
            }

            Transform fill = hint.Find("Charge/Fill");
            Set(ui, "thrower", Object.FindAnyObjectByType<PlayerThrower>());
            Set(ui, "root", hint.gameObject);
            Set(ui, "label", hint.Find("Label") != null ? hint.Find("Label").GetComponent<Text>() : null);
            Set(ui, "chargeFill", fill != null ? fill.GetComponent<RectTransform>() : null);
            return added;
        }

        /// <summary>Bottom-left, above the battery bar: a label and a thin charge bar.</summary>
        private static GameObject CreateHint(Transform canvas)
        {
            var hint = new GameObject(HintName, typeof(RectTransform));
            hint.transform.SetParent(canvas, false);
            RectTransform rect = hint.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(34f, 84f);
            rect.sizeDelta = new Vector2(260f, 6f);

            var charge = new GameObject("Charge", typeof(RectTransform));
            charge.transform.SetParent(hint.transform, false);
            Image background = charge.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.5f);
            background.raycastTarget = false;
            RectTransform chargeRect = background.rectTransform;
            chargeRect.anchorMin = Vector2.zero;
            chargeRect.anchorMax = Vector2.one;
            chargeRect.offsetMin = Vector2.zero;
            chargeRect.offsetMax = Vector2.zero;

            var fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(charge.transform, false);
            Image fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.9f, 0.55f, 0.3f, 0.95f);
            fillImage.raycastTarget = false;
            RectTransform fillRect = fillImage.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            Text label = Stage4MenuBuilder.CreateText(hint.transform, "Label", "ЛКМ  БРОСОК",
                Vector2.zero, new Vector2(500f, 30f), 20, TextColor);
            label.alignment = TextAnchor.LowerLeft;
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.pivot = new Vector2(0f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 6f);

            hint.SetActive(false);
            return hint;
        }

        private static void UpdatePauseControls()
        {
            GameObject controls = GameObject.Find("UI/MenuCanvas/PausePanel/Controls");
            if (controls == null)
            {
                // The pause panel is inactive while editing, and GameObject.Find skips inactive objects.
                GameObject ui = GameObject.Find("UI");
                Transform found = ui != null ? ui.transform.Find("MenuCanvas/PausePanel/Controls") : null;
                controls = found != null ? found.gameObject : null;
            }

            Text text = controls != null ? controls.GetComponent<Text>() : null;
            if (text != null && text.text == Stage7ControlsLine)
            {
                text.text = ControlsLine;
                EditorUtility.SetDirty(text);
            }
        }

        private static void Set(Object target, string field, Object value)
        {
            Stage1PlaygroundBuilder.SetObjectReference(target, field, value);
        }
    }
}
