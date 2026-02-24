using UnityEngine;

namespace Cryptid.Core
{
    /// <summary>
    /// Manages background music (BGM) and sound effects (SFX).
    /// BGM: Randomly shuffles and loops tracks from Resources/BGM.
    /// SFX: Plays one-shot clips loaded from Resources/SFX.
    ///
    /// Singleton — persists across scenes.
    /// Volume is controlled via <see cref="SettingsManager"/>.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        // ---------------------------------------------------------
        // Singleton
        // ---------------------------------------------------------

        private static AudioManager _instance;
        public static AudioManager Instance => _instance;

        // ---------------------------------------------------------
        // Audio Sources
        // ---------------------------------------------------------

        private AudioSource _bgmSource;
        private AudioSource _sfxSource;

        // ---------------------------------------------------------
        // BGM State
        // ---------------------------------------------------------

        private AudioClip[] _bgmClips;
        private int[] _shuffledIndices;
        private int _currentBgmIndex;
        private bool _bgmInitialized;

        // ---------------------------------------------------------
        // Volume
        // ---------------------------------------------------------

        private float _bgmVolume = 0.3f;
        private float _sfxVolume = 0.7f;

        public float BgmVolume
        {
            get => _bgmVolume;
            set
            {
                _bgmVolume = Mathf.Clamp01(value);
                if (_bgmSource != null) _bgmSource.volume = _bgmVolume;
            }
        }

        public float SfxVolume
        {
            get => _sfxVolume;
            set => _sfxVolume = Mathf.Clamp01(value);
        }

        // ---------------------------------------------------------
        // SFX Clip Names → Resources/SFX/{name}
        // Mapped after user provides DM-CGS-XX → event mapping.
        // ---------------------------------------------------------

        // Placeholder mappings — update once user provides SFX mapping
        private const string SFX_BUTTON_CLICK    = "DM-CGS-01";
        private const string SFX_TURN_START      = "DM-CGS-02";
        private const string SFX_TOKEN_PLACE     = "DM-CGS-03";
        private const string SFX_SEARCH_SUCCESS  = "DM-CGS-04";
        private const string SFX_SEARCH_FAIL     = "DM-CGS-05";
        private const string SFX_PENALTY         = "DM-CGS-06";
        private const string SFX_TIMER_WARNING   = "DM-CGS-07";
        private const string SFX_GAME_WIN        = "DM-CGS-08";
        private const string SFX_QUESTION_ASK    = "DM-CGS-09";
        private const string SFX_RESPONSE_YES    = "DM-CGS-10";
        private const string SFX_RESPONSE_NO     = "DM-CGS-11";

        // ---------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            // Create audio sources
            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.loop = false; // We manage looping manually for shuffle
            _bgmSource.playOnAwake = false;
            _bgmSource.volume = _bgmVolume;
            _bgmSource.spatialBlend = 0f; // 2D sound

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.loop = false;
            _sfxSource.playOnAwake = false;
            _sfxSource.spatialBlend = 0f;

            LoadBGMTracks();
        }

        private void Update()
        {
            // Auto-advance to next BGM track when current finishes
            if (_bgmInitialized && !_bgmSource.isPlaying && _bgmClips.Length > 0)
            {
                PlayNextBGM();
            }
        }

        // ---------------------------------------------------------
        // BGM System
        // ---------------------------------------------------------

        private void LoadBGMTracks()
        {
            _bgmClips = Resources.LoadAll<AudioClip>("BGM");
            if (_bgmClips == null || _bgmClips.Length == 0)
            {
                Debug.LogWarning("[AudioManager] No BGM clips found in Resources/BGM.");
                return;
            }

            Debug.Log($"[AudioManager] Loaded {_bgmClips.Length} BGM tracks.");
            ShuffleBGMOrder();
            _bgmInitialized = true;
            PlayNextBGM();
        }

        private void ShuffleBGMOrder()
        {
            _shuffledIndices = new int[_bgmClips.Length];
            for (int i = 0; i < _shuffledIndices.Length; i++)
                _shuffledIndices[i] = i;

            // Fisher-Yates shuffle
            for (int i = _shuffledIndices.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (_shuffledIndices[i], _shuffledIndices[j]) = (_shuffledIndices[j], _shuffledIndices[i]);
            }

            _currentBgmIndex = 0;
        }

        private void PlayNextBGM()
        {
            if (_bgmClips.Length == 0) return;

            if (_currentBgmIndex >= _shuffledIndices.Length)
            {
                // All tracks played — reshuffle for next cycle
                ShuffleBGMOrder();
            }

            int clipIndex = _shuffledIndices[_currentBgmIndex];
            _bgmSource.clip = _bgmClips[clipIndex];
            _bgmSource.volume = _bgmVolume;
            _bgmSource.Play();
            _currentBgmIndex++;

            Debug.Log($"[AudioManager] Playing BGM: {_bgmSource.clip.name}");
        }

        /// <summary>Stops BGM playback.</summary>
        public void StopBGM()
        {
            _bgmSource.Stop();
        }

        /// <summary>Pauses BGM playback.</summary>
        public void PauseBGM()
        {
            _bgmSource.Pause();
        }

        /// <summary>Resumes paused BGM playback.</summary>
        public void ResumeBGM()
        {
            _bgmSource.UnPause();
        }

        // ---------------------------------------------------------
        // SFX System
        // ---------------------------------------------------------

        /// <summary>Plays an SFX clip by resource name (without path prefix).</summary>
        public void PlaySFX(string clipName)
        {
            var clip = Resources.Load<AudioClip>($"SFX/{clipName}");
            if (clip == null)
            {
                Debug.LogWarning($"[AudioManager] SFX not found: SFX/{clipName}");
                return;
            }

            _sfxSource.PlayOneShot(clip, _sfxVolume);
        }

        // ---------------------------------------------------------
        // Convenience SFX Methods (game events)
        // ---------------------------------------------------------

        public void PlayButtonClick()    => PlaySFX(SFX_BUTTON_CLICK);
        public void PlayTurnStart()      => PlaySFX(SFX_TURN_START);
        public void PlayTokenPlace()     => PlaySFX(SFX_TOKEN_PLACE);
        public void PlaySearchSuccess()  => PlaySFX(SFX_SEARCH_SUCCESS);
        public void PlaySearchFail()     => PlaySFX(SFX_SEARCH_FAIL);
        public void PlayPenalty()        => PlaySFX(SFX_PENALTY);
        public void PlayTimerWarning()   => PlaySFX(SFX_TIMER_WARNING);
        public void PlayGameWin()        => PlaySFX(SFX_GAME_WIN);
        public void PlayQuestionAsk()    => PlaySFX(SFX_QUESTION_ASK);
        public void PlayResponseYes()    => PlaySFX(SFX_RESPONSE_YES);
        public void PlayResponseNo()     => PlaySFX(SFX_RESPONSE_NO);
    }
}
