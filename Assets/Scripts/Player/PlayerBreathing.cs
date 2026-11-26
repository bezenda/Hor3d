using AshenWoods.Audio;
using AshenWoods.GameManagement;
using UnityEngine;

namespace AshenWoods.Player
{
    /// <summary>
    /// Heavy breathing while the player is out of stamina (PlayerMotor.IsExhausted): it starts
    /// when the bar runs dry and fades out once sprinting is allowed again. Added by PlayerMotor.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerBreathing : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float volume = 0.55f;
        [SerializeField, Min(0.05f)] private float fadeInSeconds = 0.25f;
        [SerializeField, Min(0.05f)] private float fadeOutSeconds = 1.5f;

        private PlayerMotor motor;
        private AudioSource source;
        private float level;

        private void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.priority = 40;
            source.volume = 0f;
        }

        private void Update()
        {
            if (motor == null)
            {
                return;
            }

            bool panting = motor.IsExhausted;
            if (panting && !source.isPlaying)
            {
                source.clip = SoundBank.Get(GameSound.Panting);
                source.time = 0f;
                source.Play();
            }

            float rate = Time.deltaTime / (panting ? fadeInSeconds : fadeOutSeconds);
            level = Mathf.MoveTowards(level, panting ? 1f : 0f, rate);
            source.volume = level * volume;
            if (level <= 0f && source.isPlaying)
            {
                source.Stop();
            }
        }
    }
}
