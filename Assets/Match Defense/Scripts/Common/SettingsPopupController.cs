using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MatchDefense.UI
{
    public class SettingsPopupController
    {
        private const string HideClass = "hide";

        private readonly bool _isPlaySceneMode;
        private readonly Action _onCloseRequested;
        private readonly Action _onRestartRequested;
        private readonly Action _onLobbyRequested;

        private VisualElement _overlay;
        private Label _titleLabel;
        private Button _closeBtn;
        private Slider _bgmSlider;
        private Slider _sfxSlider;
        private Toggle _vibrationToggle;
        private Button _languageBtn;
        private Button _resetDataBtn;

        private VisualElement _playActionsGroup;
        private Button _resumeBtn;
        private Button _restartBtn;
        private Button _lobbyBtn;

        private VisualElement _resetConfirmOverlay;
        private Label _resetConfirmText;
        private Button _resetCancelBtn;
        private Button _resetConfirmBtn;

        public bool IsOpen => _overlay != null && !_overlay.ClassListContains(HideClass);

        public SettingsPopupController(
            bool isPlaySceneMode,
            Action onCloseRequested = null,
            Action onRestartRequested = null,
            Action onLobbyRequested = null)
        {
            _isPlaySceneMode = isPlaySceneMode;
            _onCloseRequested = onCloseRequested;
            _onRestartRequested = onRestartRequested;
            _onLobbyRequested = onLobbyRequested;
        }

        public void Mount(VisualElement root, VisualTreeAsset settingsPopupAsset)
        {
            Unbind();
            if (root == null || settingsPopupAsset == null) return;

            _overlay = root.Q<VisualElement>("pause-overlay");
            if (_overlay == null)
            {
                TemplateContainer container = settingsPopupAsset.Instantiate();
                container.style.position = Position.Absolute;
                container.style.left = 0;
                container.style.right = 0;
                container.style.top = 0;
                container.style.bottom = 0;
                container.pickingMode = PickingMode.Ignore;
                root.Add(container);
                _overlay = container.Q<VisualElement>("pause-overlay");
            }

            if (_overlay == null) return;

            _titleLabel = _overlay.Q<Label>("pause-title");
            _closeBtn = _overlay.Q<Button>("pause-close-btn");
            _bgmSlider = _overlay.Q<Slider>("bgm-slider");
            _sfxSlider = _overlay.Q<Slider>("sfx-slider");
            _vibrationToggle = _overlay.Q<Toggle>("vibration-toggle");
            _languageBtn = _overlay.Q<Button>("language-btn");
            _resetDataBtn = _overlay.Q<Button>("reset-data-btn");

            _playActionsGroup = _overlay.Q<VisualElement>("pause-actions");
            _resumeBtn = _overlay.Q<Button>("resume-btn");
            _restartBtn = _overlay.Q<Button>("restart-btn");
            _lobbyBtn = _overlay.Q<Button>("lobby-btn");

            _resetConfirmOverlay = _overlay.Q<VisualElement>("reset-confirm-overlay");
            _resetConfirmText = _overlay.Q<Label>("reset-confirm-text");
            _resetCancelBtn = _overlay.Q<Button>("reset-cancel-btn");
            _resetConfirmBtn = _overlay.Q<Button>("reset-confirm-btn");

            if (_playActionsGroup != null)
                _playActionsGroup.style.display = _isPlaySceneMode ? DisplayStyle.Flex : DisplayStyle.None;

            _overlay.RegisterCallback<ClickEvent>(OnOverlayClicked);
            if (_closeBtn != null) _closeBtn.clicked += Close;
            if (_resumeBtn != null) _resumeBtn.clicked += Close;
            if (_restartBtn != null) _restartBtn.clicked += OnRestartClicked;
            if (_lobbyBtn != null) _lobbyBtn.clicked += OnLobbyClicked;
            if (_languageBtn != null) _languageBtn.clicked += OnLanguageClicked;
            if (_resetDataBtn != null) _resetDataBtn.clicked += ShowResetConfirm;
            if (_resetCancelBtn != null) _resetCancelBtn.clicked += HideResetConfirm;
            if (_resetConfirmBtn != null) _resetConfirmBtn.clicked += OnConfirmResetData;

            _bgmSlider?.RegisterValueChangedCallback(OnBgmChanged);
            _sfxSlider?.RegisterValueChangedCallback(OnSfxChanged);
            _vibrationToggle?.RegisterValueChangedCallback(OnVibrationChanged);

            if (UIManager.Instance != null)
                UIManager.Instance.OnLanguageChanged += RefreshLocalizedLabels;

            SyncValuesFromProgramManager();
            RefreshLocalizedLabels();
            SetVisible(false);
        }

        public void Unbind()
        {
            _overlay?.UnregisterCallback<ClickEvent>(OnOverlayClicked);
            if (_closeBtn != null) _closeBtn.clicked -= Close;
            if (_resumeBtn != null) _resumeBtn.clicked -= Close;
            if (_restartBtn != null) _restartBtn.clicked -= OnRestartClicked;
            if (_lobbyBtn != null) _lobbyBtn.clicked -= OnLobbyClicked;
            if (_languageBtn != null) _languageBtn.clicked -= OnLanguageClicked;
            if (_resetDataBtn != null) _resetDataBtn.clicked -= ShowResetConfirm;
            if (_resetCancelBtn != null) _resetCancelBtn.clicked -= HideResetConfirm;
            if (_resetConfirmBtn != null) _resetConfirmBtn.clicked -= OnConfirmResetData;

            _bgmSlider?.UnregisterValueChangedCallback(OnBgmChanged);
            _sfxSlider?.UnregisterValueChangedCallback(OnSfxChanged);
            _vibrationToggle?.UnregisterValueChangedCallback(OnVibrationChanged);

            if (UIManager.Instance != null)
                UIManager.Instance.OnLanguageChanged -= RefreshLocalizedLabels;

            _overlay = null;
        }

        public void Open()
        {
            SyncValuesFromProgramManager();
            RefreshLocalizedLabels();
            HideResetConfirm();
            SetVisible(true);
            _overlay?.BringToFront();
        }

        public void Close()
        {
            HideResetConfirm();
            SetVisible(false);
            _onCloseRequested?.Invoke();
        }

        public void SetVisible(bool visible)
        {
            if (_overlay == null) return;
            _overlay.EnableInClassList(HideClass, !visible);
        }

        private void SyncValuesFromProgramManager()
        {
            if (ProgramManager.Instance == null) return;
            _bgmSlider?.SetValueWithoutNotify(ProgramManager.Instance.BgmVolume);
            _sfxSlider?.SetValueWithoutNotify(ProgramManager.Instance.SfxVolume);
            _vibrationToggle?.SetValueWithoutNotify(ProgramManager.Instance.IsVibrationEnabled);
        }

        private void RefreshLocalizedLabels()
        {
            if (_titleLabel != null)
            {
                string key = _isPlaySceneMode ? "pause_title" : "setting_title";
                _titleLabel.text = GetTextOrFallback(key, _isPlaySceneMode ? "PAUSE" : "SETTINGS");
            }

            if (_languageBtn != null)
                _languageBtn.text = GetTextOrFallback("setting_lang_value", "KO / EN");

            if (_resetDataBtn != null)
                _resetDataBtn.text = GetTextOrFallback("setting_reset_btn", "RESET");

            if (_resetConfirmText != null)
                _resetConfirmText.text = GetTextOrFallback("setting_reset_confirm_msg", "Reset all game data?");

            if (_resetCancelBtn != null)
                _resetCancelBtn.text = GetTextOrFallback("common_cancel", "CANCEL");

            if (_resetConfirmBtn != null)
                _resetConfirmBtn.text = GetTextOrFallback("common_confirm", "CONFIRM");

            if (_resumeBtn != null)
                _resumeBtn.text = GetTextOrFallback("pause_resume", "RESUME");

            if (_restartBtn != null)
                _restartBtn.text = GetTextOrFallback("pause_restart", "RESTART");

            if (_lobbyBtn != null)
                _lobbyBtn.text = GetTextOrFallback("pause_lobby", "LOBBY");
        }

        private static string GetTextOrFallback(string key, string fallback)
        {
            if (UIManager.Instance == null) return fallback;
            string result = UIManager.Instance.GetText(key);
            return string.IsNullOrEmpty(result) || result.StartsWith("#") ? fallback : result;
        }

        private void OnOverlayClicked(ClickEvent evt)
        {
            if (evt.target == _overlay)
                Close();
        }

        private void OnBgmChanged(ChangeEvent<float> evt) => ProgramManager.Instance?.SetBgmVolume(evt.newValue);
        private void OnSfxChanged(ChangeEvent<float> evt) => ProgramManager.Instance?.SetSfxVolume(evt.newValue);
        private void OnVibrationChanged(ChangeEvent<bool> evt) => ProgramManager.Instance?.SetVibrationEnabled(evt.newValue);
        private void OnLanguageClicked() => UIManager.Instance?.SwitchLanguage();
        private void OnRestartClicked() => _onRestartRequested?.Invoke();
        private void OnLobbyClicked() => _onLobbyRequested?.Invoke();

        private void ShowResetConfirm()
        {
            _resetConfirmOverlay?.RemoveFromClassList(HideClass);
        }

        private void HideResetConfirm()
        {
            _resetConfirmOverlay?.AddToClassList(HideClass);
        }

        private void OnConfirmResetData()
        {
            HideResetConfirm();
            ProgramManager.Instance?.ResetAllPlayerData();
            Close();

            Time.timeScale = 1f;
            SceneManager.LoadScene("Lobby");
        }
    }
}
