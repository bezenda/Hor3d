using System.Reflection;
using AshenWoods.Enemy;
using AshenWoods.Interaction;
using AshenWoods.Player;
using AshenWoods.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace AshenWoods.GameManagement
{
    /// <summary>
    /// Test cheats, in the editor and development builds only. Creates itself in every level.
    ///
    ///   F1  panel (frees the cursor)      F2  god mode (cannot be caught)
    ///   F3  noclip fly (WASD, Space/Ctrl up/down, Shift fast)
    ///   F4  freeze / unfreeze enemies     F5  stun enemies      F6  game speed x1 / x3
    ///   F7  full light everywhere (flat bright ambient, no fog)
    ///
    /// The panel also teleports to rooms, gives keys, restores power, starts the finale, heals and
    /// shows what each enemy is thinking.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DebugMode : MonoBehaviour
    {
        private bool panelOpen;
        private bool noclip;
        private bool enemiesFrozen;
        private bool infiniteStamina;
        private bool infiniteTorch;
        private bool fullBright;
        private bool savedLighting;
        private UnityEngine.Rendering.AmbientMode savedAmbientMode;
        private Color savedAmbient;
        private bool savedFog;
        private Light debugSun;
        private float speed = 1f;
        private Vector2 scroll;
        private Rect window = new Rect(20f, 20f, 380f, 640f);
        private float fps;

        private PlayerMotor motor;
        private PlayerInputReader input;
        private PlayerFlashlight torch;
        private CharacterController body;
        private EnemyBrain[] enemies = new EnemyBrain[0];
        private bool inputWasEnabled = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Application.isEditor && !Debug.isDebugBuild)
            {
                return;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Ensure();

        private static void Ensure()
        {
            if (FindAnyObjectByType<DebugMode>() == null && FindAnyObjectByType<PlayerMotor>() != null)
            {
                new GameObject("DebugMode").AddComponent<DebugMode>();
            }
        }

        private void Start()
        {
            motor = FindAnyObjectByType<PlayerMotor>();
            input = FindAnyObjectByType<PlayerInputReader>();
            torch = FindAnyObjectByType<PlayerFlashlight>();
            body = motor != null ? motor.GetComponent<CharacterController>() : null;
            enemies = FindObjectsByType<EnemyBrain>(FindObjectsInactive.Include);
        }

        private void OnDestroy()
        {
            SetFullBright(false);
            Time.timeScale = Time.timeScale > 0f ? 1f : Time.timeScale;
        }

        // ------------------------------------------------------------------ per frame

        private void Update()
        {
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime), 0.05f);
            Keyboard keys = Keyboard.current;
            if (keys != null)
            {
                if (keys.f1Key.wasPressedThisFrame) SetPanel(!panelOpen);
                if (keys.f2Key.wasPressedThisFrame) DebugFlags.God = !DebugFlags.God;
                if (keys.f3Key.wasPressedThisFrame) SetNoclip(!noclip);
                if (keys.f4Key.wasPressedThisFrame) SetFrozen(!enemiesFrozen);
                if (keys.f5Key.wasPressedThisFrame) StunEnemies();
                if (keys.f6Key.wasPressedThisFrame) SetSpeed(speed > 1f ? 1f : 3f);
                if (keys.f7Key.wasPressedThisFrame) SetFullBright(!fullBright);
            }

            if (infiniteStamina && motor != null && motor.Stamina < 1f && !motor.HasBrokenLegs)
            {
                motor.RefillStamina();
            }

            if (infiniteTorch && torch != null && torch.Charge < 0.99f)
            {
                torch.AddCharge(1f);
            }

            if (noclip)
            {
                Fly(keys);
            }
        }

        private void Fly(Keyboard keys)
        {
            if (motor == null || keys == null || panelOpen)
            {
                return;
            }

            Transform view = Camera.main != null ? Camera.main.transform : motor.transform;
            Vector3 move = Vector3.zero;
            if (keys.wKey.isPressed) move += view.forward;
            if (keys.sKey.isPressed) move -= view.forward;
            if (keys.dKey.isPressed) move += view.right;
            if (keys.aKey.isPressed) move -= view.right;
            if (keys.spaceKey.isPressed) move += Vector3.up;
            if (keys.leftCtrlKey.isPressed) move -= Vector3.up;
            float rate = keys.leftShiftKey.isPressed ? 18f : 6f;
            motor.transform.position += move.normalized * rate * Time.unscaledDeltaTime;
        }

        private void LateUpdate()
        {
            if (!fullBright)
            {
                return;
            }

            // House power re-sets the ambient when it changes: hold the bright values every frame.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.8f, 0.8f, 0.82f);
            RenderSettings.fog = false;
        }

        /// <summary>Everything lit evenly: flat bright ambient, no fog, a shadowless sun. F7.</summary>
        private void SetFullBright(bool on)
        {
            if (on == fullBright)
            {
                return;
            }

            fullBright = on;
            if (on)
            {
                savedAmbientMode = RenderSettings.ambientMode;
                savedAmbient = RenderSettings.ambientLight;
                savedFog = RenderSettings.fog;
                savedLighting = true;
                if (debugSun == null)
                {
                    var sun = new GameObject("DebugSun");
                    sun.transform.SetParent(transform, false);
                    sun.transform.rotation = Quaternion.Euler(55f, 35f, 0f);
                    debugSun = sun.AddComponent<Light>();
                    debugSun.type = LightType.Directional;
                    debugSun.shadows = LightShadows.None;
                    debugSun.intensity = 1.1f;
                }

                debugSun.enabled = true;
                LateUpdate();
                return;
            }

            if (debugSun != null)
            {
                debugSun.enabled = false;
            }

            if (savedLighting)
            {
                RenderSettings.ambientMode = savedAmbientMode;
                RenderSettings.ambientLight = savedAmbient;
                RenderSettings.fog = savedFog;
            }
        }

        // ------------------------------------------------------------------ switches

        private void SetPanel(bool open)
        {
            panelOpen = open;
            if (input != null)
            {
                if (open)
                {
                    inputWasEnabled = input.InputEnabled;
                    input.SetInputEnabled(false);
                }
                else
                {
                    input.SetInputEnabled(inputWasEnabled);
                }
            }

            PlayerInputReader.ApplyCursorLock(!open);
        }

        private void SetNoclip(bool on)
        {
            if (motor == null)
            {
                return;
            }

            noclip = on;
            motor.enabled = !on;
            if (body != null)
            {
                body.enabled = !on;
            }

            if (!on)
            {
                Physics.SyncTransforms();
            }
        }

        private void SetFrozen(bool frozen)
        {
            enemiesFrozen = frozen;
            foreach (EnemyBrain enemy in enemies)
            {
                if (enemy != null)
                {
                    enemy.enabled = !frozen;
                }
            }
        }

        private void StunEnemies()
        {
            foreach (EnemyBrain enemy in enemies)
            {
                if (enemy != null && enemy.isActiveAndEnabled)
                {
                    enemy.Daze(0f, 10f);
                }
            }
        }

        private void SetSpeed(float value)
        {
            speed = value;
            if (Time.timeScale > 0f)
            {
                Time.timeScale = value;
            }
        }

        // ------------------------------------------------------------------ actions

        private IInteractionActor Actor => FindAnyObjectByType<PlayerInteractor>();

        private void GiveAllKeys()
        {
            PlayerKeyHolder holder = FindAnyObjectByType<PlayerKeyHolder>();
            if (holder == null)
            {
                return;
            }

            FieldInfo field = typeof(Door).GetField("requiredKeyId", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (Door door in FindObjectsByType<Door>(FindObjectsInactive.Include))
            {
                if (field != null && field.GetValue(door) is string id && !string.IsNullOrEmpty(id))
                {
                    holder.AddKey(id);
                }
            }
        }

        private void RestorePower()
        {
            IInteractionActor actor = Actor;
            foreach (PuzzleController puzzle in FindObjectsByType<PuzzleController>())
            {
                foreach (PuzzleSwitch toggle in puzzle.Switches)
                {
                    if (!puzzle.IsSolved && toggle != null && !toggle.IsOn)
                    {
                        toggle.Interact(actor);
                    }
                }
            }
        }

        private void StartFinale()
        {
            RestorePower();
            EscapeExit exit = FindAnyObjectByType<EscapeExit>();
            if (exit != null && !exit.OpeningHasStarted)
            {
                exit.Interact(Actor);
            }
        }

        private void TeleportTo(Vector3 point, bool upper)
        {
            if (motor == null)
            {
                return;
            }

            // Down onto the floor, then sideways until the body fits (machines stand mid-room).
            float radius = body != null ? body.radius : 0.35f;
            float height = body != null ? body.height : 1.8f;
            for (int ring = 0; ring < 8; ring++)
            {
                for (int k = 0; k < Mathf.Max(1, ring * 6); k++)
                {
                    float angle = k * Mathf.PI * 2f / Mathf.Max(1, ring * 6);
                    Vector3 probe = point + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ring * 1.5f;
                    probe.y = point.y + (upper ? 2.5f : 3.5f);
                    if (!Physics.Raycast(probe, Vector3.down, out RaycastHit floor, 6f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }

                    Vector3 feet = floor.point + Vector3.up * 0.05f;
                    Vector3 a = feet + Vector3.up * (radius + 0.05f);
                    Vector3 b = feet + Vector3.up * (height - radius);
                    if (!Physics.CheckCapsule(a, b, radius, ~0, QueryTriggerInteraction.Ignore))
                    {
                        Place(feet);
                        return;
                    }
                }
            }

            Place(point + Vector3.up * 0.1f);
        }

        private void Place(Vector3 feet)
        {
            bool wasOn = body != null && body.enabled;
            if (body != null) body.enabled = false;
            motor.Teleport(feet, motor.transform.rotation);
            if (body != null) body.enabled = wasOn;
            Physics.SyncTransforms();
        }

        // ------------------------------------------------------------------ panel

        private void OnGUI()
        {
            GUI.Label(new Rect(Screen.width - 230f, 6f, 224f, 22f),
                $"DEBUG  F1 panel   {fps:0} fps" + (DebugFlags.God ? "  GOD" : "") + (noclip ? "  NOCLIP" : ""));
            if (panelOpen)
            {
                window = GUI.Window(73019, window, DrawWindow, "Debug (F1 to close)");
            }
        }

        private void DrawWindow(int id)
        {
            scroll = GUILayout.BeginScrollView(scroll);

            GUILayout.Label("<b>Player</b>");
            DebugFlags.God = GUILayout.Toggle(DebugFlags.God, " God mode - cannot be caught (F2)");
            DebugFlags.Invisible = GUILayout.Toggle(DebugFlags.Invisible, " Invisible - enemies cannot see you");
            DebugFlags.Deaf = GUILayout.Toggle(DebugFlags.Deaf, " Deaf enemies - they hear nothing");
            bool fly = GUILayout.Toggle(noclip, " Noclip fly (F3)");
            if (fly != noclip) SetNoclip(fly);
            infiniteStamina = GUILayout.Toggle(infiniteStamina, " Infinite stamina");
            infiniteTorch = GUILayout.Toggle(infiniteTorch, " Infinite torch battery");
            if (GUILayout.Button("Heal (legs, stamina)") && motor != null) motor.RefillStamina();

            bool bright = GUILayout.Toggle(fullBright, " Full light everywhere (F7)");
            if (bright != fullBright) SetFullBright(bright);

            GUILayout.Space(6f);
            GUILayout.Label("<b>Game</b>");
            GUILayout.BeginHorizontal();
            foreach (float s in new[] { 0.25f, 1f, 3f })
            {
                if (GUILayout.Button((Mathf.Approximately(speed, s) ? "> " : "") + "x" + s)) SetSpeed(s);
            }

            GUILayout.EndHorizontal();
            if (GUILayout.Button("Give every key")) GiveAllKeys();
            if (GUILayout.Button("Restore power (solve breakers)")) RestorePower();
            if (GUILayout.Button("Start the finale (open the bay)")) StartFinale();
            if (GUILayout.Button("Restart level"))
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }

            GUILayout.Space(6f);
            GUILayout.Label("<b>Enemies</b>");
            bool freeze = GUILayout.Toggle(enemiesFrozen, " Frozen (F4)");
            if (freeze != enemiesFrozen) SetFrozen(freeze);
            if (GUILayout.Button("Stun all for 10 s (F5)")) StunEnemies();
            foreach (EnemyBrain enemy in enemies)
            {
                if (enemy == null) continue;
                float distance = motor != null ? Vector3.Distance(enemy.transform.position, motor.transform.position) : 0f;
                GUILayout.Label($"{enemy.name}: {enemy.State}  alert {enemy.Alertness:0.00}  {distance:0.0} m" +
                                (enemy.CanSeeTarget ? "  SEES YOU" : "") + (enemy.KnowsTargetPosition ? "  KNOWS" : "") +
                                (enemy.IsStunned ? "  stunned" : ""));
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Go to it") && motor != null)
                {
                    TeleportTo(enemy.transform.position - enemy.transform.forward * 4f, enemy.transform.position.y > 4f);
                }

                if (GUILayout.Button("Bring it here") && motor != null)
                {
                    var cc = enemy.GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;
                    enemy.transform.position = motor.transform.position + motor.transform.forward * 6f;
                    if (cc != null) cc.enabled = true;
                    Physics.SyncTransforms();
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6f);
            GUILayout.Label("<b>Teleport</b>");
            RoomAtlas atlas = RoomAtlas.Instance;
            if (motor != null && atlas != null)
            {
                GUILayout.Label($"At {motor.transform.position.x:0.0}, {motor.transform.position.y:0.0}, {motor.transform.position.z:0.0}  " +
                                atlas.RoomAt(motor.transform.position));
                foreach (string room in atlas.RoomNames)
                {
                    if (!string.IsNullOrEmpty(room) && GUILayout.Button(room)
                        && atlas.TryGetRoomCentre(room, out Vector3 centre, out bool upper))
                    {
                        TeleportTo(centre, upper);
                    }
                }
            }

            EscapeExit exit = FindAnyObjectByType<EscapeExit>();
            if (exit != null && GUILayout.Button("Exit door"))
            {
                TeleportTo(exit.transform.position - exit.transform.forward * 2f, false);
            }

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }
    }
}
