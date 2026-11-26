using UnityEngine;

namespace AshenWoods.Audio
{
    /// <summary>
    /// The title screen's background music (owner, 2026-11-26): Sounds/MenuMusic, looped, kept low so it sits under the
    /// wind and the menu ticks, faded in over a few seconds. It follows the master volume (AudioListener); starting a
    /// game fades the listener out (MainMenuController), and the scene change ends it. Added by MainMenuController.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuMusic : MonoBehaviour
    {
        private const float Volume = 0.22f;
        private const float FadeInSeconds = 4f;

        private AudioSource source;

        private void Start()
        {
            AudioClip clip = SoundBank.Get(GameSound.MenuMusic);
            if (clip == null || clip.length < 1f)
            {
                return; // no recording: silent (SoundBank already warned)
            }

            source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.priority = 0;
            source.volume = 0f;
            source.Play();
        }

        private void Update()
        {
            if (source != null && source.volume < Volume)
            {
                source.volume = Mathf.Min(Volume, source.volume + Volume * Time.unscaledDeltaTime / FadeInSeconds);
            }
        }
    }
}
