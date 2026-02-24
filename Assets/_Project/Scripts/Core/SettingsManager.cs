using UnityEngine;

namespace Cryptid.Core
{
    /// <summary>
    /// Persistent game settings stored via PlayerPrefs.
    /// Manages language, resolution, audio volume, and camera perspective.
    /// Loads on first access and saves automatically on change.
    /// </summary>
    public static class SettingsManager
    {
        // ---------------------------------------------------------
        // PlayerPrefs Keys
        // ---------------------------------------------------------

        private const string KEY_LANGUAGE    = "Settings_Language";
        private const string KEY_RES_WIDTH   = "Settings_ResWidth";
        private const string KEY_RES_HEIGHT  = "Settings_ResHeight";
        private const string KEY_FULLSCREEN  = "Settings_Fullscreen";
        private const string KEY_VOLUME      = "Settings_Volume";
        private const string KEY_PERSPECTIVE = "Settings_Perspective";

        // ---------------------------------------------------------
        // Cached Values
        // ---------------------------------------------------------

        private static bool _loaded;
        private static L.Language _language;
        private static int _resWidth;
        private static int _resHeight;
        private static bool _fullscreen;
        private static float _volume;
        private static bool _usePerspective; // false = orthographic (default)

        // ---------------------------------------------------------
        // Events
        // ---------------------------------------------------------

        /// <summary>Fired when perspective/orthographic toggle changes.</summary>
        public static event System.Action<bool> OnPerspectiveChanged;

        /// <summary>Fired when volume changes.</summary>
        public static event System.Action<float> OnVolumeChanged;

        // ---------------------------------------------------------
        // Properties
        // ---------------------------------------------------------

        public static L.Language Language
        {
            get { EnsureLoaded(); return _language; }
            set
            {
                EnsureLoaded();
                _language = value;
                L.CurrentLanguage = value;
                PlayerPrefs.SetInt(KEY_LANGUAGE, (int)value);
                PlayerPrefs.Save();
            }
        }

        public static int ResolutionWidth
        {
            get { EnsureLoaded(); return _resWidth; }
        }

        public static int ResolutionHeight
        {
            get { EnsureLoaded(); return _resHeight; }
        }

        public static bool Fullscreen
        {
            get { EnsureLoaded(); return _fullscreen; }
        }

        public static float Volume
        {
            get { EnsureLoaded(); return _volume; }
            set
            {
                EnsureLoaded();
                _volume = Mathf.Clamp01(value);
                AudioListener.volume = _volume;
                PlayerPrefs.SetFloat(KEY_VOLUME, _volume);
                PlayerPrefs.Save();
                OnVolumeChanged?.Invoke(_volume);
            }
        }

        public static bool UsePerspective
        {
            get { EnsureLoaded(); return _usePerspective; }
            set
            {
                EnsureLoaded();
                _usePerspective = value;
                PlayerPrefs.SetInt(KEY_PERSPECTIVE, value ? 1 : 0);
                PlayerPrefs.Save();
                OnPerspectiveChanged?.Invoke(value);
            }
        }

        // ---------------------------------------------------------
        // Resolution (special: applied via Screen.SetResolution)
        // ---------------------------------------------------------

        /// <summary>
        /// Sets and applies window resolution. Disabled during gameplay.
        /// </summary>
        public static void SetResolution(int width, int height, bool fullscreen)
        {
            EnsureLoaded();
            _resWidth = width;
            _resHeight = height;
            _fullscreen = fullscreen;
            PlayerPrefs.SetInt(KEY_RES_WIDTH, width);
            PlayerPrefs.SetInt(KEY_RES_HEIGHT, height);
            PlayerPrefs.SetInt(KEY_FULLSCREEN, fullscreen ? 1 : 0);
            PlayerPrefs.Save();
            Screen.SetResolution(width, height, fullscreen);
        }

        // ---------------------------------------------------------
        // Common Resolutions
        // ---------------------------------------------------------

        public static readonly (int w, int h, string label)[] Resolutions =
        {
            (1280,  720, "1280 × 720"),
            (1366,  768, "1366 × 768"),
            (1600,  900, "1600 × 900"),
            (1920, 1080, "1920 × 1080"),
            (2560, 1440, "2560 × 1440"),
            (3840, 2160, "3840 × 2160"),
        };

        // ---------------------------------------------------------
        // Load / Init
        // ---------------------------------------------------------

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            _language = (L.Language)PlayerPrefs.GetInt(KEY_LANGUAGE, (int)L.Language.KR);
            L.CurrentLanguage = _language;

            _resWidth = PlayerPrefs.GetInt(KEY_RES_WIDTH, Screen.currentResolution.width);
            _resHeight = PlayerPrefs.GetInt(KEY_RES_HEIGHT, Screen.currentResolution.height);
            _fullscreen = PlayerPrefs.GetInt(KEY_FULLSCREEN, 1) == 1;

            _volume = PlayerPrefs.GetFloat(KEY_VOLUME, 1f);
            AudioListener.volume = _volume;

            _usePerspective = PlayerPrefs.GetInt(KEY_PERSPECTIVE, 0) == 1;
        }

        /// <summary>
        /// Force-loads all settings from PlayerPrefs and applies them.
        /// Call once at startup if needed.
        /// </summary>
        public static void Initialize()
        {
            _loaded = false;
            EnsureLoaded();

            // Apply saved resolution on startup
            Screen.SetResolution(_resWidth, _resHeight, _fullscreen);
        }
    }
}
