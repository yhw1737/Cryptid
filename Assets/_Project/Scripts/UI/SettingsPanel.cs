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
    /// Settings panel with 3 tabs: Main (메인), Audio (음향), Display (디스플레이).
    /// Accessible from both the mode selection screen and in-game.
    ///
    /// Main:    language toggle, game description, back button.
    /// Audio:   master/BGM/SFX volume sliders, input/output device selectors.
    /// Display: resolution, fullscreen, perspective toggle.
    ///
    /// During gameplay, resolution controls are disabled; other settings remain accessible.
    /// Persists settings via <see cref="SettingsManager"/>.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        private RectTransform _root;
        private bool _isInGame;

        // ---------------------------------------------------------
        // Tabs
        // ---------------------------------------------------------

        private enum Tab { Main, Audio, Display }
        private Tab _activeTab = Tab.Main;

        private Button _tabMainBtn;
        private Button _tabAudioBtn;
        private Button _tabDisplayBtn;
        private TextMeshProUGUI _tabMainLabel;
        private TextMeshProUGUI _tabAudioLabel;
        private TextMeshProUGUI _tabDisplayLabel;

        private GameObject _mainContent;
        private GameObject _audioContent;
        private GameObject _displayContent;

        // ---------------------------------------------------------
        // Main Tab UI
        // ---------------------------------------------------------

        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _descriptionText;
        private TextMeshProUGUI _langLabel;
        private Button _langToggleBtn;
        private TextMeshProUGUI _langToggleLabel;
        private Button _backBtn;

        // ---------------------------------------------------------
        // Audio Tab UI
        // ---------------------------------------------------------

        private TextMeshProUGUI _masterLabel;
        private Slider _masterSlider;
        private TextMeshProUGUI _masterValueText;
        private TextMeshProUGUI _bgmLabel;
        private Slider _bgmSlider;
        private TextMeshProUGUI _bgmValueText;
        private TextMeshProUGUI _sfxLabel;
        private Slider _sfxSlider;
        private TextMeshProUGUI _sfxValueText;

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

        // ---------------------------------------------------------
        // Display Tab UI
        // ---------------------------------------------------------

        private TextMeshProUGUI _resLabel;
        private Button _resPrevBtn;
        private Button _resNextBtn;
        private TextMeshProUGUI _resValueText;
        private Button _fullscreenBtn;
        private TextMeshProUGUI _fullscreenLabel;
        private TextMeshProUGUI _perspLabel;
        private Button _perspToggleBtn;
        private TextMeshProUGUI _perspToggleLabel;

        // State
        private int _selectedResIndex;
        private bool _selectedFullscreen;

        // Tab colours
        private static readonly Color TabActive   = new Color(0.25f, 0.40f, 0.60f);
        private static readonly Color TabInactive  = new Color(0.12f, 0.12f, 0.18f);

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
            root.sizeDelta = new Vector2(500, 750);
            root.anchoredPosition = Vector2.zero;

            UIFactory.AddVerticalLayout(root, spacing: 8,
                padding: new RectOffset(20, 20, 15, 15),
                childAlignment: TextAnchor.UpperCenter);

            // Title
            _titleText = UIFactory.CreateTMP(root, "Title",
                L.Get("settings"), fontSize: 30, color: UIFactory.Accent);
            _titleText.GetComponent<RectTransform>().sizeDelta = new Vector2(460, 36);

            // Separator
            CreateSeparator(root);

            // ── Tab Buttons Row ──
            var tabRow = CreateRow(root, "TabRow");
            tabRow.sizeDelta = new Vector2(460, 40);

            _tabMainBtn = UIFactory.CreateButton(tabRow, "TabMain",
                L.Get("tab_main"), 145, 36, TabActive, fontSize: 17);
            _tabMainBtn.onClick.AddListener(() => SwitchTab(Tab.Main));
            _tabMainLabel = _tabMainBtn.GetComponentInChildren<TextMeshProUGUI>();

            _tabAudioBtn = UIFactory.CreateButton(tabRow, "TabAudio",
                L.Get("tab_audio"), 145, 36, TabInactive, fontSize: 17);
            _tabAudioBtn.onClick.AddListener(() => SwitchTab(Tab.Audio));
            _tabAudioLabel = _tabAudioBtn.GetComponentInChildren<TextMeshProUGUI>();

            _tabDisplayBtn = UIFactory.CreateButton(tabRow, "TabDisplay",
                L.Get("tab_display"), 145, 36, TabInactive, fontSize: 17);
            _tabDisplayBtn.onClick.AddListener(() => SwitchTab(Tab.Display));
            _tabDisplayLabel = _tabDisplayBtn.GetComponentInChildren<TextMeshProUGUI>();

            // ── Content Panels ──
            BuildMainContent(root);
            BuildAudioContent(root);
            BuildDisplayContent(root);

            // Separator
            CreateSeparator(root);

            // ── Back Button (always visible) ──
            _backBtn = UIFactory.CreateButton(root, "BackBtn",
                L.Get("settings_back"), 200, 45, new Color(0.6f, 0.2f, 0.2f));
            _backBtn.onClick.AddListener(Hide);

            // Initialize
            InitResolutionIndex();
            UpdateResolutionDisplay();
            UpdateFullscreenDisplay();

            SwitchTab(Tab.Main);
            gameObject.SetActive(false);
        }

        // ---------------------------------------------------------
        // Content: Main Tab
        // ---------------------------------------------------------

        private void BuildMainContent(RectTransform parent)
        {
            _mainContent = new GameObject("MainContent", typeof(RectTransform));
            _mainContent.transform.SetParent(parent, false);
            var rt = _mainContent.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(460, 420);
            UIFactory.AddVerticalLayout(rt, spacing: 10,
                padding: new RectOffset(5, 5, 10, 10),
                childAlignment: TextAnchor.UpperCenter);

            // Game Description
            _descriptionText = UIFactory.CreateTMP(rt, "Description",
                L.Get("game_description"), fontSize: 15,
                align: TextAlignmentOptions.TopLeft,
                color: new Color(0.75f, 0.75f, 0.80f));
            var descRT = _descriptionText.GetComponent<RectTransform>();
            descRT.sizeDelta = new Vector2(440, 130);
            _descriptionText.overflowMode = TextOverflowModes.Overflow;
            _descriptionText.enableWordWrapping = true;

            CreateSeparator(rt);

            // Language Toggle
            var langRow = CreateRow(rt, "LangRow");
            _langLabel = UIFactory.CreateTMP(langRow, "LangLabel",
                L.Get("settings_language"), fontSize: 18,
                align: TextAlignmentOptions.MidlineLeft);
            _langLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 36);

            _langToggleBtn = UIFactory.CreateButton(langRow, "LangBtn",
                GetLanguageLabel(), 240, 36, new Color(0.20f, 0.35f, 0.55f), fontSize: 18);
            _langToggleBtn.onClick.AddListener(ToggleLanguage);
            _langToggleLabel = _langToggleBtn.GetComponentInChildren<TextMeshProUGUI>();
        }

        // ---------------------------------------------------------
        // Content: Audio Tab
        // ---------------------------------------------------------

        private void BuildAudioContent(RectTransform parent)
        {
            _audioContent = new GameObject("AudioContent", typeof(RectTransform));
            _audioContent.transform.SetParent(parent, false);
            var rt = _audioContent.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(460, 420);
            UIFactory.AddVerticalLayout(rt, spacing: 10,
                padding: new RectOffset(5, 5, 10, 10),
                childAlignment: TextAnchor.UpperCenter);

            // Master Volume
            _masterLabel = UIFactory.CreateTMP(rt, "MasterLabel",
                L.Get("settings_master_volume"), fontSize: 17,
                align: TextAlignmentOptions.MidlineLeft);
            _masterLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(440, 24);

            var masterRow = CreateRow(rt, "MasterRow");
            var masterSliderGo = new GameObject("MasterSlider", typeof(RectTransform));
            masterSliderGo.transform.SetParent(masterRow, false);
            masterSliderGo.GetComponent<RectTransform>().sizeDelta = new Vector2(360, 30);
            _masterSlider = CreateSlider(masterSliderGo);
            _masterSlider.value = AudioManager.Instance != null
                ? AudioManager.Instance.MasterVolume : 1f;
            _masterSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            _masterValueText = UIFactory.CreateTMP(masterRow, "MasterVal",
                Mathf.RoundToInt(_masterSlider.value * 100) + "%", fontSize: 16);
            _masterValueText.GetComponent<RectTransform>().sizeDelta = new Vector2(55, 30);

            // BGM Volume
            _bgmLabel = UIFactory.CreateTMP(rt, "BgmLabel",
                L.Get("settings_bgm_volume"), fontSize: 17,
                align: TextAlignmentOptions.MidlineLeft);
            _bgmLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(440, 24);

            var bgmRow = CreateRow(rt, "BgmRow");
            var bgmSliderGo = new GameObject("BgmSlider", typeof(RectTransform));
            bgmSliderGo.transform.SetParent(bgmRow, false);
            bgmSliderGo.GetComponent<RectTransform>().sizeDelta = new Vector2(360, 30);
            _bgmSlider = CreateSlider(bgmSliderGo);
            _bgmSlider.value = AudioManager.Instance != null
                ? AudioManager.Instance.BgmVolume : 0.3f;
            _bgmSlider.onValueChanged.AddListener(OnBgmVolumeChanged);
            _bgmValueText = UIFactory.CreateTMP(bgmRow, "BgmVal",
                Mathf.RoundToInt(_bgmSlider.value * 100) + "%", fontSize: 16);
            _bgmValueText.GetComponent<RectTransform>().sizeDelta = new Vector2(55, 30);

            // UI/SFX Volume
            _sfxLabel = UIFactory.CreateTMP(rt, "SfxLabel",
                L.Get("settings_sfx_volume"), fontSize: 17,
                align: TextAlignmentOptions.MidlineLeft);
            _sfxLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(440, 24);

            var sfxRow = CreateRow(rt, "SfxRow");
            var sfxSliderGo = new GameObject("SfxSlider", typeof(RectTransform));
            sfxSliderGo.transform.SetParent(sfxRow, false);
            sfxSliderGo.GetComponent<RectTransform>().sizeDelta = new Vector2(360, 30);
            _sfxSlider = CreateSlider(sfxSliderGo);
            _sfxSlider.value = AudioManager.Instance != null
                ? AudioManager.Instance.SfxVolume : 0.7f;
            _sfxSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
            _sfxValueText = UIFactory.CreateTMP(sfxRow, "SfxVal",
                Mathf.RoundToInt(_sfxSlider.value * 100) + "%", fontSize: 16);
            _sfxValueText.GetComponent<RectTransform>().sizeDelta = new Vector2(55, 30);

            CreateSeparator(rt);

            // Input Device (Microphone)
            var inputDevRow = CreateRow(rt, "InputDevRow");
            _inputDeviceLabel = UIFactory.CreateTMP(inputDevRow, "InputDevLabel",
                L.Get("settings_input_device"), fontSize: 15,
                align: TextAlignmentOptions.MidlineLeft);
            _inputDeviceLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(110, 30);

            _inputDevicePrev = UIFactory.CreateButton(inputDevRow, "InputPrev",
                "◀", 28, 28, UIFactory.ButtonNormal, fontSize: 18);
            _inputDevicePrev.onClick.AddListener(() => ChangeInputDevice(-1));

            _inputDeviceText = UIFactory.CreateTMP(inputDevRow, "InputDevVal",
                L.Get("default_device"), fontSize: 12,
                align: TextAlignmentOptions.Center);
            _inputDeviceText.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 30);
            _inputDeviceText.overflowMode = TextOverflowModes.Ellipsis;

            _inputDeviceNext = UIFactory.CreateButton(inputDevRow, "InputNext",
                "▶", 28, 28, UIFactory.ButtonNormal, fontSize: 18);
            _inputDeviceNext.onClick.AddListener(() => ChangeInputDevice(1));

            // Output Device (Speaker)
            var outputDevRow = CreateRow(rt, "OutputDevRow");
            _outputDeviceLabel = UIFactory.CreateTMP(outputDevRow, "OutputDevLabel",
                L.Get("settings_output_device"), fontSize: 15,
                align: TextAlignmentOptions.MidlineLeft);
            _outputDeviceLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(110, 30);

            _outputDevicePrev = UIFactory.CreateButton(outputDevRow, "OutputPrev",
                "◀", 28, 28, UIFactory.ButtonNormal, fontSize: 18);
            _outputDevicePrev.onClick.AddListener(() => ChangeOutputDevice(-1));

            _outputDeviceText = UIFactory.CreateTMP(outputDevRow, "OutputDevVal",
                L.Get("default_device"), fontSize: 12,
                align: TextAlignmentOptions.Center);
            _outputDeviceText.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 30);
            _outputDeviceText.overflowMode = TextOverflowModes.Ellipsis;

            _outputDeviceNext = UIFactory.CreateButton(outputDevRow, "OutputNext",
                "▶", 28, 28, UIFactory.ButtonNormal, fontSize: 18);
            _outputDeviceNext.onClick.AddListener(() => ChangeOutputDevice(1));

            // Scan devices button
            _scanDevicesBtn = UIFactory.CreateButton(rt, "ScanDevBtn",
                L.Get("scan_devices"), 200, 28, new Color(0.25f, 0.35f, 0.50f), fontSize: 14);
            _scanDevicesBtn.onClick.AddListener(ScanAudioDevices);
        }

        // ---------------------------------------------------------
        // Content: Display Tab
        // ---------------------------------------------------------

        private void BuildDisplayContent(RectTransform parent)
        {
            _displayContent = new GameObject("DisplayContent", typeof(RectTransform));
            _displayContent.transform.SetParent(parent, false);
            var rt = _displayContent.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(460, 420);
            UIFactory.AddVerticalLayout(rt, spacing: 12,
                padding: new RectOffset(5, 5, 10, 10),
                childAlignment: TextAnchor.UpperCenter);

            // Resolution
            var resRow = CreateRow(rt, "ResRow");
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
            var fsRow = CreateRow(rt, "FSRow");
            var fsSpacer = new GameObject("Spacer", typeof(RectTransform));
            fsSpacer.transform.SetParent(fsRow, false);
            fsSpacer.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 36);

            _fullscreenBtn = UIFactory.CreateButton(fsRow, "FullscreenBtn",
                "", 240, 36, UIFactory.ButtonNormal, fontSize: 16);
            _fullscreenBtn.onClick.AddListener(ToggleFullscreen);
            _fullscreenLabel = _fullscreenBtn.GetComponentInChildren<TextMeshProUGUI>();

            CreateSeparator(rt);

            // Perspective Toggle
            var perspRow = CreateRow(rt, "PerspRow");
            _perspLabel = UIFactory.CreateTMP(perspRow, "PerspLabel",
                L.Get("settings_perspective"), fontSize: 18,
                align: TextAlignmentOptions.MidlineLeft);
            _perspLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 36);

            _perspToggleBtn = UIFactory.CreateButton(perspRow, "PerspBtn",
                GetPerspectiveLabel(), 240, 36,
                SettingsManager.UsePerspective
                    ? new Color(0.20f, 0.55f, 0.35f)
                    : new Color(0.20f, 0.35f, 0.55f),
                fontSize: 18);
            _perspToggleBtn.onClick.AddListener(TogglePerspective);
            _perspToggleLabel = _perspToggleBtn.GetComponentInChildren<TextMeshProUGUI>();
        }

        // ---------------------------------------------------------
        // Tab Switching
        // ---------------------------------------------------------

        private void SwitchTab(Tab tab)
        {
            _activeTab = tab;

            _mainContent.SetActive(tab == Tab.Main);
            _audioContent.SetActive(tab == Tab.Audio);
            _displayContent.SetActive(tab == Tab.Display);

            SetTabButtonColor(_tabMainBtn, tab == Tab.Main);
            SetTabButtonColor(_tabAudioBtn, tab == Tab.Audio);
            SetTabButtonColor(_tabDisplayBtn, tab == Tab.Display);

            if (tab == Tab.Audio)
                ScanAudioDevices();
        }

        private static void SetTabButtonColor(Button btn, bool active)
        {
            var color = active ? TabActive : TabInactive;
            var cb = btn.colors;
            cb.normalColor = color;
            cb.selectedColor = color;
            cb.highlightedColor = active ? TabActive * 1.1f : TabInactive * 1.3f;
            btn.colors = cb;
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
            if (_resPrevBtn != null) _resPrevBtn.interactable = !inGame;
            if (_resNextBtn != null) _resNextBtn.interactable = !inGame;
            if (_fullscreenBtn != null) _fullscreenBtn.interactable = !inGame;

            // Update resolution display with current value
            InitResolutionIndex();
            UpdateResolutionDisplay();
            UpdateFullscreenDisplay();

            RefreshLabels();
            SwitchTab(_activeTab);
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
        // Audio Volume
        // ---------------------------------------------------------

        private void OnMasterVolumeChanged(float value)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.MasterVolume = value;
            _masterValueText.text = Mathf.RoundToInt(value * 100) + "%";
        }

        private void OnBgmVolumeChanged(float value)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.BgmVolume = value;
            _bgmValueText.text = Mathf.RoundToInt(value * 100) + "%";
        }

        private void OnSfxVolumeChanged(float value)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.SfxVolume = value;
            _sfxValueText.text = Mathf.RoundToInt(value * 100) + "%";
        }

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
            _fullscreenLabel.text = _selectedFullscreen
                ? "☐ Windowed → ☑ Fullscreen"
                : "☑ Windowed → ☐ Fullscreen";
            var cb = _fullscreenBtn.colors;
            cb.normalColor = _selectedFullscreen
                ? new Color(0.20f, 0.55f, 0.35f) : UIFactory.ButtonNormal;
            cb.selectedColor = cb.normalColor;
            _fullscreenBtn.colors = cb;
        }

        // ---------------------------------------------------------
        // Audio Devices
        // ---------------------------------------------------------

        private void ScanAudioDevices()
        {
            var vivox = VivoxManager.Instance;
            if (vivox == null || !vivox.IsReady)
            {
                if (_inputDeviceText != null) _inputDeviceText.text = L.Get("vivox_not_ready");
                if (_outputDeviceText != null) _outputDeviceText.text = L.Get("vivox_not_ready");
                return;
            }

            _inputDevices = vivox.GetInputDevices()?.ToList() ?? new List<VivoxInputDevice>();
            _outputDevices = vivox.GetOutputDevices()?.ToList() ?? new List<VivoxOutputDevice>();

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
            _selectedInputIndex = Mathf.Clamp(
                _selectedInputIndex + dir, 0, _inputDevices.Count - 1);
            UpdateInputDeviceDisplay();
            _ = VivoxManager.Instance?.SetInputDeviceAsync(_inputDevices[_selectedInputIndex]);
        }

        private void ChangeOutputDevice(int dir)
        {
            if (_outputDevices.Count == 0) { ScanAudioDevices(); return; }
            _selectedOutputIndex = Mathf.Clamp(
                _selectedOutputIndex + dir, 0, _outputDevices.Count - 1);
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
            SettingsManager.UsePerspective
                ? L.Get("settings_persp") : L.Get("settings_ortho");

        // ---------------------------------------------------------
        // Refresh All Labels (after language change)
        // ---------------------------------------------------------

        private void RefreshLabels()
        {
            _titleText.text = L.Get("settings");
            _descriptionText.text = L.Get("game_description");
            _langLabel.text = L.Get("settings_language");
            _tabMainLabel.text = L.Get("tab_main");
            _tabAudioLabel.text = L.Get("tab_audio");
            _tabDisplayLabel.text = L.Get("tab_display");

            // Audio tab
            _masterLabel.text = L.Get("settings_master_volume");
            _bgmLabel.text = L.Get("settings_bgm_volume");
            _sfxLabel.text = L.Get("settings_sfx_volume");
            _inputDeviceLabel.text = L.Get("settings_input_device");
            _outputDeviceLabel.text = L.Get("settings_output_device");
            _scanDevicesBtn.GetComponentInChildren<TextMeshProUGUI>().text =
                L.Get("scan_devices");

            // Display tab
            _resLabel.text = L.Get("settings_resolution");
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
            row.sizeDelta = new Vector2(440, 36);
            UIFactory.AddHorizontalLayout(row, spacing: 8,
                padding: new RectOffset(0, 0, 0, 0),
                childAlignment: TextAnchor.MiddleLeft);
            return row;
        }

        private static void CreateSeparator(RectTransform parent)
        {
            var sep = UIFactory.CreatePanel(parent, "Separator",
                new Color(0.3f, 0.3f, 0.4f, 0.5f));
            sep.sizeDelta = new Vector2(440, 2);
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
