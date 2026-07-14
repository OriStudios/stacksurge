using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using StackSurge.Core;
using StackSurge.Settings;
using StackSurge.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using DG.Tweening;

namespace StackSurge.UI
{
    public class GameView : MonoBehaviour
    {
        StackSurgeSettings _settings;
        Image[,] _cellImages;
        RectTransform _gridRoot;
        [SerializeField] TextMeshProUGUI _scoreText;
        [SerializeField] TextMeshProUGUI _hudText;
        [SerializeField] GameObject _instructionalText;

        // Visual Previews
        [SerializeField] Image _nextPreviewImg;
        [SerializeField] Image _queuedPreviewImg;
        [SerializeField] Image _nextInnerIcon;
        [SerializeField] Image _queuedInnerIcon;
        [SerializeField] Sprite _bombSprite;
        [SerializeField] Sprite _wildSprite;

        // Column buttons
        //[SerializeField] ColumnButtonView _columnButtonPrefab;
        //[SerializeField] Transform _columnButtonsRoot;

        [SerializeField] GameObject _helpPanel;
        [SerializeField] Button _closeHelpButton;
        [SerializeField] Canvas _mainCanvas;
        [SerializeField] GameObject _hudRoot;
        [SerializeField] GameObject _loadingRoot;
        [SerializeField] TextMeshProUGUI _loadingTitleText;
        [SerializeField] TextMeshProUGUI _loadingStatusText;
        [SerializeField] Image[] _loadingGlows;
        [SerializeField] GameObject _gameOverRoot;
        [SerializeField] TextMeshProUGUI _gameOverScore;
        [SerializeField] Button _retryButton;
        [SerializeField] Button _shareButton;

        [SerializeField] GameObject _challengesRoot;
        [SerializeField] GameObject _challengeRowPrefab;
        [SerializeField] TextMeshProUGUI _challengesBody;
        [SerializeField] Button _challengesButton;
        [SerializeField] Button _helpButton;
        [SerializeField] Button _leaderboardButton;

        [Header("Icons")]
        [SerializeField] Sprite completedIcon;
        [SerializeField] Sprite pendingIcon;

        // ── Main Menu Serialized Fields ──────────────────────────────────────
        [Header("Main Menu")]
        [SerializeField] GameObject _mainMenuRoot;

        [Header("Main Menu – Buttons")]
        [SerializeField] Button _mainMenuPlayButton;
        [SerializeField] Button _mainMenuChallengesButton;   // replaces Tutorial
        [SerializeField] Button _mainMenuLeaderboardButton;
        [SerializeField] Button _mainMenuHowToPlayButton;
        [SerializeField] Toggle _mainMenuVibrationToggle;    // placed wherever suits the UI

        [Header("Main Menu – Stats Card")]
        [SerializeField] TextMeshProUGUI _mainMenuAllTimeHighText;
        [SerializeField] TextMeshProUGUI _mainMenuDailyBestText;
        [SerializeField] TextMeshProUGUI _mainMenuStreakText;
        [SerializeField] TextMeshProUGUI _mainMenuPlayerNameText;
        [SerializeField] TMP_InputField _mainMenuNameInput;
        [SerializeField] Button _mainMenuSaveNameButton;
        // ────────────────────────────────────────────────────────────────────

        // Vibration runtime state (persisted via PlayerPrefs)
        private bool _vibrationEnabled = true;

        ScreenShake _shake;

        Action<int> _onColumnClicked;
        Action _onRetry;
        Action _onMainMenu;
        Func<ChallengeDisplayData[]> _getChallengesData;
        Func<TimeSpan> _getTimeUntilReset;
        Action<int> _onClaimReward;
        Func<SaveData> _getSaveData;
        Action<string> _onPlayerNameChanged;
        Action _onPlayGame;
        Action _onPlayTutorial;
        Action _onToggleLeaderboard;

        [SerializeField] Transform _challengeRowContainer;
        TextMeshProUGUI _titleText;
        Coroutine _resetTimerCoroutine;
        bool _challengesOpenedFromMainMenu;

        private int _lastScore = 0;

        private Color completedColor = new(0.29f, 0.87f, 0.50f);
        private Color pendingColor = new(0.29f, 0.33f, 0.39f);

        // ── Tutorial UI Elements ─────────────────────────────────────────────
        private GameObject _tutorialDialogGo;
        private TextMeshProUGUI _tutorialTitleText;
        private TextMeshProUGUI _tutorialBodyText;
        private Button _tutorialNextBtn;
        private bool _tutorialNextClicked;
        private Button[] _colOverlayBtns;   // full-height transparent click zones

        public Action OnReplayTutorialTriggered;

        void Awake()
        {
            if (_closeHelpButton != null) _closeHelpButton.onClick.AddListener(ToggleHelp);
        }

        public void Build(
            StackSurgeSettings settings,
            Action<int> onColumnClicked,
            Action onRetry,
            Action onMainMenu,
            Func<ChallengeDisplayData[]> getChallengesData,
            Func<TimeSpan> getTimeUntilReset,
            Action<int> onClaimReward,
            Func<SaveData> getSaveData,
            Action<string> onPlayerNameChanged,
            Action onPlayGame,
            Action onPlayTutorial,
            Action onToggleLeaderboard)
        {
            _settings = settings;
            _onColumnClicked = onColumnClicked;
            _onRetry = onRetry;
            _onMainMenu = onMainMenu;
            _getChallengesData = getChallengesData;
            _getTimeUntilReset = getTimeUntilReset;
            _onClaimReward = onClaimReward;
            _getSaveData = getSaveData;
            _onPlayerNameChanged = onPlayerNameChanged;
            _onPlayGame = onPlayGame;
            _onPlayTutorial = onPlayTutorial;
            _onToggleLeaderboard = onToggleLeaderboard;

            EnsureInputSystemUi();
            BuildUi();

            DOTween.SetTweensCapacity(500, 50);
            _retryButton.onClick.AddListener(Retry);

            if (_shareButton != null)
            {
                _shareButton.onClick.RemoveAllListeners();
                _shareButton.onClick.AddListener(GoToMainMenu);
                var shareTxt = _shareButton.GetComponentInChildren<TextMeshProUGUI>();
                if (shareTxt != null) shareTxt.text = "MAIN MENU";
            }

            if (_challengesButton != null) _challengesButton.onClick.AddListener(ToggleChallenges);
            if (_helpButton != null) _helpButton.onClick.AddListener(ToggleHelp);

            if (_challengesBody != null)
                _challengesBody.gameObject.SetActive(false);

            if (_challengesRoot != null)
                _titleText = _challengesRoot.transform.Find("Title")?.GetComponent<TextMeshProUGUI>();

            // Append Replay Tutorial Button to Help Panel
            if (_helpPanel != null)
            {
                var oldBtn = _helpPanel.transform.Find("ReplayTutorialBtn");
                if (oldBtn != null) Destroy(oldBtn.gameObject);

                var replayBtnGo = new GameObject("ReplayTutorialBtn");
                replayBtnGo.transform.SetParent(_helpPanel.transform, false);
                var rt = replayBtnGo.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(0f, 60f);
                rt.sizeDelta = new Vector2(340f, 80f);

                var img = replayBtnGo.AddComponent<Image>();
                img.color = new Color(0.95f, 0.75f, 0.2f, 0.95f);

                var outline = replayBtnGo.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.3f);
                outline.effectDistance = new Vector2(2f, 2f);

                var btn = replayBtnGo.AddComponent<Button>();
                btn.onClick.AddListener(() =>
                {
                    ToggleHelp();
                    OnReplayTutorialTriggered?.Invoke();
                });

                var txtGo = new GameObject("Text");
                txtGo.transform.SetParent(replayBtnGo.transform, false);
                var txtRt = txtGo.AddComponent<RectTransform>();
                txtRt.anchorMin = Vector2.zero;
                txtRt.anchorMax = Vector2.one;
                txtRt.sizeDelta = Vector2.zero;
                var txt = txtGo.AddComponent<TextMeshProUGUI>();
                txt.text = "REPLAY TUTORIAL";
                txt.fontSize = 24;
                txt.fontStyle = FontStyles.Bold;
                txt.color = Color.black;
                txt.alignment = TextAlignmentOptions.Center;
            }

            // Wire up the main menu buttons now that delegates are assigned
            InitMainMenu();
        }

