using UnityEngine;

namespace Cryptid.UI
{
    /// <summary>
    /// Provides runtime access to UI icon sprites stored in Resources/Icons.
    /// Icons are loaded on first access and cached for reuse.
    /// 
    /// Uses Texture2D → Sprite conversion so it works regardless of
    /// Unity's texture import settings (no manual "Sprite" type needed).
    /// 
    /// Icon sources: UX Flat Icons [Free] by Heathen Engineering.
    /// </summary>
    public static class IconProvider
    {
        // Cached sprites
        private static Sprite _settings;
        private static Sprite _micOn;
        private static Sprite _micOff;
        private static Sprite _speakerOn;
        private static Sprite _speakerOff;

        /// <summary>Settings gear icon.</summary>
        public static Sprite Settings  => _settings  ??= LoadIcon("icon_settings");

        /// <summary>Microphone enabled icon.</summary>
        public static Sprite MicOn     => _micOn     ??= LoadIcon("icon_mic_on");

        /// <summary>Microphone muted icon.</summary>
        public static Sprite MicOff    => _micOff    ??= LoadIcon("icon_mic_off");

        /// <summary>Speaker enabled icon.</summary>
        public static Sprite SpeakerOn => _speakerOn ??= LoadIcon("icon_speaker_on");

        /// <summary>Speaker muted icon.</summary>
        public static Sprite SpeakerOff => _speakerOff ??= LoadIcon("icon_speaker_off");

        /// <summary>
        /// Loads a PNG from Resources/Icons and converts to Sprite.
        /// Works regardless of texture import type.
        /// </summary>
        private static Sprite LoadIcon(string name)
        {
            // Try loading as Sprite first (works if import type = Sprite)
            var sprite = Resources.Load<Sprite>($"Icons/{name}");
            if (sprite != null) return sprite;

            // Fallback: load as Texture2D and create Sprite manually
            var tex = Resources.Load<Texture2D>($"Icons/{name}");
            if (tex != null)
            {
                return Sprite.Create(tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), 100f);
            }

            Debug.LogWarning($"[IconProvider] Icon not found: Icons/{name}");
            return null;
        }
    }
}
