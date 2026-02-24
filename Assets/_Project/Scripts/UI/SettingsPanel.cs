using Cryptid.Core;
using Cryptid.Network;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.UI;

namespace Cryptid.UI
{
    /// <summary>
    /// Settings panel accessible from the mode selection screen.
    /// Contains: game description, language toggle (KR/EN), resolution dropdown,
    /// audio slider, and perspective toggle (orthographic/perspective).
    ///
    /// During gameplay, resolution controls are disabled; other settings remain accessible.
    /// Persists settings via <see cref="SettingsManager"/>.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        private RectTransform _root;
        private bool _isInGame;

        // UI Elements
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _descriptionText;
        private TextMeshProUGUI _langLabel;
        private Button _langToggleBtn;
        private TextMeshProUGUI _langToggleLabel;
        private TextMeshProUGUI _resLabel;
        private Button _resPrevBtn;
        private Button _resNextBtn;
        private TextMeshProUGUI _resValueText;
        private Button _fullscreenBtn;
        private TextMeshProUGUI _fullscreenLabel;
        private TextMeshProUGUI _audioLabel;
        private Slider _audioSlider;
        private TextMeshProUGUI _audioValueText;
        private TextMeshProUGUI _perspLabel;
        private Button _perspToggleBtn;
        private TextMeshProUGUI _perspToggleLabel;
        private Button _backBtn;

        // Audio device UI
        private TextMeshProUGUI _inputDeviceLabel;
        private Button _inputDevicePrev;
        private Button _inputDeviceNext;
        private TextMeshProUGUI _inputDeviceText;
        private TextMeshProUGUI _outputDeviceLabel;
        private Button _outputDevicePrev;
        private Button _outputDeviceNext;
        private TextMeshProUGUI _outputDeviceText;
        private Button _scanDevicesBtn;

        // Audio device state
        private List<VivoxInputDevice> _inputDevices = new();
        private List<VivoxOutputDevice> _outputDevices = new();
        private int _selectedInputIndex;
        private int _selectedOutputIndex;

        // State
        private int _selectedResIndex;
        private bool _selectedFullscreen;

        // ---------------------------------------------------------
        // Construction
        // ---------------------------------------------------------

        /// <summary>Builds the settings panel UI. Called once.</summary>
        public void Build(RectTransform root)
        {
            _root = root;

            // Panel sizing
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(500, 820);
            root.anchoredPosition = Vector2.zero;

            UIFactory.AddVerticalLayout(root, spacing: 10,
                padding: new RectOffset(25, 25, 20, 20),
                childAlignment: TextAnchor.UpperCenter);

            // Title
            _titleText = UIFactory.CreateTMP(root, "Title",
                L.Get("settings"), fontSize: 32, color: UIFactory.Accent);
            _titleText.GetComponent<RectTransform>().sizeDelta = new Vector2(450, 40);

            // Game Description
            _descriptionText = UIFactory.CreateTMP(root, "Description",
                L.Get("game_description"), fontSize: 15,
                align: TextAlignmentOptions.TopLeft,
                color: new Color(0.75f, 0.75f, 0.80f));
            var descRT = _descriptionText.GetComponent<RectTransform>();
            descRT.sizeDelta = new Vector2(450, 100);
            _descriptionText.overflowMode = TextOverflowModes.Overflow;
            _descriptionText.enableWordWrapping = true;

            // Separator
            CreateSeparator(root);

            // ── Language Toggle ──
            var langRow = CreateRow(root, "LangRow");
            _langLabel = UIFactory.CreateTMP(langRow, "LangLabel",
                L.Get("settings_language"), fontSize: 18,
                align: TextAlignmentOptions.MidlineLeft);
            _langLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 36);

            _langToggleBtn = UIFactory.CreateButton(langRow, "LangBtn",
                GetLanguageLabel(), 220, 36, new Color(0.20f, 0.35f, 0.55f), fontSize: 18);
            _langToggleBtn.onClick.AddListener(ToggleLanguage);
            _langToggleLabel = _langToggleBtn.GetComponentInChildren<TextMeshProUGUI>();