        // Main Menu Init
        /// <summary>
        /// Wires up all delegates and initial state for the main menu.
        /// All GameObjects are assigned in the Inspector; this method only
        /// adds listeners and sets starting visibility.
        /// </summary>
        private void InitMainMenu()
        {
            if (_mainMenuRoot == null)
            {
                Debug.LogError("[GameView] _mainMenuRoot is not assigned in the Inspector!");
                return;
            }

            // Start hidden – shown after the loading screen completes
            _mainMenuRoot.SetActive(false);

            // Ensure a CanvasGroup exists for fade animations
            if (_mainMenuRoot.GetComponent<CanvasGroup>() == null)
                _mainMenuRoot.AddComponent<CanvasGroup>();

            // PLAY GAME
            if (_mainMenuPlayButton != null)
            {
                _mainMenuPlayButton.onClick.RemoveAllListeners();
                _mainMenuPlayButton.onClick.AddListener(() =>
                {
                    HideMainMenu();
                    _onPlayGame?.Invoke();
                });
            }
            else Debug.LogWarning("[GameView] _mainMenuPlayButton not assigned.");

            // DAILY CHALLENGES
            if (_mainMenuChallengesButton != null)
            {
                _mainMenuChallengesButton.onClick.RemoveAllListeners();
                _mainMenuChallengesButton.onClick.AddListener(() =>
                {
                    // Open challenges ON TOP of the main menu (mirrors How to Play behaviour)
                    _challengesOpenedFromMainMenu = true;
                    ToggleChallenges();
                });
            }
            else Debug.LogWarning("[GameView] _mainMenuChallengesButton not assigned.");

            // LEADERBOARD
            if (_mainMenuLeaderboardButton != null)
            {
                _mainMenuLeaderboardButton.onClick.RemoveAllListeners();
                _mainMenuLeaderboardButton.onClick.AddListener(() => _onToggleLeaderboard?.Invoke());
            }
            else Debug.LogWarning("[GameView] _mainMenuLeaderboardButton not assigned.");

            // HOW TO PLAY
            if (_mainMenuHowToPlayButton != null)
            {
                _mainMenuHowToPlayButton.onClick.RemoveAllListeners();
                _mainMenuHowToPlayButton.onClick.AddListener(ToggleHelp);
            }
            else Debug.LogWarning("[GameView] _mainMenuHowToPlayButton not assigned.");

            // VIBRATION TOGGLE  — assign _mainMenuVibrationToggle in the Inspector
            _vibrationEnabled = PlayerPrefs.GetInt("VibrationEnabled", 1) == 1;
            if (_mainMenuVibrationToggle != null)
            {
                // Set without firing the callback first
                _mainMenuVibrationToggle.onValueChanged.RemoveAllListeners();
                _mainMenuVibrationToggle.isOn = _vibrationEnabled;
                _mainMenuVibrationToggle.onValueChanged.AddListener(isOn =>
                {
                    _vibrationEnabled = isOn;
                    PlayerPrefs.SetInt("VibrationEnabled", _vibrationEnabled ? 1 : 0);
                    PlayerPrefs.Save();
                });
            }

            // SAVE NAME
            if (_mainMenuSaveNameButton != null)
            {
                _mainMenuSaveNameButton.onClick.RemoveAllListeners();
                _mainMenuSaveNameButton.onClick.AddListener(() =>
                {
                    if (_mainMenuNameInput != null)
                    {
                        _onPlayerNameChanged?.Invoke(_mainMenuNameInput.text);
                        _mainMenuNameInput.text = "";
                    }
                });
            }
            else Debug.LogWarning("[GameView] _mainMenuSaveNameButton not assigned.");

            // Name input – submit on keyboard confirm as well
            if (_mainMenuNameInput != null)
            {
                _mainMenuNameInput.onSubmit.RemoveAllListeners();
                _mainMenuNameInput.onSubmit.AddListener(val =>
                {
                    _onPlayerNameChanged?.Invoke(val);
                    _mainMenuNameInput.text = "";
                });
            }
        }



        /// <summary>
        /// Call with <c>true</c> when the interactive tutorial starts and <c>false</c> when it ends.
        /// Locks every HUD/menu button that should not be reachable mid-tutorial so only the
        /// tutorial-controlled column highlight can receive input.
        /// </summary>
        public void SetTutorialActive(bool tutorialActive)
        {
            bool canInteract = !tutorialActive;

            // HUD overlay buttons
            if (_challengesButton != null) _challengesButton.interactable = canInteract;
            if (_helpButton != null) _helpButton.interactable = canInteract;
            if (_leaderboardButton != null) _leaderboardButton.interactable = canInteract;

            // Main menu buttons (usually hidden during tutorial, but guard anyway)
            if (_mainMenuPlayButton != null) _mainMenuPlayButton.interactable = canInteract;
            if (_mainMenuChallengesButton != null) _mainMenuChallengesButton.interactable = canInteract;
            if (_mainMenuLeaderboardButton != null) _mainMenuLeaderboardButton.interactable = canInteract;
            if (_mainMenuHowToPlayButton != null) _mainMenuHowToPlayButton.interactable = canInteract;
            if (_mainMenuVibrationToggle != null) _mainMenuVibrationToggle.interactable = canInteract;
            if (_mainMenuSaveNameButton != null) _mainMenuSaveNameButton.interactable = canInteract;
        }

