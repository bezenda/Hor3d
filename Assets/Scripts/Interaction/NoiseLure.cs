using System.Collections.Generic;
using UnityEngine;
using AshenWoods.Audio;
using AshenWoods.World;

namespace AshenWoods.Interaction
{
    /// <summary>
    /// A distraction the player sets off on purpose: a steam release valve. A short delay (time to get away),
    /// then it vents loudly for a few seconds - the enemy hears it like any other noise and goes to look.
    /// It has to recharge before it can be used again, and runs out after a few uses, so it is a tool to plan
    /// with, not a button to hold the enemy in place.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NoiseLure : MonoBehaviour, IInteractable
    {
        [SerializeField] private string prompt = "Открыть сброс пара";
        [SerializeField] private string armedMessage = "Вентиль дрожит. Давление растёт — отойди подальше.";
        [SerializeField] private string busyPrompt = "Сброс пара (стравливается)";
        [SerializeField] private string rechargePrompt = "Сброс пара (давление ещё не набралось)";
        [SerializeField] private string spentPrompt = "Сброс пара (пусто)";

        [Tooltip("Seconds between turning it and the steam (time to move away).")]
        [SerializeField, Min(0f)] private float delaySeconds = 3f;

        [Tooltip("Seconds it vents.")]
        [SerializeField, Min(0.5f)] private float ventSeconds = 6f;

        [Tooltip("Seconds between the noises it reports while venting.")]
        [SerializeField, Min(0.3f)] private float noiseInterval = 1.5f;

        [Tooltip("Loudness of each noise (0-1). Steam at 0.85 carries across a hall.")]
        [SerializeField, Range(0f, 1f)] private float loudness = 0.85f;

        [Tooltip("Seconds after venting before it can be used again.")]
        [SerializeField, Min(0f)] private float rechargeSeconds = 45f;

        [Tooltip("How many times it works in a run (0 = unlimited).")]
        [SerializeField, Min(0)] private int uses = 2;

        [Tooltip("Recorded clips used: AmbienceEvent variants whose name starts with this.")]
        [SerializeField] private string clipPrefix = "steam_hiss";

        [Tooltip("Steam particles (optional), played while venting.")]
        [SerializeField] private ParticleSystem steam;

        private enum Phase { Ready, Armed, Venting, Recharging, Spent }

        private Phase phase = Phase.Ready;
        private float timer;
        private float noiseTimer;
        private int used;
        private AudioSource source;
        private readonly List<AudioClip> clips = new List<AudioClip>();

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.minDistance = 3f;
            source.maxDistance = 45f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.volume = 0.9f;

            foreach (AudioClip clip in SoundBank.GetAll(GameSound.AmbienceEvent))
            {
                if (clip != null && clip.name.StartsWith(clipPrefix))
                {
                    clips.Add(clip);
                }
            }
        }

        public bool IsAvailable => phase == Phase.Ready;

        public string GetPrompt(IInteractionActor actor)
        {
            switch (phase)
            {
                case Phase.Ready: return prompt;
                case Phase.Recharging: return rechargePrompt;
                case Phase.Spent: return spentPrompt;
                default: return busyPrompt;
            }
        }

        public string Interact(IInteractionActor actor)
        {
            if (phase != Phase.Ready)
            {
                return null;
            }

            phase = Phase.Armed;
            timer = delaySeconds;
            used++;
            GameLog.Info($"[Lure] {name} armed ({used}/{uses}).", this);
            return armedMessage;
        }

        private void Update()
        {
            if (phase == Phase.Ready || phase == Phase.Spent)
            {
                return;
            }

            timer -= Time.deltaTime;
            switch (phase)
            {
                case Phase.Armed:
                    if (timer <= 0f)
                    {
                        phase = Phase.Venting;
                        timer = ventSeconds;
                        noiseTimer = 0f;
                        if (steam != null) steam.Play();
                    }

                    break;

                case Phase.Venting:
                    noiseTimer -= Time.deltaTime;
                    if (noiseTimer <= 0f)
                    {
                        noiseTimer = noiseInterval;
                        NoiseSystem.Emit(transform.position, NoiseCategory.Other, loudness, this);
                        if (clips.Count > 0)
                        {
                            source.PlayOneShot(clips[Random.Range(0, clips.Count)]);
                        }
                    }

                    if (timer <= 0f)
                    {
                        if (steam != null) steam.Stop();
                        bool spent = uses > 0 && used >= uses;
                        phase = spent ? Phase.Spent : Phase.Recharging;
                        timer = rechargeSeconds;
                    }

                    break;

                case Phase.Recharging:
                    if (timer <= 0f)
                    {
                        phase = Phase.Ready;
                    }

                    break;
            }
        }
    }
}