            // ── Resolution ──
            var resRow = CreateRow(root, "ResRow");
            _resLabel = UIFactory.CreateTMP(resRow, "ResLabel",
                L.Get("settings_resolution"), fontSize: 18,
                align: TextAlignmentOptions.MidlineLeft);
            _resLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 36);

            _resPrevBtn = UIFactory.CreateButton(resRow, "ResPrev",
                "◀", 36, 36, UIFactory.ButtonNormal, fontSize: 20);
            _resPrevBtn.onClick.AddListener(() => ChangeResolution(-1));

            _resValueText = UIFactory.CreateTMP(resRow, "ResValue",
                "", fontSize: 16, align: TextAlignmentOptions.Center);
            _resValueText.GetComponent<RectTransform>().sizeDelta = new Vector2(148, 36);

            _resNextBtn = UIFactory.CreateButton(resRow, "ResNext",
                "▶", 36, 36, UIFactory.ButtonNormal, fontSize: 20);
            _resNextBtn.onClick.AddListener(() => ChangeResolution(1));

            // Fullscreen toggle
            var fsRow = CreateRow(root, "FSRow");
            var fsSpacer = new GameObject("Spacer", typeof(RectTransform));
            fsSpacer.transform.SetParent(fsRow, false);
            fsSpacer.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 36);

            _fullscreenBtn = UIFactory.CreateButton(fsRow, "FullscreenBtn",
                "", 220, 36, UIFactory.ButtonNormal, fontSize: 16);
            _fullscreenBtn.onClick.AddListener(ToggleFullscreen);
            _fullscreenLabel = _fullscreenBtn.GetComponentInChildren<TextMeshProUGUI>();

            // ── Audio Volume ──
            var audioRow = CreateRow(root, "AudioRow");
            _audioLabel = UIFactory.CreateTMP(audioRow, "AudioLabel",
                L.Get("settings_audio"), fontSize: 18,
                align: TextAlignmentOptions.MidlineLeft);
            _audioLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 36);

            // Slider container
            var sliderGo = new GameObject("VolumeSlider", typeof(RectTransform));
            sliderGo.transform.SetParent(audioRow, false);
            var sliderRT = sliderGo.GetComponent<RectTransform>();
            sliderRT.sizeDelta = new Vector2(175, 36);

            _audioSlider = CreateSlider(sliderGo);
            _audioSlider.value = SettingsManager.Volume;
            _audioSlider.onValueChanged.AddListener(OnVolumeChanged);

            _audioValueText = UIFactory.CreateTMP(audioRow, "AudioVal",
                Mathf.RoundToInt(SettingsManager.Volume * 100) + "%", fontSize: 16);
            _audioValueText.GetComponent<RectTransform>().sizeDelta = new Vector2(45, 36);

            // ── Input Device (Microphone) ──
            var inputDevRow = CreateRow(root, "InputDevRow");
            _inputDeviceLabel = UIFactory.CreateTMP(inputDevRow, "InputDevLabel",
                L.Get("settings_input_device"), fontSize: 16,
                align: TextAlignmentOptions.MidlineLeft);
            _inputDeviceLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(120, 36);

            _inputDevicePrev = UIFactory.CreateButton(inputDevRow, "InputPrev",
                "◀", 30, 30, UIFactory.ButtonNormal, fontSize: 18);
            _inputDevicePrev.onClick.AddListener(() => ChangeInputDevice(-1));

            _inputDeviceText = UIFactory.CreateTMP(inputDevRow, "InputDevVal",
                L.Get("default_device"), fontSize: 13,
                align: TextAlignmentOptions.Center);
            _inputDeviceText.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 36);
            _inputDeviceText.overflowMode = TextOverflowModes.Ellipsis;

            _inputDeviceNext = UIFactory.CreateButton(inputDevRow, "InputNext",
                "▶", 30, 30, UIFactory.ButtonNormal, fontSize: 18);
            _inputDeviceNext.onClick.AddListener(() => ChangeInputDevice(1));

            // ── Output Device (Speaker) ──
            var outputDevRow = CreateRow(root, "OutputDevRow");
            _outputDeviceLabel = UIFactory.CreateTMP(outputDevRow, "OutputDevLabel",
                L.Get("settings_output_device"), fontSize: 16,
                align: TextAlignmentOptions.MidlineLeft);
            _outputDeviceLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(120, 36);

            _outputDevicePrev = UIFactory.CreateButton(outputDevRow, "OutputPrev",
                "◀", 30, 30, UIFactory.ButtonNormal, fontSize: 18);
            _outputDevicePrev.onClick.AddListener(() => ChangeOutputDevice(-1));

            _outputDeviceText = UIFactory.CreateTMP(outputDevRow, "OutputDevVal",
                L.Get("default_device"), fontSize: 13,
                align: TextAlignmentOptions.Center);
            _outputDeviceText.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 36);
            _outputDeviceText.overflowMode = TextOverflowModes.Ellipsis;

            _outputDeviceNext = UIFactory.CreateButton(outputDevRow, "OutputNext",
                "▶", 30, 30, UIFactory.ButtonNormal, fontSize: 18);
            _outputDeviceNext.onClick.AddListener(() => ChangeOutputDevice(1));

            // Scan devices button
            _scanDevicesBtn = UIFactory.CreateButton(root, "ScanDevBtn",
                L.Get("scan_devices"), 200, 30, new Color(0.25f, 0.35f, 0.50f), fontSize: 14);
            _scanDevicesBtn.onClick.AddListener(ScanAudioDevices);

            // ── Perspective Toggle ──
            var perspRow = CreateRow(root, "PerspRow");
            _perspLabel = UIFactory.CreateTMP(perspRow, "PerspLabel",
                L.Get("settings_perspective"), fontSize: 18,
                align: TextAlignmentOptions.MidlineLeft);
            _perspLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 36);

            _perspToggleBtn = UIFactory.CreateButton(perspRow, "PerspBtn",
                GetPerspectiveLabel(), 220, 36,
                SettingsManager.UsePerspective
                    ? new Color(0.20f, 0.55f, 0.35f)
                    : new Color(0.20f, 0.35f, 0.55f),
                fontSize: 18);
            _perspToggleBtn.onClick.AddListener(TogglePerspective);
            _perspToggleLabel = _perspToggleBtn.GetComponentInChildren<TextMeshProUGUI>();

            // Separator
            CreateSeparator(root);

            // ── Back Button ──
            _backBtn = UIFactory.CreateButton(root, "BackBtn",
                L.Get("settings_back"), 200, 45, new Color(0.6f, 0.2f, 0.2f));
            _backBtn.onClick.AddListener(Hide);

            // Initialize resolution index
            InitResolutionIndex();
            UpdateResolutionDisplay();
            UpdateFullscreenDisplay();

            gameObject.SetActive(false);
        }

        // ---------------------------------------------------------
        // Show/Hide
        // ---------------------------------------------------------

        /// <summary>Shows the settings panel. Pass inGame=true to disable resolution.</summary>
        public void Show(bool inGame = false)
        {
            _isInGame = inGame;
            UIAnimator.ShowPanel(gameObject);

            // Disable resolution controls during gameplay
            _resPrevBtn.interactable = !inGame;
            _resNextBtn.interactable = !inGame;
            _fullscreenBtn.interactable = !inGame;

            // Update resolution display with current value
            InitResolutionIndex();
            UpdateResolutionDisplay();
            UpdateFullscreenDisplay();

            // Scan audio devices
            ScanAudioDevices();

            RefreshLabels();
        }

        public void Hide()
        {
            UIAnimator.HidePanel(gameObject);
        }

        // ---------------------------------------------------------
        // Language Toggle
        // ---------------------------------------------------------

        private void ToggleLanguage()
        {
            var newLang = SettingsManager.Language == L.Language.KR
                ? L.Language.EN : L.Language.KR;
            SettingsManager.Language = newLang;
            _langToggleLabel.text = GetLanguageLabel();
            RefreshLabels();
        }

        private static string GetLanguageLabel() =>
            SettingsManager.Language == L.Language.KR ? "한국어 / Korean" : "English / 영어";

        // ---------------------------------------------------------
        // Resolution
        // ---------------------------------------------------------

        private void InitResolutionIndex()
        {
            int w = SettingsManager.ResolutionWidth;
            int h = SettingsManager.ResolutionHeight;
            _selectedFullscreen = SettingsManager.Fullscreen;

            _selectedResIndex = 3; // Default 1920x1080
            for (int i = 0; i < SettingsManager.Resolutions.Length; i++)
            {
                var r = SettingsManager.Resolutions[i];
                if (r.w == w && r.h == h)
                {
                    _selectedResIndex = i;
                    break;
                }
            }
        }

        private void ChangeResolution(int dir)
        {
            _selectedResIndex = Mathf.Clamp(
                _selectedResIndex + dir, 0, SettingsManager.Resolutions.Length - 1);
            var res = SettingsManager.Resolutions[_selectedResIndex];
            SettingsManager.SetResolution(res.w, res.h, _selectedFullscreen);
            UpdateResolutionDisplay();
        }

        private void ToggleFullscreen()
        {
            _selectedFullscreen = !_selectedFullscreen;
            var res = SettingsManager.Resolutions[_selectedResIndex];
            SettingsManager.SetResolution(res.w, res.h, _selectedFullscreen);
            UpdateFullscreenDisplay();
        }

        private void UpdateResolutionDisplay()
        {
            if (_resValueText == null) return;
            _resValueText.text = SettingsManager.Resolutions[_selectedResIndex].label;
        }

        private void UpdateFullscreenDisplay()
        {
            if (_fullscreenLabel == null) return;
            _fullscreenLabel.text = _selectedFullscreen ? "☐ Windowed → ☑ Fullscreen" : "☑ Windowed → ☐ Fullscreen";
            var cb = _fullscreenBtn.colors;
            cb.normalColor = _selectedFullscreen
                ? new Color(0.20f, 0.55f, 0.35f) : UIFactory.ButtonNormal;
            cb.selectedColor = cb.normalColor;
            _fullscreenBtn.colors = cb;
        }

        // ---------------------------------------------------------
        // Audio
        // ---------------------------------------------------------

        private void OnVolumeChanged(float value)
        {
            SettingsManager.Volume = value;
            _audioValueText.text = Mathf.RoundToInt(value * 100) + "%";
        }

        // ---------------------------------------------------------
        // Audio Devices
        // ---------------------------------------------------------

        private void ScanAudioDevices()
        {
            var vivox = VivoxManager.Instance;
            if (vivox == null || !vivox.IsReady)
            {
                _inputDeviceText.text = L.Get("vivox_not_ready");
                _outputDeviceText.text = L.Get("vivox_not_ready");
                return;
            }

            _inputDevices = vivox.GetInputDevices()?.ToList() ?? new List<VivoxInputDevice>();
            _outputDevices = vivox.GetOutputDevices()?.ToList() ?? new List<VivoxOutputDevice>();

            // Find current active device index
            var activeInput = vivox.GetActiveInputDevice();
            _selectedInputIndex = 0;
            if (activeInput != null)
            {
                for (int i = 0; i < _inputDevices.Count; i++)
                {
                    if (_inputDevices[i].DeviceName == activeInput.DeviceName)
                    {
                        _selectedInputIndex = i;
                        break;
                    }
                }
            }

            var activeOutput = vivox.GetActiveOutputDevice();
            _selectedOutputIndex = 0;
            if (activeOutput != null)
            {
                for (int i = 0; i < _outputDevices.Count; i++)
                {
                    if (_outputDevices[i].DeviceName == activeOutput.DeviceName)
                    {
                        _selectedOutputIndex = i;
                        break;
                    }
                }
            }

            UpdateInputDeviceDisplay();
            UpdateOutputDeviceDisplay();
        }

        private void ChangeInputDevice(int dir)
        {
            if (_inputDevices.Count == 0) { ScanAudioDevices(); return; }
            _selectedInputIndex = Mathf.Clamp(_selectedInputIndex + dir, 0, _inputDevices.Count - 1);
            UpdateInputDeviceDisplay();
            _ = VivoxManager.Instance?.SetInputDeviceAsync(_inputDevices[_selectedInputIndex]);
        }

        private void ChangeOutputDevice(int dir)
        {
            if (_outputDevices.Count == 0) { ScanAudioDevices(); return; }
            _selectedOutputIndex = Mathf.Clamp(_selectedOutputIndex + dir, 0, _outputDevices.Count - 1);
            UpdateOutputDeviceDisplay();
            _ = VivoxManager.Instance?.SetOutputDeviceAsync(_outputDevices[_selectedOutputIndex]);
        }

        private void UpdateInputDeviceDisplay()
        {
            if (_inputDeviceText == null) return;
            _inputDeviceText.text = _inputDevices.Count > 0
                ? _inputDevices[_selectedInputIndex].DeviceName
                : L.Get("no_devices");
        }

        private void UpdateOutputDeviceDisplay()
        {
            if (_outputDeviceText == null) return;
            _outputDeviceText.text = _outputDevices.Count > 0
                ? _outputDevices[_selectedOutputIndex].DeviceName
                : L.Get("no_devices");
        }

        // ---------------------------------------------------------
        // Perspective Toggle
        // ---------------------------------------------------------

        private void TogglePerspective()
        {
            SettingsManager.UsePerspective = !SettingsManager.UsePerspective;
            _perspToggleLabel.text = GetPerspectiveLabel();
            var cb = _perspToggleBtn.colors;
            cb.normalColor = SettingsManager.UsePerspective
                ? new Color(0.20f, 0.55f, 0.35f)
                : new Color(0.20f, 0.35f, 0.55f);
            cb.selectedColor = cb.normalColor;
            _perspToggleBtn.colors = cb;
        }

        private static string GetPerspectiveLabel() =>
            SettingsManager.UsePerspective ? L.Get("settings_persp") : L.Get("settings_ortho");

        // ---------------------------------------------------------
        // Refresh All Labels (after language change)
        // ---------------------------------------------------------

        private void RefreshLabels()
        {
            _titleText.text = L.Get("settings");
            _descriptionText.text = L.Get("game_description");
            _langLabel.text = L.Get("settings_language");
            _resLabel.text = L.Get("settings_resolution");
            _audioLabel.text = L.Get("settings_audio");
            _inputDeviceLabel.text = L.Get("settings_input_device");
            _outputDeviceLabel.text = L.Get("settings_output_device");
            _scanDevicesBtn.GetComponentInChildren<TextMeshProUGUI>().text =
                L.Get("scan_devices");
            _perspLabel.text = L.Get("settings_perspective");
            _perspToggleLabel.text = GetPerspectiveLabel();
            _backBtn.GetComponentInChildren<TextMeshProUGUI>().text = L.Get("settings_back");
        }

        // ---------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------

        private static RectTransform CreateRow(RectTransform parent, string name)
        {
            var row = UIFactory.CreatePanel(parent, name);
            row.sizeDelta = new Vector2(450, 36);
            UIFactory.AddHorizontalLayout(row, spacing: 8,
                padding: new RectOffset(0, 0, 0, 0),
                childAlignment: TextAnchor.MiddleLeft);
            return row;
        }

        private static void CreateSeparator(RectTransform parent)
        {
            var sep = UIFactory.CreatePanel(parent, "Separator",
                new Color(0.3f, 0.3f, 0.4f, 0.5f));
            sep.sizeDelta = new Vector2(450, 2);
        }

        private static Slider CreateSlider(GameObject parent)
        {
            var slider = parent.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;

            // Background
            var bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(parent.transform, false);
            var bgRT = bgGo.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0, 0.4f);
            bgRT.anchorMax = new Vector2(1, 0.6f);
            bgRT.sizeDelta = Vector2.zero;
            bgGo.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f);

            // Fill Area
            var fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(parent.transform, false);
            var faRT = fillArea.GetComponent<RectTransform>();
            faRT.anchorMin = new Vector2(0, 0.4f);
            faRT.anchorMax = new Vector2(1, 0.6f);
            faRT.sizeDelta = Vector2.zero;
            faRT.offsetMin = new Vector2(5, 0);
            faRT.offsetMax = new Vector2(-5, 0);

            var fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(fillArea.transform, false);
            var fillRT = fill.GetComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.sizeDelta = Vector2.zero;
            fill.AddComponent<Image>().color = UIFactory.Accent;

            slider.fillRect = fillRT;

            // Handle
            var handleArea = new GameObject("HandleArea", typeof(RectTransform));
            handleArea.transform.SetParent(parent.transform, false);
            var haRT = handleArea.GetComponent<RectTransform>();
            haRT.anchorMin = Vector2.zero;
            haRT.anchorMax = Vector2.one;
            haRT.sizeDelta = Vector2.zero;
            haRT.offsetMin = new Vector2(5, 0);
            haRT.offsetMax = new Vector2(-5, 0);

            var handle = new GameObject("Handle", typeof(RectTransform));
            handle.transform.SetParent(handleArea.transform, false);
            var hRT = handle.GetComponent<RectTransform>();
            hRT.sizeDelta = new Vector2(16, 16);
            handle.AddComponent<Image>().color = Color.white;

            slider.handleRect = hRT;
            slider.targetGraphic = handle.GetComponent<Image>();

            return slider;
        }
    }
}
