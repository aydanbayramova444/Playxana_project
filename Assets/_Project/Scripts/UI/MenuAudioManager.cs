using UnityEngine;

namespace Game.UI
{
    [DisallowMultipleComponent]
    public class MenuAudioManager : MonoBehaviour
    {
        public static MenuAudioManager Instance { get; private set; }

        [Header("Background Music")]
        [SerializeField] private AudioClip backgroundMusic;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.5f;
        [SerializeField] private bool playMusicOnStart = true;
        [SerializeField] private bool loopMusic = true;

        [Header("Button Sounds")]
        [SerializeField] private AudioClip buttonClickSound;
        [SerializeField, Range(0f, 1f)] private float buttonVolume = 1f;

        private AudioSource musicSource;
        private AudioSource buttonSource;

        private void Awake()
        {
            // Singleton protection
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // Create background music AudioSource
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = loopMusic;
            musicSource.volume = musicVolume;
            musicSource.spatialBlend = 0f;

            // Create button sound AudioSource
            buttonSource = gameObject.AddComponent<AudioSource>();
            buttonSource.playOnAwake = false;
            buttonSource.loop = false;
            buttonSource.volume = buttonVolume;
            buttonSource.spatialBlend = 0f;
        }

        private void Start()
        {
            if (playMusicOnStart && backgroundMusic != null)
            {
                PlayMusic();
            }
        }

        // ---------- Background Music ----------

        public void PlayMusic()
        {
            if (backgroundMusic == null)
                return;

            if (musicSource.clip == backgroundMusic && musicSource.isPlaying)
                return;

            musicSource.clip = backgroundMusic;
            musicSource.loop = loopMusic;
            musicSource.volume = musicVolume;
            musicSource.Play();
        }

        public void StopMusic()
        {
            if (musicSource != null)
                musicSource.Stop();
        }

        public void PauseMusic()
        {
            if (musicSource != null)
                musicSource.Pause();
        }

        public void ResumeMusic()
        {
            if (musicSource != null && musicSource.clip != null)
                musicSource.UnPause();
        }

        public void SetMusicVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);

            if (musicSource != null)
                musicSource.volume = musicVolume;
        }

        // ---------- Button Sounds ----------

        public void PlayButtonSound()
        {
            if (buttonClickSound != null && buttonSource != null)
            {
                buttonSource.PlayOneShot(buttonClickSound, buttonVolume);
            }
        }

        public void SetButtonVolume(float volume)
        {
            buttonVolume = Mathf.Clamp01(volume);

            if (buttonSource != null)
                buttonSource.volume = buttonVolume;
        }

        // ---------- Toggle Music ----------

        public void ToggleMusic()
        {
            if (musicSource == null)
                return;

            if (musicSource.isPlaying)
                PauseMusic();
            else
                ResumeMusic();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}