        public void SetInstructionalTextActive(bool active)
        {
            if (_instructionalText == null) return;

            if (active)
            {
                var cg = _instructionalText.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1f;
                _instructionalText.SetActive(true);
            }
            else
            {
                var cg = _instructionalText.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.DOKill();
                    cg.DOFade(0f, 0.4f).SetUpdate(true).OnComplete(() => _instructionalText.SetActive(false));
                }
                else
                {
                    _instructionalText.SetActive(false);
                }
            }
        }
        // ────────────────────────────────────────────────────────────────────

        public void UpdateHud(int score, string timeStr, string riseLine, float wildChance, TileKind current, TileKind next)
        {
            if (_scoreText == null) return;

            if (score != _lastScore)
            {
                _scoreText.text = score.ToString();
                _scoreText.transform.DOKill();
                _scoreText.transform.localScale = Vector3.one;
                _scoreText.transform.DOPunchScale(Vector3.one * 0.15f, 0.3f, 10, 1f);
                _lastScore = score;
            }

            _hudText.text =
                $"Time: <color=#A0A0A0>{timeStr}</color>\n" +
                $"<color=#60A5FA>{riseLine}</color>";

            UpdatePreviewSquare(_nextPreviewImg, _nextInnerIcon, current);
            UpdatePreviewSquare(_queuedPreviewImg, _queuedInnerIcon, next);
        }

        private void UpdatePreviewSquare(Image img, Image innerIcon, TileKind k)
        {
            if (img == null || innerIcon == null) return;

            img.color = ColorFor(k);

            if (k == TileKind.Wild)
            {
                innerIcon.sprite = _wildSprite;
                //innerIcon.color = new Color(0.36f, 0.2f, 0.55f); // deep violet, reads clearly on white
                innerIcon.color = new Color(0.0f, 0.0f, 0.0f);
                innerIcon.enabled = true;
                img.transform.DOKill();
                img.transform.DOScale(1.1f, 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
            }
            else if (k == TileKind.Bomb)
            {
                innerIcon.sprite = _bombSprite;
                innerIcon.color = Color.white;
                innerIcon.enabled = true;
                img.transform.DOKill();
                img.transform.localScale = Vector3.one;
            }
            else
            {
                innerIcon.enabled = false;
                img.transform.DOKill();
                img.transform.localScale = Vector3.one;
            }
        }

        public void ShowGameOver(int score, int dailyBest, int allTimeHigh, int streak)
        {
            _hudRoot.SetActive(false);

            _gameOverScore.text = $"Score: <size=120%>{score}</size>\n\n" +
                                  $"Daily Best: {dailyBest}\n" +
                                  $"All-time: {allTimeHigh}\n" +
                                  $"Streak: {streak} {(streak == 1 ? "day" : "days")}";

            _gameOverRoot.SetActive(true);
            var cg = _gameOverRoot.GetComponent<CanvasGroup>();
            cg.alpha = 0;
            cg.DOFade(1f, 0.4f);
            _gameOverRoot.transform.localScale = Vector3.one * 0.9f;
            _gameOverRoot.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
        }

        public void HideGameOver()
        {
            if (_gameOverRoot != null) _gameOverRoot.SetActive(false);
            if (_hudRoot != null) _hudRoot.SetActive(true);
        }

        public void Retry() => _onRetry?.Invoke();
        public void GoToMainMenu() => _onMainMenu?.Invoke();

        public void ShowMainMenu(bool animate = true)
        {
            if (_hudRoot != null) _hudRoot.SetActive(false);
            if (_gameOverRoot != null) _gameOverRoot.SetActive(false);
            if (_helpPanel != null) _helpPanel.SetActive(false);
            if (_challengesRoot != null) _challengesRoot.SetActive(false);

            if (_mainMenuRoot != null)
            {
                _mainMenuRoot.SetActive(true);
                RefreshMainMenuStats();

                var cg = _mainMenuRoot.GetComponent<CanvasGroup>();
                if (animate)
                {
                    if (cg != null)
                    {
                        cg.alpha = 0f;
                        cg.DOFade(1f, 0.4f).SetUpdate(true);
                    }
                    _mainMenuRoot.transform.localScale = Vector3.one * 0.9f;
                    _mainMenuRoot.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack).SetUpdate(true);
                }
                else
                {
                    if (cg != null) cg.alpha = 1f;
                    _mainMenuRoot.transform.localScale = Vector3.one;
                }
            }
        }

        public void HideMainMenu()
        {
            if (_hudRoot != null) _hudRoot.SetActive(true);
            if (_mainMenuRoot != null)
            {
                var cg = _mainMenuRoot.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.DOFade(0f, 0.3f).SetUpdate(true).OnComplete(() => _mainMenuRoot.SetActive(false));
                }
                else
                {
                    _mainMenuRoot.SetActive(false);
                }
            }
        }

        public void RefreshMainMenuStats()
        {
            if (_getSaveData == null) return;
            var save = _getSaveData();
            if (save == null) return;

            if (_mainMenuAllTimeHighText != null)
                _mainMenuAllTimeHighText.text = save.AllTimeHigh.ToString();

            if (_mainMenuDailyBestText != null)
                _mainMenuDailyBestText.text = save.DailyBest.ToString();

            if (_mainMenuStreakText != null)
                _mainMenuStreakText.text = $"{save.Streak} {(save.Streak == 1 ? "Day" : "Days")}";

            /*
            if (_mainMenuNameInput != null)
            {
                _mainMenuNameInput.text = save.PlayerDisplayName ?? "";
            }*/

            if (_mainMenuPlayerNameText != null)
            {
                _mainMenuPlayerNameText.text = save.PlayerDisplayName ?? "";
            }
        }

        public void ToggleHelp()
        {
            if (_helpPanel != null) _helpPanel.SetActive(!_helpPanel.activeSelf);
        }

        public void ToggleChallenges()
        {
            if (_challengesRoot == null) return;
            bool active = !_challengesRoot.activeSelf;
            _challengesRoot.SetActive(active);

            // Only touch timeScale when in-game (not when opened from the main menu)
            if (!_challengesOpenedFromMainMenu)
                Time.timeScale = active ? 0f : 1f;

            if (active)
            {
                var cg = _challengesRoot.GetComponent<CanvasGroup>();
                if (cg == null) cg = _challengesRoot.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                cg.DOFade(1f, 0.25f).SetUpdate(true);
                _challengesRoot.transform.localScale = Vector3.one * 0.9f;
                _challengesRoot.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetUpdate(true);

                PopulateChallenges();

                if (_resetTimerCoroutine != null) StopCoroutine(_resetTimerCoroutine);
                _resetTimerCoroutine = StartCoroutine(UpdateResetTimerCoroutine());

                var closeBtn = _challengesRoot.GetComponentInChildren<Button>();
                if (closeBtn != null && closeBtn != _challengesButton)
                {
                    closeBtn.onClick.RemoveAllListeners();
                    closeBtn.onClick.AddListener(ToggleChallenges);
                }
            }
            else
            {
                if (_resetTimerCoroutine != null)
                {
                    StopCoroutine(_resetTimerCoroutine);
                    _resetTimerCoroutine = null;
                }

                // If we came from the main menu, return to it instead of the game
                if (_challengesOpenedFromMainMenu)
                {
                    _challengesOpenedFromMainMenu = false;
                    ShowMainMenu(animate: false);
                }
            }
        }

        private IEnumerator UpdateResetTimerCoroutine()
        {
            while (true)
            {
                if (_titleText != null && _getTimeUntilReset != null)
                {
                    var timeRemaining = _getTimeUntilReset();
                    _titleText.text = $"DAILY CHALLENGES\n<size=50%><color=#A0A0A0>Resets in {timeRemaining.Hours:D2}h {timeRemaining.Minutes:D2}m {timeRemaining.Seconds:D2}s</color></size>";
                }
                yield return new WaitForSecondsRealtime(1.0f);
            }
        }

        void PopulateChallenges()
        {
            if (_challengeRowContainer == null || _getChallengesData == null) return;

            foreach (Transform child in _challengeRowContainer)
                Destroy(child.gameObject);

            var data = _getChallengesData();
            if (data == null) return;

            float rowHeight = 110f;
            float startY = (data.Length - 1) * rowHeight * 0.5f;

            for (int i = 0; i < data.Length; i++)
            {
                int index = i;
                var item = data[i];

                GameObject rowGo;
                RectTransform rowRt;

                if (_challengeRowPrefab != null)
                {
                    rowGo = Instantiate(_challengeRowPrefab, _challengeRowContainer, false);
                    rowRt = rowGo.GetComponent<RectTransform>();
                    if (rowRt != null && _challengeRowContainer.GetComponent<UnityEngine.UI.LayoutGroup>() == null)
                        rowRt.anchoredPosition = new Vector2(0, startY - i * rowHeight);

                    var rowView = rowGo.GetComponent<ChallengeRowView>();
                    if (rowView != null)
                    {
                        if (rowView.TitleText != null) rowView.TitleText.text = item.Title;
                        if (rowView.DescriptionText != null) rowView.DescriptionText.text = item.Description;
                        if (rowView.StatusIcon != null)
                        {
                            rowView.StatusIcon.sprite = item.Completed ? completedIcon : pendingIcon;
                            rowView.StatusIcon.color = item.Completed ? completedColor : pendingColor;
                        }
                        if (rowView.ProgressText != null)
                            rowView.ProgressText.text = $"{item.Progress} / {item.TargetValue}";

                        float ratio = item.TargetValue > 0 ? (float)item.Progress / item.TargetValue : 0f;

                        if (rowView.ProgressBarFill != null)
                        {
                            rowView.ProgressBarFill.fillAmount = 0f;
                            rowView.ProgressBarFill.DOFillAmount(ratio, 0.75f).SetEase(Ease.OutQuad).SetUpdate(true);
                            rowView.ProgressBarFill.color = Color.Lerp(new Color(0.2f, 0.6f, 1.0f), new Color(0.3f, 0.8f, 0.4f), ratio);
                        }

                        if (item.Completed)
                        {
                            if (item.Claimed)
                            {
                                if (rowView.ClaimButton != null) rowView.ClaimButton.gameObject.SetActive(false);
                                if (rowView.RewardText != null)
                                {
                                    rowView.RewardText.gameObject.SetActive(true);
                                    rowView.RewardText.text = "CLAIMED";
                                    rowView.RewardText.color = new Color(0.5f, 0.5f, 0.5f, 0.8f);
                                }
                            }
                            else
                            {
                                if (rowView.ClaimButton != null)
                                {
                                    rowView.ClaimButton.gameObject.SetActive(true);
                                    rowView.ClaimButton.onClick.RemoveAllListeners();
                                    rowView.ClaimButton.onClick.AddListener(() =>
                                    {
                                        _onClaimReward?.Invoke(index);
                                        PopulateChallenges();
                                    });
                                    rowView.ClaimButton.transform.DOScale(1.08f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true);
                                }
                                if (rowView.RewardText != null) rowView.RewardText.gameObject.SetActive(false);
                            }
                        }
                        else
                        {
                            if (rowView.ClaimButton != null) rowView.ClaimButton.gameObject.SetActive(false);
                            if (rowView.RewardText != null)
                            {
                                rowView.RewardText.gameObject.SetActive(true);
                                rowView.RewardText.text = $"+{item.RewardPoints} pts";
                                rowView.RewardText.color = new Color(0.95f, 0.75f, 0.2f, 0.8f);
                            }
                        }

                        if (rowView.BackgroundImage != null)
                        {
                            rowView.BackgroundImage.color = item.Completed
                                ? new Color(1f, 0.85f, 0.4f, 0.15f)
                                : new Color(0.1f, 0.1f, 0.12f, 0.6f);
                        }
                    }
                }
                else
                {
                    rowGo = new GameObject($"ChallengeRow_{i}");
                    rowGo.transform.SetParent(_challengeRowContainer, false);
                    rowRt = rowGo.AddComponent<RectTransform>();
                    rowRt.anchorMin = new Vector2(0, 0.5f);
                    rowRt.anchorMax = new Vector2(1, 0.5f);
                    rowRt.pivot = new Vector2(0.5f, 0.5f);
                    rowRt.anchoredPosition = new Vector2(0, startY - i * rowHeight);
                    rowRt.sizeDelta = new Vector2(0, 100f);

                    var bgImg = rowGo.AddComponent<Image>();
                    bgImg.color = item.Completed
                        ? new Color(1f, 0.85f, 0.4f, 0.15f)
                        : new Color(0.1f, 0.1f, 0.12f, 0.6f);

                    var statusGo = new GameObject("StatusIcon");
                    statusGo.transform.SetParent(rowGo.transform, false);
                    var statusRt = statusGo.AddComponent<RectTransform>();
                    statusRt.anchorMin = new Vector2(0, 0.5f);
                    statusRt.anchorMax = new Vector2(0, 0.5f);
                    statusRt.pivot = new Vector2(0, 0.5f);
                    statusRt.anchoredPosition = new Vector2(20, 0);
                    statusRt.sizeDelta = new Vector2(50, 80);
                    var statusText = statusGo.AddComponent<TextMeshProUGUI>();
                    statusText.text = item.Completed ? "<color=#4ADE80>✓</color>" : "<color=#4B5563>○</color>";
                    statusText.fontSize = 32;
                    statusText.alignment = TextAlignmentOptions.Center;

                    var textColGo = new GameObject("TextColumn");
                    textColGo.transform.SetParent(rowGo.transform, false);
                    var textColRt = textColGo.AddComponent<RectTransform>();
                    textColRt.anchorMin = new Vector2(0, 0.5f);
                    textColRt.anchorMax = new Vector2(1, 0.5f);
                    textColRt.pivot = new Vector2(0, 0.5f);
                    float rightOffset = (item.Completed && !item.Claimed) ? -180f : -150f;
                    textColRt.anchoredPosition = new Vector2(80, 10);
                    textColRt.sizeDelta = new Vector2(rightOffset - 80f, 80);

                    var titleGo2 = new GameObject("Title");
                    titleGo2.transform.SetParent(textColGo.transform, false);
                    var titleRt2 = titleGo2.AddComponent<RectTransform>();
                    titleRt2.anchorMin = new Vector2(0, 1);
                    titleRt2.anchorMax = new Vector2(1, 1);
                    titleRt2.pivot = new Vector2(0, 1);
                    titleRt2.anchoredPosition = new Vector2(0, 0);
                    titleRt2.sizeDelta = new Vector2(0, 35);
                    var titleText2 = titleGo2.AddComponent<TextMeshProUGUI>();
                    titleText2.text = item.Title;
                    titleText2.fontSize = 24;
                    titleText2.fontStyle = FontStyles.Bold;
                    titleText2.color = Color.white;

                    var descGo = new GameObject("Description");
                    descGo.transform.SetParent(textColGo.transform, false);
                    var descRt = descGo.AddComponent<RectTransform>();
                    descRt.anchorMin = new Vector2(0, 0);
                    descRt.anchorMax = new Vector2(1, 0);
                    descRt.pivot = new Vector2(0, 0);
                    descRt.anchoredPosition = new Vector2(0, 0);
                    descRt.sizeDelta = new Vector2(0, 30);
                    var descText = descGo.AddComponent<TextMeshProUGUI>();
                    descText.text = item.Description;
                    descText.fontSize = 18;
                    descText.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);

                    var progTextGo = new GameObject("ProgressText");
                    progTextGo.transform.SetParent(rowGo.transform, false);
                    var progTextRt = progTextGo.AddComponent<RectTransform>();
                    progTextRt.anchorMin = new Vector2(1, 0.5f);
                    progTextRt.anchorMax = new Vector2(1, 0.5f);
                    progTextRt.pivot = new Vector2(1, 0.5f);
                    float progTextRightOffset = (item.Completed && !item.Claimed) ? -180f : -20f;
                    progTextRt.anchoredPosition = new Vector2(progTextRightOffset, 15);
                    progTextRt.sizeDelta = new Vector2(150, 40);
                    var progText = progTextGo.AddComponent<TextMeshProUGUI>();
                    progText.text = $"{item.Progress} / {item.TargetValue}";
                    progText.fontSize = 20;
                    progText.alignment = TextAlignmentOptions.Right;
                    progText.color = new Color(0.9f, 0.9f, 0.9f);

                    var barBgGo = new GameObject("ProgressBarBg");
                    barBgGo.transform.SetParent(rowGo.transform, false);
                    var barBgRt = barBgGo.AddComponent<RectTransform>();
                    barBgRt.anchorMin = new Vector2(0, 0);
                    barBgRt.anchorMax = new Vector2(1, 0);
                    barBgRt.pivot = new Vector2(0.5f, 0);
                    float barBgRightOffset = (item.Completed && !item.Claimed) ? -180f : -20f;
                    barBgRt.anchoredPosition = new Vector2(80, 8);
                    barBgRt.sizeDelta = new Vector2(barBgRightOffset - 80f, 10);
                    var barBgImg = barBgGo.AddComponent<Image>();
                    barBgImg.color = new Color(0.2f, 0.2f, 0.25f, 1f);

                    var barFillGo = new GameObject("ProgressBarFill");
                    barFillGo.transform.SetParent(barBgGo.transform, false);
                    var barFillRt = barFillGo.AddComponent<RectTransform>();
                    barFillRt.anchorMin = new Vector2(0, 0);
                    barFillRt.anchorMax = new Vector2(0, 1);
                    barFillRt.pivot = new Vector2(0, 0.5f);
                    barFillRt.anchoredPosition = Vector2.zero;
                    barFillRt.sizeDelta = new Vector2(0, 0);
                    var barFillImg = barFillGo.AddComponent<Image>();

                    float ratio = item.TargetValue > 0 ? (float)item.Progress / item.TargetValue : 0f;
                    barFillImg.color = Color.Lerp(new Color(0.2f, 0.6f, 1.0f), new Color(0.3f, 0.8f, 0.4f), ratio);

                    float targetWidth = ratio * barBgRt.sizeDelta.x;
                    barFillRt.sizeDelta = new Vector2(0, 0);
                    barFillRt.DOSizeDelta(new Vector2(targetWidth, 0), 0.75f).SetEase(Ease.OutQuad).SetUpdate(true);

                    if (item.Completed)
                    {
                        if (item.Claimed)
                        {
                            var claimedGo = new GameObject("ClaimedLabel");
                            claimedGo.transform.SetParent(rowGo.transform, false);
                            var claimedRt = claimedGo.AddComponent<RectTransform>();
                            claimedRt.anchorMin = new Vector2(1, 0.5f);
                            claimedRt.anchorMax = new Vector2(1, 0.5f);
                            claimedRt.pivot = new Vector2(1, 0.5f);
                            claimedRt.anchoredPosition = new Vector2(-20, 0);
                            claimedRt.sizeDelta = new Vector2(140, 60);
                            var claimedText = claimedGo.AddComponent<TextMeshProUGUI>();
                            claimedText.text = "CLAIMED";
                            claimedText.fontSize = 20;
                            claimedText.alignment = TextAlignmentOptions.Center;
                            claimedText.color = new Color(0.5f, 0.5f, 0.5f, 0.8f);
                        }
                        else
                        {
                            var claimBtnGo = new GameObject("ClaimButton");
                            claimBtnGo.transform.SetParent(rowGo.transform, false);
                            var claimBtnRt = claimBtnGo.AddComponent<RectTransform>();
                            claimBtnRt.anchorMin = new Vector2(1, 0.5f);
                            claimBtnRt.anchorMax = new Vector2(1, 0.5f);
                            claimBtnRt.pivot = new Vector2(1, 0.5f);
                            claimBtnRt.anchoredPosition = new Vector2(-20, 0);
                            claimBtnRt.sizeDelta = new Vector2(140, 60);
                            var claimBtnImg = claimBtnGo.AddComponent<Image>();
                            claimBtnImg.color = new Color(0.95f, 0.75f, 0.2f);
                            var btn = claimBtnGo.AddComponent<Button>();
                            btn.onClick.AddListener(() =>
                            {
                                _onClaimReward?.Invoke(index);
                                PopulateChallenges();
                            });

                            var btnTextGo = new GameObject("Text");
                            btnTextGo.transform.SetParent(claimBtnGo.transform, false);
                            var btnTextRt = btnTextGo.AddComponent<RectTransform>();
                            btnTextRt.anchorMin = Vector2.zero;
                            btnTextRt.anchorMax = Vector2.one;
                            btnTextRt.sizeDelta = Vector2.zero;
                            var btnText = btnTextGo.AddComponent<TextMeshProUGUI>();
                            btnText.text = "CLAIM";
                            btnText.fontSize = 22;
                            btnText.fontStyle = FontStyles.Bold;
                            btnText.alignment = TextAlignmentOptions.Center;
                            btnText.color = Color.black;

                            claimBtnRt.DOScale(1.08f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true);
                        }
                    }
                    else
                    {
                        var rewardGo = new GameObject("RewardLabel");
                        rewardGo.transform.SetParent(rowGo.transform, false);
                        var rewardRt = rewardGo.AddComponent<RectTransform>();
                        rewardRt.anchorMin = new Vector2(1, 0.5f);
                        rewardRt.anchorMax = new Vector2(1, 0.5f);
                        rewardRt.pivot = new Vector2(1, 0.5f);
                        rewardRt.anchoredPosition = new Vector2(-20, 0);
                        rewardRt.sizeDelta = new Vector2(140, 60);
                        var rewardText = rewardGo.AddComponent<TextMeshProUGUI>();
                        rewardText.text = $"+{item.RewardPoints} pts";
                        rewardText.fontSize = 20;
                        rewardText.alignment = TextAlignmentOptions.Right;
                        rewardText.color = new Color(0.95f, 0.75f, 0.2f, 0.8f);
                    }
                }

                if (rowRt != null)
                {
                    rowRt.localScale = new Vector3(0.9f, 0.9f, 1f);
                    rowRt.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetDelay(i * 0.1f).SetUpdate(true);
                }
            }
        }

        public void RefreshGrid(TileKind[,] cells)
        {
            float cw = 720f / _settings.Columns;
            float cellH = 920f / _settings.Rows;

            for (int r = 0; r < _settings.Rows; r++)
                for (int c = 0; c < _settings.Columns; c++)
                {
                    SetCellColor(r, c, cells[r, c]);
                    _cellImages[r, c].rectTransform.localScale = Vector3.one;
                    _cellImages[r, c].rectTransform.anchoredPosition = new Vector2(c * cw + 4f, r * cellH + 4f);
                }
        }

        public void SetCellColor(int r, int c, TileKind k) => _cellImages[r, c].color = ColorFor(k);

        public void TriggerClearShake(int tileCount)
        {
#if UNITY_ANDROID || UNITY_IOS
            if (_vibrationEnabled) Handheld.Vibrate();
#endif
            float shake = _settings.ShakeBase + Mathf.Max(0, tileCount - 4) * _settings.ShakePerExtraTile;
            _shake.AddShake(shake, 0.12f);
        }

        // ── Reward Toast / Badge ──────────────────────────────────────────────
        RectTransform _toastParent;  // lazy – created on first use

        RectTransform GetToastParent()
        {
            if (_toastParent != null) return _toastParent;
            var go = new GameObject("ToastLayer");
            var parent = (_mainCanvas != null ? _mainCanvas.transform : transform);
            go.transform.SetParent(parent, false);
            _toastParent = go.AddComponent<RectTransform>();
            _toastParent.anchorMin = Vector2.zero;
            _toastParent.anchorMax = Vector2.one;
            _toastParent.offsetMin = Vector2.zero;
            _toastParent.offsetMax = Vector2.zero;
            return _toastParent;
        }

        /// <summary>
        /// Spawns a floating animated reward label in the centre of the screen.
        /// Multiple toasts stack vertically so they don't overlap.
        /// </summary>
        public void ShowRewardToast(string label, string sub, RewardToastStyle style)
        {
            var parent = GetToastParent();

            // Count existing toasts to offset vertically
            int existingCount = 0;
            for (int i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).name == "RewardToast") existingCount++;

            // ── Colours ───────────────────────────────────────────────────────
            Color accent = style switch
            {
                RewardToastStyle.PerfectClear => new Color(1f, 0.84f, 0f),      // gold
                RewardToastStyle.RowClear => new Color(0.4f, 0.9f, 1f),     // cyan
                RewardToastStyle.Combo => new Color(1f, 0.45f, 0.15f),   // orange
                RewardToastStyle.Carry => new Color(0.6f, 0.9f, 0.4f),   // green
                _ => Color.white
            };

            // ── Container ─────────────────────────────────────────────────────
            var go = new GameObject("RewardToast");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.6f);
            rt.anchorMax = new Vector2(0.5f, 0.6f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(400f, 80f);
            rt.anchoredPosition = new Vector2(0f, existingCount * 90f);

            // ── Main label ────────────────────────────────────────────────────
            var textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            var txt = textGo.AddComponent<TextMeshProUGUI>();
            txt.text = label;
            txt.fontSize = style == RewardToastStyle.PerfectClear ? 52 :
                           style == RewardToastStyle.Combo ? 58 : 46;
            txt.fontStyle = FontStyles.Bold;
            txt.color = accent;
            txt.alignment = TextAlignmentOptions.Center;
            txt.enableAutoSizing = false;
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            // ── Sub-label (points) ────────────────────────────────────────────
            if (!string.IsNullOrEmpty(sub))
            {
                var subGo = new GameObject("Sub");
                subGo.transform.SetParent(go.transform, false);
                var subTxt = subGo.AddComponent<TextMeshProUGUI>();
                subTxt.text = sub;
                subTxt.fontSize = 30;
                subTxt.color = new Color(accent.r, accent.g, accent.b, 0.85f);
                subTxt.alignment = TextAlignmentOptions.Center;
                var srt = subGo.GetComponent<RectTransform>();
                srt.anchorMin = new Vector2(0f, -0.6f);
                srt.anchorMax = new Vector2(1f, -0.6f);
                srt.sizeDelta = new Vector2(0f, 40f);
            }

            // ── Animation ────────────────────────────────────────────────────
            var cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            float totalDuration = style == RewardToastStyle.PerfectClear ? 2.2f : 1.6f;

            // Fade in fast, hold, fade out
            var seq = DOTween.Sequence().SetUpdate(true);
            seq.Append(cg.DOFade(1f, 0.18f).SetUpdate(true));
            seq.AppendInterval(totalDuration - 0.55f);
            seq.Append(cg.DOFade(0f, 0.35f).SetUpdate(true));
            seq.OnComplete(() => { if (go != null) Destroy(go); });

            // Float upward
            rt.DOAnchorPosY(rt.anchoredPosition.y + 120f, totalDuration + 0.05f)
              .SetEase(Ease.OutCubic).SetUpdate(true);

            // Scale punch on appear
            go.transform.localScale = Vector3.one * 0.5f;
            go.transform.DOScale(1f, 0.22f).SetEase(Ease.OutBack).SetUpdate(true);

            // Combo gets a continuous pulse
            if (style == RewardToastStyle.Combo)
                txt.transform.DOScale(1.06f, 0.25f).SetLoops(4, LoopType.Yoyo)
                   .SetEase(Ease.InOutSine).SetUpdate(true);

            // Perfect clear gets a shimmer outline
            if (style == RewardToastStyle.PerfectClear)
            {
                txt.fontStyle |= FontStyles.Underline;
                txt.outlineWidth = 0.15f;
                txt.outlineColor = new Color32(255, 255, 200, 200);
            }
        }

        // Persistent badge – e.g. "SLOW RISE" while SlowFillBuff is active
        GameObject _slowRiseBadgeGo;
        Coroutine _slowRiseExpireCoroutine;

        public void ShowSlowRiseBadge(float durationSeconds)
        {
            // Kill any existing badge
            if (_slowRiseBadgeGo != null) { Destroy(_slowRiseBadgeGo); _slowRiseBadgeGo = null; }
            if (_slowRiseExpireCoroutine != null) { StopCoroutine(_slowRiseExpireCoroutine); _slowRiseExpireCoroutine = null; }

            var parent = GetToastParent();

            var go = new GameObject("SlowRiseBadge");
            go.transform.SetParent(parent, false);
            _slowRiseBadgeGo = go;

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.88f);
            rt.anchorMax = new Vector2(0.5f, 0.88f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(320f, 52f);

            // Background pill
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.55f, 0.85f, 0.85f);

            // Label
            var textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            var txt = textGo.AddComponent<TextMeshProUGUI>();
            txt.text = "⬇ SLOW RISE";
            txt.fontSize = 28;
            txt.fontStyle = FontStyles.Bold;
            txt.color = Color.white;
            txt.alignment = TextAlignmentOptions.Center;
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            // Entrance animation
            var cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.DOFade(1f, 0.3f).SetUpdate(true);
            go.transform.localScale = Vector3.one * 0.8f;
            go.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetUpdate(true);

            // Gentle continuous pulse to stay visible
            go.transform.DOScale(1.04f, 0.7f).SetLoops(-1, LoopType.Yoyo)
              .SetEase(Ease.InOutSine).SetUpdate(true);

            // Auto-expire
            _slowRiseExpireCoroutine = StartCoroutine(ExpireSlowRiseBadge(go, cg, durationSeconds));
        }

        IEnumerator ExpireSlowRiseBadge(GameObject badge, CanvasGroup cg, float delay)
        {
            yield return new WaitForSeconds(delay - 0.5f);
            if (badge == null) yield break;
            cg.DOFade(0f, 0.5f).SetUpdate(true).OnComplete(() =>
            {
                if (badge != null) Destroy(badge);
                _slowRiseBadgeGo = null;
            });
        }
        // ─────────────────────────────────────────────────────────────────────

        public IEnumerator BlinkTilesCoroutine(IEnumerable<(int r, int c)> tiles, TileKind[,] boardCells, float blinkTime)
        {
            foreach (var m in tiles)
            {
                var img = _cellImages[m.r, m.c];
                img.DOColor(Color.white, blinkTime * 0.5f).SetLoops(2, LoopType.Yoyo);
                img.rectTransform.DOScale(0f, blinkTime).SetEase(Ease.InExpo);
            }
            yield return new WaitForSeconds(blinkTime);
        }

        public IEnumerator BlinkBombAreaCoroutine(int startCol, int startRow, TileKind[,] boardCells, float blinkTime)
        {
            int W = boardCells.GetLength(1);
            int H = boardCells.GetLength(0);

            for (int dc = -1; dc <= 1; dc++)
                for (int dr = -1; dr <= 1; dr++)
                {
                    int cc = startCol + dc;
                    int rr = startRow + dr;
                    if (cc < 0 || cc >= W || rr < 0 || rr >= H) continue;
                    if (boardCells[rr, cc] == TileKind.Empty) continue;
                    _cellImages[rr, cc].DOColor(Color.white, 0.05f).SetLoops((int)(blinkTime / 0.05f), LoopType.Yoyo);
                }
            yield return new WaitForSeconds(blinkTime);
        }

        public IEnumerator AnimateGravityCoroutine(int[,] fallDistances)
        {
            bool anyFalls = false;
            float maxFallTime = 0.4f;
            float cw = 720f / _settings.Columns;
            float cellH = 920f / _settings.Rows;

            for (int r = 0; r < _settings.Rows; r++)
                for (int c = 0; c < _settings.Columns; c++)
                {
                    if (fallDistances[r, c] > 0)
                    {
                        anyFalls = true;
                        float startY = r * cellH + 4f;
                        float endY = (r - fallDistances[r, c]) * cellH + 4f;
                        _cellImages[r, c].rectTransform.anchoredPosition = new Vector2(c * cw + 4f, startY);
                        _cellImages[r, c].rectTransform.DOAnchorPosY(endY, maxFallTime).SetEase(Ease.OutBounce);
                    }
                }

            if (anyFalls) yield return new WaitForSeconds(maxFallTime);

            for (int r = 0; r < _settings.Rows; r++)
                for (int c = 0; c < _settings.Columns; c++)
                    _cellImages[r, c].rectTransform.anchoredPosition = new Vector2(c * cw + 4f, r * cellH + 4f);
        }

        public IEnumerator SlideGridUpCoroutine(float slideTime)
        {
            float cellH = 920f / _settings.Rows;
            _gridRoot.anchoredPosition = new Vector2(0, -cellH);
            _gridRoot.DOAnchorPosY(0f, slideTime).SetEase(Ease.OutBack);
            yield return new WaitForSeconds(slideTime);
            _gridRoot.anchoredPosition = Vector2.zero;
        }

        Color ColorFor(TileKind k) => k switch
        {
            TileKind.Empty => new Color(0.12f, 0.12f, 0.14f, 1f),
            TileKind.Red => new Color(0.95f, 0.35f, 0.35f),
            TileKind.Blue => new Color(0.35f, 0.55f, 0.95f),
            TileKind.Green => new Color(0.45f, 0.85f, 0.45f),
            TileKind.Yellow => new Color(1.0f, 0.9f, 0.3f),
            TileKind.Purple => new Color(0.75f, 0.45f, 1.0f),
            TileKind.Wild => Color.white,
            TileKind.Bomb => new Color(0.25f, 0.25f, 0.25f),
            _ => Color.gray
        };

        void EnsureInputSystemUi()
        {
            var es = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem");
                go.AddComponent<EventSystem>();
                go.AddComponent<InputSystemUIInputModule>();
                return;
            }

            var standalone = es.GetComponent<StandaloneInputModule>();
            if (standalone != null)
            {
                standalone.enabled = false;
                UnityEngine.Object.Destroy(standalone);
            }

            if (es.GetComponent<InputSystemUIInputModule>() == null)
                es.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        void BuildUi()
        {
            if (_mainCanvas != null)
            {
                _shake = _mainCanvas.gameObject.GetComponent<ScreenShake>();
                if (_shake == null) _shake = _mainCanvas.gameObject.AddComponent<ScreenShake>();
                _shake.Target = _mainCanvas.transform;
            }

            if (_hudRoot != null) _hudRoot.SetActive(false);
            if (_helpPanel != null) _helpPanel.SetActive(false);

            var gridMaskGo = new GameObject("GridMask");
            gridMaskGo.transform.SetParent(_hudRoot.transform, false);
            var maskRt = gridMaskGo.AddComponent<RectTransform>();
            maskRt.anchorMin = new Vector2(0.5f, 0.5f);
            maskRt.anchorMax = new Vector2(0.5f, 0.5f);
            maskRt.sizeDelta = new Vector2(740, 940);
            maskRt.anchoredPosition = new Vector2(0, -120);
            var maskImg = gridMaskGo.AddComponent<Image>();
            maskImg.color = new Color(0.15f, 0.15f, 0.2f, 1f);
            gridMaskGo.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = true;

            var gridGo = new GameObject("Grid");
            gridGo.transform.SetParent(gridMaskGo.transform, false);
            _gridRoot = gridGo.AddComponent<RectTransform>();
            _gridRoot.anchorMin = Vector2.zero;
            _gridRoot.anchorMax = Vector2.one;
            _gridRoot.offsetMin = new Vector2(10, 10);
            _gridRoot.offsetMax = new Vector2(-10, -10);

            _cellImages = new Image[_settings.Rows, _settings.Columns];
            float cw = 720f / _settings.Columns;
            float cellH = 920f / _settings.Rows;
            for (int r = 0; r < _settings.Rows; r++)
                for (int c = 0; c < _settings.Columns; c++)
                {
                    var cell = new GameObject($"c_{r}_{c}");
                    cell.transform.SetParent(_gridRoot, false);
                    var rt = cell.AddComponent<RectTransform>();
                    rt.sizeDelta = new Vector2(cw - 8f, cellH - 8f);
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 0);
                    rt.anchoredPosition = new Vector2(c * cw + 4f, r * cellH + 4f);
                    var img = cell.AddComponent<Image>();
                    img.color = ColorFor(TileKind.Empty);
                    _cellImages[r, c] = img;
                }

            _colOverlayBtns = new Button[_settings.Columns];
            for (int c = 0; c < _settings.Columns; c++)
            {
                int col = c;
                var colOverlay = new GameObject($"ColOverlay_{c}");
                colOverlay.transform.SetParent(_gridRoot, false);
                var rt = colOverlay.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0, 0);
                rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(c * cw + cw * 0.5f, 0);
                rt.sizeDelta = new Vector2(cw, 0);

                var img = colOverlay.AddComponent<Image>();
                img.color = new Color(1f, 1f, 1f, 0f);

                var btn = colOverlay.AddComponent<Button>();
                btn.onClick.AddListener(() => _onColumnClicked?.Invoke(col));
                btn.transition = Selectable.Transition.ColorTint;
                btn.targetGraphic = img;

                var colors = btn.colors;
                colors.normalColor = new Color(1f, 1f, 1f, 0f);
                colors.highlightedColor = new Color(1f, 1f, 1f, 0.05f);
                colors.pressedColor = new Color(1f, 1f, 1f, 0.15f);
                colors.selectedColor = new Color(1f, 1f, 1f, 0f);
                colors.disabledColor = new Color(1f, 1f, 1f, 0f);
                btn.colors = colors;

                _colOverlayBtns[c] = btn;   // store so tutorial can lock it
            }

            /*
            _colButtonGos = new GameObject[_settings.Columns];
            for (int c = 0; c < _settings.Columns; c++)
            {
                int col = c;
                var view = Instantiate(_columnButtonPrefab, _columnButtonsRoot);
                var btnGo = view.gameObject;
                btnGo.name = $"Col{c}";
                _colButtonGos[c] = btnGo;
                view.Button.onClick.AddListener(() => _onColumnClicked?.Invoke(col));
                view.Label.text = (c + 1).ToString();
            } */

            if (_gameOverRoot != null)
            {
                _gameOverRoot.SetActive(false);
                if (_gameOverRoot.GetComponent<CanvasGroup>() == null)
                    _gameOverRoot.AddComponent<CanvasGroup>();
            }

            if (_mainCanvas != null)
                BuildLoadingScreen(_mainCanvas.transform);
            else
                BuildLoadingScreen(transform);

            // Main menu is now fully in the hierarchy – InitMainMenu() is called
            // at the end of Build() once delegates are ready.
        }

        private void CreateGlow(Transform parent, Color color, float size, float duration)
        {
            var go = new GameObject("Glow");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(size, size);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.DOFade(color.a * 0.5f, duration).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true);
            rt.DOScale(1.15f, duration * 1.2f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true);
        }

        private Image CreatePreviewSlot(Transform parent, string label, Vector2 pos, out TextMeshProUGUI innerLabel)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(160, 160);
            rt.pivot = new Vector2(0, 1);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);

            var lblGo = new GameObject("Label");
            lblGo.transform.SetParent(go.transform, false);
            var lrt = lblGo.AddComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0, 1);
            lrt.anchorMax = new Vector2(1, 1);
            lrt.anchoredPosition = new Vector2(0, 30);
            lrt.sizeDelta = new Vector2(0, 40);
            var lt = lblGo.AddComponent<TextMeshProUGUI>();
            lt.text = label;
            lt.fontSize = 24;
            lt.fontStyle = FontStyles.Bold;
            lt.color = new Color(0.7f, 0.7f, 0.7f, 0.6f);
            lt.alignment = TextAlignmentOptions.Left;

            var box = new GameObject("Box");
            box.transform.SetParent(go.transform, false);
            var brt = box.AddComponent<RectTransform>();
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.sizeDelta = Vector2.zero;
            var img = box.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0.1f);

            var txt = new GameObject("Text");
            txt.transform.SetParent(box.transform, false);
            var trt = txt.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.sizeDelta = Vector2.zero;
            innerLabel = txt.AddComponent<TextMeshProUGUI>();
            innerLabel.fontSize = 32;
            innerLabel.fontStyle = FontStyles.Bold;
            innerLabel.alignment = TextAlignmentOptions.Center;
            innerLabel.color = Color.white;
            innerLabel.raycastTarget = false;

            return img;
        }

        GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            go.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f, 0.8f);
            return go;
        }

        TextMeshProUGUI CreateTmp(Transform parent, string name, float size, FontStyles style, TextAlignmentOptions align)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(0, 200);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            return t;
        }

        Button CreateButton(Transform parent, string label, Vector2 pos, Action onClick)
        {
            var go = new GameObject(label + "Btn");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(400, 100);
            rt.anchoredPosition = pos;
            go.AddComponent<Image>().color = new Color(0.25f, 0.45f, 0.85f, 1f);
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(go.transform, false);
            var trt = txtGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            var t = txtGo.AddComponent<TextMeshProUGUI>();
            t.text = label;
            t.fontSize = 38;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.color = Color.white;
            t.raycastTarget = false;

            return btn;
        }

        public void UpdateLoadingStatus(string status)
        {
            if (_loadingStatusText != null)
                _loadingStatusText.text = status;
        }

        public void EnableStartButton()
        {
            if (_loadingRoot == null) return;

            // Auto-dismiss after a brief pause so the player can see the status
            if (_loadingStatusText != null)
            {
                _loadingStatusText.transform.DOKill();
                _loadingStatusText.transform.localScale = Vector3.one;
            }

            // Automatically proceed to the main menu — no tap required
            Time.timeScale = 1f;
            HideLoadingScreen(1.0f);
        }

        private void BuildLoadingScreen(Transform parent)
        {
            Time.timeScale = 0f;

            if (_loadingRoot == null)
            {
                Debug.LogError("Loading Root is not assigned in the Inspector!");
                return;
            }

            _loadingRoot.SetActive(true);
            _loadingRoot.transform.SetParent(parent, false);
            _loadingRoot.transform.SetAsLastSibling();

            var cg = _loadingRoot.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f;

            var startBtn = _loadingRoot.GetComponent<Button>();
            if (startBtn != null)
            {
                startBtn.interactable = false;
                startBtn.onClick.RemoveAllListeners();
                startBtn.onClick.AddListener(() =>
                {
                    startBtn.interactable = false;
                    Time.timeScale = 1f;
                    HideLoadingScreen(1.2f);
                });
            }

            if (_loadingTitleText != null)
            {
                _loadingTitleText.transform.DOKill();
                _loadingTitleText.transform.localScale = Vector3.one;
                _loadingTitleText.transform.DOScale(1.04f, 3f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true);
            }

            if (_loadingGlows != null && _loadingGlows.Length > 0)
            {
                foreach (var glow in _loadingGlows)
                {
                    if (glow == null) continue;
                    glow.transform.DOKill();
                    float baseAlpha = glow.color.a;
                    glow.DOFade(baseAlpha * 0.5f, 3f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true);
                    glow.rectTransform.DOScale(1.15f, 3.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true);
                }
            }
        }

        public Action OnLoadingDone;

        public void HideLoadingScreen(float duration = 1.2f)
        {
            if (_loadingRoot == null) return;
            var cg = _loadingRoot.GetComponent<CanvasGroup>();
            cg.DOFade(0, duration).SetEase(Ease.InOutQuad).SetUpdate(true).OnComplete(() =>
            {
                _loadingRoot.SetActive(false);
                OnLoadingDone?.Invoke();
            });
        }

        // ── Tutorial Controller Extensions ───────────────────────────────────

        public IEnumerator ShowTutorialDialogCoroutine(string title, string body, bool showNextButton)
        {
            _tutorialNextClicked = false;
            ShowTutorialDialog(title, body, showNextButton);
            if (showNextButton)
                yield return new WaitUntil(() => _tutorialNextClicked);
        }

        public void ShowTutorialDialog(string title, string body, bool showNextButton)
        {
            if (_tutorialDialogGo == null)
            {
                _tutorialDialogGo = new GameObject("TutorialDialogPanel");
                _tutorialDialogGo.transform.SetParent(_mainCanvas != null ? _mainCanvas.transform : transform, false);
                var rt = _tutorialDialogGo.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -140f);
                rt.sizeDelta = new Vector2(680f, 320f);

                _tutorialDialogGo.AddComponent<Image>().color = new Color(0.06f, 0.06f, 0.08f, 0.98f);

                var outline = _tutorialDialogGo.AddComponent<Outline>();
                outline.effectColor = new Color(0.2f, 0.6f, 1.0f, 0.5f);
                outline.effectDistance = new Vector2(3f, 3f);

                var titleGo = new GameObject("Title");
                titleGo.transform.SetParent(_tutorialDialogGo.transform, false);
                var titleRt = titleGo.AddComponent<RectTransform>();
                titleRt.anchorMin = new Vector2(0f, 1f);
                titleRt.anchorMax = new Vector2(1f, 1f);
                titleRt.pivot = new Vector2(0.5f, 1f);
                titleRt.anchoredPosition = new Vector2(0f, -15f);
                titleRt.sizeDelta = new Vector2(-40f, 55f);
                _tutorialTitleText = titleGo.AddComponent<TextMeshProUGUI>();
                _tutorialTitleText.fontSize = 36;
                _tutorialTitleText.fontStyle = FontStyles.Bold;
                _tutorialTitleText.color = new Color(0.2f, 0.6f, 1.0f);
                _tutorialTitleText.alignment = TextAlignmentOptions.Left;

                var bodyGo = new GameObject("Body");
                bodyGo.transform.SetParent(_tutorialDialogGo.transform, false);
                var bodyRt = bodyGo.AddComponent<RectTransform>();
                bodyRt.anchorMin = new Vector2(0f, 1f);
                bodyRt.anchorMax = new Vector2(1f, 1f);
                bodyRt.pivot = new Vector2(0.5f, 1f);
                bodyRt.anchoredPosition = new Vector2(0f, -80f);
                bodyRt.sizeDelta = new Vector2(-40f, 150f);
                _tutorialBodyText = bodyGo.AddComponent<TextMeshProUGUI>();
                _tutorialBodyText.fontSize = 25;
                _tutorialBodyText.color = new Color(0.9f, 0.9f, 0.95f);
                _tutorialBodyText.alignment = TextAlignmentOptions.Left;
                _tutorialBodyText.overflowMode = TextOverflowModes.Ellipsis;

                var btnGo = new GameObject("NextBtn");
                btnGo.transform.SetParent(_tutorialDialogGo.transform, false);
                var btnRt = btnGo.AddComponent<RectTransform>();
                btnRt.anchorMin = new Vector2(1f, 0f);
                btnRt.anchorMax = new Vector2(1f, 0f);
                btnRt.pivot = new Vector2(1f, 0f);
                btnRt.anchoredPosition = new Vector2(-20f, 20f);
                btnRt.sizeDelta = new Vector2(180f, 60f);
                btnGo.AddComponent<Image>().color = new Color(0.2f, 0.6f, 1.0f, 1.0f);
                _tutorialNextBtn = btnGo.AddComponent<Button>();
                _tutorialNextBtn.onClick.AddListener(() => _tutorialNextClicked = true);

                var btnTxtGo = new GameObject("Text");
                btnTxtGo.transform.SetParent(btnGo.transform, false);
                var btnTxtRt = btnTxtGo.AddComponent<RectTransform>();
                btnTxtRt.anchorMin = Vector2.zero;
                btnTxtRt.anchorMax = Vector2.one;
                btnTxtRt.sizeDelta = Vector2.zero;
                var btnTxt = btnTxtGo.AddComponent<TextMeshProUGUI>();
                btnTxt.text = "NEXT";
                btnTxt.fontSize = 26;
                btnTxt.fontStyle = FontStyles.Bold;
                btnTxt.color = Color.white;
                btnTxt.alignment = TextAlignmentOptions.Center;
            }

            _tutorialDialogGo.SetActive(true);
            _tutorialDialogGo.transform.SetAsLastSibling();
            _tutorialTitleText.text = title;
            _tutorialBodyText.text = body;
            _tutorialNextBtn.gameObject.SetActive(showNextButton);

            var bRt = _tutorialBodyText.GetComponent<RectTransform>();
            bRt.sizeDelta = showNextButton ? new Vector2(-40f, 150f) : new Vector2(-40f, 220f);

            _tutorialDialogGo.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
            _tutorialDialogGo.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        public void ShowTutorialChoiceDialog(string title, string body, Action onPlay, Action onSkip)
        {
            ShowTutorialDialog(title, body, false);

            var playBtnGo = new GameObject("PlayTutorialChoiceBtn");
            playBtnGo.transform.SetParent(_tutorialDialogGo.transform, false);
            var rtPlay = playBtnGo.AddComponent<RectTransform>();
            rtPlay.anchorMin = new Vector2(0.5f, 0f);
            rtPlay.anchorMax = new Vector2(0.5f, 0f);
            rtPlay.pivot = new Vector2(1f, 0f);
            rtPlay.anchoredPosition = new Vector2(-20f, 20f);
            rtPlay.sizeDelta = new Vector2(240f, 60f);
            playBtnGo.AddComponent<Image>().color = new Color(0.2f, 0.6f, 1.0f, 1.0f);
            var btnPlay = playBtnGo.AddComponent<Button>();
            btnPlay.onClick.AddListener(() =>
            {
                Destroy(playBtnGo);
                var skipBtn = _tutorialDialogGo.transform.Find("SkipTutorialChoiceBtn");
                if (skipBtn != null) Destroy(skipBtn.gameObject);
                onPlay?.Invoke();
            });
            var txtPlayGo = new GameObject("Text");
            txtPlayGo.transform.SetParent(playBtnGo.transform, false);
            var txtPlayRt = txtPlayGo.AddComponent<RectTransform>();
            txtPlayRt.anchorMin = Vector2.zero;
            txtPlayRt.anchorMax = Vector2.one;
            txtPlayRt.sizeDelta = Vector2.zero;
            var txtPlay = txtPlayGo.AddComponent<TextMeshProUGUI>();
            txtPlay.text = "TUTORIAL";
            txtPlay.fontSize = 24;
            txtPlay.fontStyle = FontStyles.Bold;
            txtPlay.color = Color.white;
            txtPlay.alignment = TextAlignmentOptions.Center;

            var skipBtnGo = new GameObject("SkipTutorialChoiceBtn");
            skipBtnGo.transform.SetParent(_tutorialDialogGo.transform, false);
            var rtSkip = skipBtnGo.AddComponent<RectTransform>();
            rtSkip.anchorMin = new Vector2(0.5f, 0f);
            rtSkip.anchorMax = new Vector2(0.5f, 0f);
            rtSkip.pivot = new Vector2(0f, 0f);
            rtSkip.anchoredPosition = new Vector2(20f, 20f);
            rtSkip.sizeDelta = new Vector2(240f, 60f);
            skipBtnGo.AddComponent<Image>().color = new Color(0.25f, 0.25f, 0.28f, 1.0f);
            var btnSkip = skipBtnGo.AddComponent<Button>();
            btnSkip.onClick.AddListener(() =>
            {
                Destroy(skipBtnGo);
                var playBtn = _tutorialDialogGo.transform.Find("PlayTutorialChoiceBtn");
                if (playBtn != null) Destroy(playBtn.gameObject);
                HideTutorialDialog();
                onSkip?.Invoke();
            });
            var txtSkipGo = new GameObject("Text");
            txtSkipGo.transform.SetParent(skipBtnGo.transform, false);
            var txtSkipRt = txtSkipGo.AddComponent<RectTransform>();
            txtSkipRt.anchorMin = Vector2.zero;
            txtSkipRt.anchorMax = Vector2.one;
            txtSkipRt.sizeDelta = Vector2.zero;
            var txtSkip = txtSkipGo.AddComponent<TextMeshProUGUI>();
            txtSkip.text = "SKIP";
            txtSkip.fontSize = 24;
            txtSkip.fontStyle = FontStyles.Bold;
            txtSkip.color = Color.white;
            txtSkip.alignment = TextAlignmentOptions.Center;
        }

        public void HideTutorialDialog()
        {
            if (_tutorialDialogGo != null) _tutorialDialogGo.SetActive(false);
        }

        public void HighlightColumn(int colIndex)
        {
            if (_colOverlayBtns == null) return;

            float cellH = 920f / _settings.Rows;
            float halfHeight = (_settings.Rows * cellH) * 0.5f;

            for (int c = 0; c < _colOverlayBtns.Length; c++)
            {
                var btn = _colOverlayBtns[c];
                if (btn == null) continue;
                var btnGo = btn.gameObject;

                if (c == colIndex)
                {
                    var img = btnGo.GetComponent<Image>();
                    if (img != null) img.color = new Color(1f, 1f, 1f, 0.05f);
                    if (btn != null) btn.interactable = true;

                    var overlayGo = btnGo.transform.Find("TutorialHighlightOverlay")?.gameObject;
                    if (overlayGo == null)
                    {
                        overlayGo = new GameObject("TutorialHighlightOverlay");
                        overlayGo.transform.SetParent(btnGo.transform, false);
                        var ort = overlayGo.AddComponent<RectTransform>();
                        ort.anchorMin = Vector2.zero;
                        ort.anchorMax = Vector2.one;
                        ort.sizeDelta = Vector2.zero;
                        var oimg = overlayGo.AddComponent<Image>();
                        oimg.color = new Color(0.95f, 0.75f, 0.2f, 0.2f);
                        overlayGo.transform.DOScale(1.08f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true);
                    }

                    var arrowGo = btnGo.transform.Find("TutorialArrow")?.gameObject;
                    if (arrowGo == null)
                    {
                        arrowGo = new GameObject("TutorialArrow");
                        arrowGo.transform.SetParent(btnGo.transform, false);
                        var art = arrowGo.AddComponent<RectTransform>();
                        art.anchoredPosition = new Vector2(0f, halfHeight - 60f);
                        art.sizeDelta = new Vector2(60f, 60f);
                        var txt = arrowGo.AddComponent<TextMeshProUGUI>();
                        txt.text = "▼";
                        txt.fontSize = 44;
                        txt.color = new Color(0.95f, 0.75f, 0.2f, 1f);
                        txt.fontStyle = FontStyles.Bold;
                        txt.alignment = TextAlignmentOptions.Center;
                        art.DOAnchorPosY(halfHeight - 40f, 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true);
                    }

                    for (int r = 0; r < _settings.Rows; r++)
                    {
                        var cellImg = _cellImages[r, c];
                        if (cellImg != null)
                        {
                            var col = cellImg.color;
                            col.a = 1.0f;
                            cellImg.color = col;
                        }
                    }
                }
                else
                {
                    var img = btnGo.GetComponent<Image>();
                    if (img != null) img.color = new Color(1f, 1f, 1f, 0f);
                    if (btn != null) btn.interactable = false;

                    for (int r = 0; r < _settings.Rows; r++)
                    {
                        var cellImg = _cellImages[r, c];
                        if (cellImg != null)
                        {
                            var col = cellImg.color;
                            col.a = 0.15f;
                            cellImg.color = col;
                        }
                    }
                }
            }

            // Ensure the target column's overlay is enabled
            if (_colOverlayBtns != null && colIndex >= 0 && colIndex < _colOverlayBtns.Length && _colOverlayBtns[colIndex] != null)
                _colOverlayBtns[colIndex].interactable = true;
        }

        public void ClearColumnHighlight()
        {
            if (_colOverlayBtns == null) return;

            for (int c = 0; c < _colOverlayBtns.Length; c++)
            {
                var btn = _colOverlayBtns[c];
                if (btn == null) continue;
                var btnGo = btn.gameObject;

                if (btn != null) btn.interactable = true;

                var img = btnGo.GetComponent<Image>();
                if (img != null) img.color = new Color(1f, 1f, 1f, 0f);

                var overlay = btnGo.transform.Find("TutorialHighlightOverlay");
                if (overlay != null) Destroy(overlay.gameObject);

                var arrow = btnGo.transform.Find("TutorialArrow");
                if (arrow != null) Destroy(arrow.gameObject);

                for (int r = 0; r < _settings.Rows; r++)
                {
                    var cellImg = _cellImages[r, c];
                    if (cellImg != null)
                    {
                        var col = cellImg.color;
                        col.a = 1.0f;
                        cellImg.color = col;
                    }
                }
            }
        }
    }

    /// <summary>Style hint for ShowRewardToast so it can pick the right colour and emphasis.</summary>
    public enum RewardToastStyle { Default, Combo, RowClear, PerfectClear, Carry }
}