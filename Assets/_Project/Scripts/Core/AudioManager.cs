using UnityEngine;

namespace Cryptid.Core
{
    /// <summary>
    /// Manages background music (BGM) and sound effects (SFX).
    /// BGM: Scene-specific tracks — lobby plays Sunrise, in-game plays The Battle #1,
    ///      then shuffles remaining tracks after the first finishes.
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
        private bool _playingInitialTrack; // True while lobby/ingame first track is still active

        // Named track references (loaded once)
        private AudioClip _lobbyTrack;   // Sunrise_Loop
        private AudioClip _ingameTrack;  // TheBattle1_Loop

        // ---------------------------------------------------------
        // Volume
        // ---------------------------------------------------------

        private float _masterVolume = 1.0f;
        private float _bgmVolume = 0.3f;
        private float _sfxVolume = 0.7f;

        public float MasterVolume
        {
            get => _masterVolume;
            set
            {
                _masterVolume = Mathf.Clamp01(value);
                ApplyBgmVolume();
            }
        }

        public float BgmVolume
        {
            get => _bgmVolume;
            set
            {
                _bgmVolume = Mathf.Clamp01(value);
                ApplyBgmVolume();
            }
        }

        public float SfxVolume
        {
            get => _sfxVolume;
            set => _sfxVolume = Mathf.Clamp01(value);
        }

        private void ApplyBgmVolume()
        {
            if (_bgmSource != null)
                _bgmSource.volume = _bgmVolume * _masterVolume;
        }

        // ---------------------------------------------------------
        // SFX Clip Names → Resources/SFX/{name}
        // ---------------------------------------------------------

        private const string SFX_BUTTON_CLICK    = "DM-CGS-32";
        private const string SFX_TURN_START      = "DM-CGS-02";
        private const string SFX_TOKEN_PLACE     = "DM-CGS-03";
        private const string SFX_SEARCH_SUCCESS  = "DM-CGS-26";
        private const string SFX_SEARCH_FAIL     = "DM-CGS-17";
        private const string SFX_PENALTY         = "DM-CGS-06";
        private const string SFX_TIMER_TICK      = "DM-CGS-04";
        private const string SFX_GAME_WIN        = "DM-CGS-18";
        private const string SFX_GAME_LOSE       = "DM-CGS-23";
        private const string SFX_QUESTION_ASK    = "DM-CGS-09";
        private const string SFX_RESPONSE_YES    = "DM-CGS-26";
        private const string SFX_RESPONSE_NO     = "DM-CGS-17";

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
            _bgmSource.volume = _bgmVolume * _masterVolume;
            _bgmSource.spatialBlend = 0f; // 2D sound

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.loop = false;
            _sfxSource.playOnAwake = false;
            _sfxSource.spatialBlend = 0f;

            LoadBGMTracks();

            // Start with lobby music by default
            PlayLobbyBGM();
        }

        private void Update()
        {
            // Auto-advance to next BGM track when current finishes
            if (_bgmInitialized && !_bgmSource.isPlaying && _bgmClips.Length > 0)
            {
                _playingInitialTrack = false;
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

            // Cache named track references
            foreach (var clip in _bgmClips)
            {
                if (clip.name.Contains("Sunrise"))  _lobbyTrack = clip;
                if (clip.name.Contains("Battle1") || clip.name.Contains("TheBattle1"))
                    _ingameTrack = clip;
            }

            ShuffleBGMOrder();
            _bgmInitialized = true;
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
            ApplyBgmVolume();
            _bgmSource.Play();
            _currentBgmIndex++;

            Debug.Log($"[AudioManager] Playing BGM: {_bgmSource.clip.name}");
        }

        /// <summary>Plays the lobby BGM (Sunrise). Called on main menu / lobby screen.</summary>
        public void PlayLobbyBGM()
        {
            if (_lobbyTrack != null)
            {
                _bgmSource.Stop();
                _bgmSource.clip = _lobbyTrack;
                ApplyBgmVolume();
                _bgmSource.Play();
                _playingInitialTrack = true;
                Debug.Log("[AudioManager] Playing Lobby BGM: Sunrise");
            }
            else
            {
                // Fallback to shuffle if named track not found
                PlayNextBGM();
            }
        }

        /// <summary>Plays the in-game BGM (The Battle #1). Called when game starts.</summary>
        public void PlayIngameBGM()
        {
            if (_ingameTrack != null)
            {
                _bgmSource.Stop();
                _bgmSource.clip = _ingameTrack;
                ApplyBgmVolume();
                _bgmSource.Play();
                _playingInitialTrack = true;
                Debug.Log("[AudioManager] Playing In-Game BGM: The Battle #1");
            }
            else
            {
                // Fallback to shuffle if named track not found
                PlayNextBGM();
            }
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

            _sfxSource.PlayOneShot(clip, _sfxVolume * _masterVolume);
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
        public void PlayTimerWarning()   => PlaySFX(SFX_TIMER_TICK);
        public void PlayGameWin()        => PlaySFX(SFX_GAME_WIN);
        public void PlayGameLose()       => PlaySFX(SFX_GAME_LOSE);
        public void PlayQuestionAsk()    => PlaySFX(SFX_QUESTION_ASK);
        public void PlayResponseYes()    => PlaySFX(SFX_RESPONSE_YES);
        public void PlayResponseNo()     => PlaySFX(SFX_RESPONSE_NO);
    }
}
