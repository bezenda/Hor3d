using UnityEngine;
using AshenWoods.World;

namespace AshenWoods.Interaction
{
    /// <summary>
    /// A code lock beside a door. Using it opens the keypad screen (KeypadUI); the right code unlocks the door,
    /// a wrong one sets off a loud alarm the enemy comes to and locks the panel for a few seconds (2026-11-25, owner:
    /// the code could be guessed in peace). Its LED (children "Led" light, "LedLens" mesh) is red while locked,
    /// flashes during the alarm and turns green when open. The code is drawn once per run (RunRandom), the same after
    /// a checkpoint retry, and its digits are offered to notes as {code1}, {code2}, ... (TextTokens) - each note
    /// can carry one digit, so the player has to find them all.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Keypad : MonoBehaviour, IInteractable
    {
        [SerializeField] private Door door;

        [Tooltip("Digits in the code.")]
        [SerializeField, Range(2, 6)] private int digits = 3;

        [Tooltip("Fixed code; empty draws a random one per run.")]
        [SerializeField] private string fixedCode = "";

        [Tooltip("Token prefix for notes: 'code' gives {code1}, {code2}, ...")]
        [SerializeField] private string tokenPrefix = "code";

        [SerializeField] private string prompt = "Ввести код";
        [SerializeField] private string openPrompt = "Кодовый замок (открыт)";
        [SerializeField] private string unlockedMessage = "Замок щёлкает и открывается.";
        [SerializeField] private string wrongMessage = "Неверный код — завыла тревога! Оно услышит.";

        [Tooltip("Loudness of the alarm on a wrong code (a noise the enemy hears and comes to).")]
        [SerializeField, Range(0f, 1f)] private float wrongNoise = 0.9f;

        [Tooltip("Seconds the panel refuses any code after a wrong one.")]
        [SerializeField, Min(0f)] private float lockoutSeconds = 5f;

        [Tooltip("Volume of the alarm recording at the panel.")]
        [SerializeField, Range(0f, 1f)] private float alarmVolume = 1f;

        private string code;
        private float lockedUntil = -1f;
        private Light led;
        private Renderer ledLens;
        private AudioSource alarm;
        private MaterialPropertyBlock ledBlock;
        private static readonly Color LedRed = new Color(1f, 0.12f, 0.08f);
        private static readonly Color LedGreen = new Color(0.2f, 1f, 0.35f);

        /// <summary>True for a few seconds after a wrong code: the panel takes nothing.</summary>
        public bool IsLockedOut => Time.time < lockedUntil;

        /// <summary>Seconds left of the lockout.</summary>
        public float LockoutRemaining => Mathf.Max(0f, lockedUntil - Time.time);

        public int Digits => digits;
        public bool IsOpen => door == null || !door.IsLocked;

        /// <summary>The code for this run.</summary>
        public string Code
        {
            get
            {
                if (string.IsNullOrEmpty(code))
                {
                    if (!string.IsNullOrEmpty(fixedCode))
                    {
                        code = fixedCode;
                    }
                    else
                    {
                        System.Random random = RunRandom.For("keypad:" + name);
                        var chars = new char[digits];
                        for (int i = 0; i < digits; i++)
                        {
                            chars[i] = (char)('0' + random.Next(10));
                        }

                        code = new string(chars);
                    }
                }

                return code;
            }
        }

        private void Awake()
        {
            for (int i = 0; i < digits; i++)
            {
                int index = i;
                TextTokens.Register(tokenPrefix + (i + 1), () => index < Code.Length ? Code[index].ToString() : "?");
            }

            TextTokens.Register(tokenPrefix, () => Code);

            Transform ledTransform = transform.Find("Led");
            led = ledTransform != null ? ledTransform.GetComponent<Light>() : GetComponentInChildren<Light>(true);
            Transform lens = transform.Find("LedLens");
            ledLens = lens != null ? lens.GetComponent<Renderer>() : null;
            ledBlock = new MaterialPropertyBlock();

            alarm = gameObject.AddComponent<AudioSource>();
            alarm.playOnAwake = false;
            alarm.spatialBlend = 0.9f;
            alarm.rolloffMode = AudioRolloffMode.Linear;
            alarm.minDistance = 2f;
            alarm.maxDistance = 45f;
            alarm.priority = 40;
        }

        private void Update()
        {
            // Red while locked, blinking hard during the alarm, green once open.
            Color color = IsOpen ? LedGreen : LedRed;
            float level = IsOpen ? 1f : IsLockedOut ? (Mathf.Repeat(Time.time * 4f, 1f) < 0.5f ? 1.6f : 0.05f) : 0.8f;
            if (led != null)
            {
                led.color = color;
                led.intensity = 0.06f * level; // a glint on the panel, not a lamp on the wall
            }

            if (ledLens != null)
            {
                ledBlock ??= new MaterialPropertyBlock(); // a script reload in Play mode skips Awake
                ledLens.GetPropertyBlock(ledBlock);
                // The lens is unlit: its colour is its glow (HDR above 1 blooms).
                Color glow = color * (0.4f + 1.8f * level);
                glow.a = 1f;
                ledBlock.SetColor("_BaseColor", glow);
                ledLens.SetPropertyBlock(ledBlock);
            }
        }

        public bool IsAvailable => !IsOpen;

        public string GetPrompt(IInteractionActor actor) => IsOpen ? openPrompt : prompt;

        public string Interact(IInteractionActor actor)
        {
            if (IsOpen)
            {
                return null;
            }

            AshenWoods.UI.KeypadUI.Open(this);
            return null;
        }

        /// <summary>Called by the keypad screen with what was typed. Returns the line to show.</summary>
        public string Submit(string typed)
        {
            if (IsOpen)
            {
                return unlockedMessage;
            }

            if (IsLockedOut)
            {
                return null; // KeypadUI says how long; nothing is checked meanwhile.
            }

            if (typed == Code)
            {
                if (door != null)
                {
                    door.Unlock();
                }

                GameLog.Info($"[Keypad] {name} opened with the right code.", this);
                return unlockedMessage;
            }

            NoiseSystem.Emit(transform.position, NoiseCategory.Other, wrongNoise, this);
            lockedUntil = Time.time + lockoutSeconds;
            AudioClip clip = AshenWoods.Audio.SoundBank.Get(AshenWoods.Audio.GameSound.KeypadAlarm);
            if (clip != null)
            {
                alarm.clip = clip;
                alarm.volume = alarmVolume;
                alarm.loop = true;
                alarm.Play();
                CancelInvoke(nameof(StopAlarm));
                Invoke(nameof(StopAlarm), Mathf.Max(clip.length, Mathf.Min(lockoutSeconds, clip.length * 3f)));
            }

            GameLog.Info($"[Keypad] {name}: wrong code '{typed}'. Alarm.", this);
            return wrongMessage;
        }

        private void StopAlarm()
        {
            if (alarm != null)
            {
                alarm.loop = false;
                alarm.Stop();
            }
        }
    }
}
