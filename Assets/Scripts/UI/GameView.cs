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
        
        // Visual Previews
        [SerializeField] Image _nextPreviewImg;
        [SerializeField] Image _queuedPreviewImg;
        [SerializeField] TextMeshProUGUI _nextInnerLabel;
        [SerializeField] TextMeshProUGUI _queuedInnerLabel;

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
        ScreenShake _shake;

        Action<int> _onColumnClicked;
        Action _onRetry;
        Action _onShare;
        Func<ChallengeDisplayData[]> _getChallengesData;
        Func<TimeSpan> _getTimeUntilReset;
        Action<int> _onClaimReward;

        [SerializeField] Transform _challengeRowContainer;
        TextMeshProUGUI _titleText;
        Coroutine _resetTimerCoroutine;

        private int _lastScore = 0;

        // ── Leaderboard ───────────────────────────────────────────────────────
        Func<Task<LeaderboardEntryData[]>> _getLeaderboardScores;
        Func<string, Task>               _setPlayerName;
        Func<string>                     _getPlayerName;
        GameObject                       _leaderboardRoot;
        Transform                        _leaderboardRowContainer;
        TextMeshProUGUI                  _leaderboardStatusText;
        TMP_InputField                   _playerNameInput;

        void Awake()
        {
            if(_closeHelpButton != null) _closeHelpButton.onClick.AddListener(ToggleHelp);
        }

        public void Build(StackSurgeSettings settings, Action<int> onColumnClicked, Action onRetry, Action onShare, Func<ChallengeDisplayData[]> getChallengesData, Func<TimeSpan> getTimeUntilReset, Action<int> onClaimReward)
        {
            _settings = settings;
            _onColumnClicked = onColumnClicked;
            _onRetry = onRetry;
            _onShare = onShare;
            _getChallengesData = getChallengesData;
            _getTimeUntilReset = getTimeUntilReset;
            _onClaimReward = onClaimReward;

            EnsureInputSystemUi();
            BuildUi();
            
            DOTween.SetTweensCapacity(500, 50);
            _retryButton.onClick.AddListener(Retry);
            _shareButton.onClick.AddListener(Share);

            if (_challengesButton != null) _challengesButton.onClick.AddListener(ToggleChallenges);
            if (_helpButton != null) _helpButton.onClick.AddListener(ToggleHelp);
            if (_leaderboardButton != null) _leaderboardButton.onClick.AddListener(ToggleLeaderboard);

            // Hide old challenges body text and prepare the row container
            if (_challengesBody != null)
            {
                _challengesBody.gameObject.SetActive(false);
            }

            if (_challengesRoot != null)
            {
                _titleText = _challengesRoot.transform.Find("Title")?.GetComponent<TextMeshProUGUI>();
            }
        }

        public void UpdateHud(int score, string timeStr, string riseLine, float wildChance, TileKind current, TileKind next)
        {
            if (_scoreText == null) return;
            
            if (score != _lastScore)
            {
                _scoreText.text = score.ToString();
                _scoreText.transform.DOPunchScale(Vector3.one * 0.15f, 0.3f, 10, 1f);
                _lastScore = score;
            }

            _hudText.text =
                $"Time: <color=#A0A0A0>{timeStr}</color>\n" +
                $"<color=#60A5FA>{riseLine}</color>";
            
            UpdatePreviewSquare(_nextPreviewImg, _nextInnerLabel, current);
            UpdatePreviewSquare(_queuedPreviewImg, _queuedInnerLabel, next);
        }

        private void UpdatePreviewSquare(Image img, TextMeshProUGUI label, TileKind k)
        {
            if (img == null || label == null) return;

            img.color = ColorFor(k);

            if (k == TileKind.Wild)
            {
                label.text = "WILD";
                label.color = Color.black;
                img.transform.DOKill();
                img.transform.DOScale(1.1f, 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
            }
            else if (k == TileKind.Bomb)
            {
                label.text = "BOMB";
                label.color = Color.white;
                img.transform.DOKill();
                img.transform.localScale = Vector3.one;
            }
            else
            {
                label.text = "";
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
                                 $"Streak: {streak} days";
            
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
        public void Share() => _onShare?.Invoke();

        public void ToggleHelp()
        {
            if (_helpPanel != null) _helpPanel.SetActive(!_helpPanel.activeSelf);
        }

        public void ToggleChallenges()
        {
            if (_challengesRoot == null) return;
            bool active = !_challengesRoot.activeSelf;
            _challengesRoot.SetActive(active);
            
            // Pause/resume the game
            Time.timeScale = active ? 0f : 1f;
            
            if (active)
            {
                // Animate panel scale and fade-in
                var cg = _challengesRoot.GetComponent<CanvasGroup>();
                if (cg == null) cg = _challengesRoot.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                cg.DOFade(1f, 0.25f).SetUpdate(true);
                _challengesRoot.transform.localScale = Vector3.one * 0.9f;
                _challengesRoot.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetUpdate(true);

                // Build/Populate rows
                PopulateChallenges();

                // Start reset timer coroutine
                if (_resetTimerCoroutine != null) StopCoroutine(_resetTimerCoroutine);
                _resetTimerCoroutine = StartCoroutine(UpdateResetTimerCoroutine());

                // Hook up the close button inside the challenges panel
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

            // Clear previous rows
            foreach (Transform child in _challengeRowContainer)
            {
                Destroy(child.gameObject);
            }

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
                    {
                        rowRt.anchoredPosition = new Vector2(0, startY - i * rowHeight);
                    }

                    var rowView = rowGo.GetComponent<ChallengeRowView>();
                    if (rowView != null)
                    {
                        if (rowView.TitleText != null) rowView.TitleText.text = item.Title;
                        if (rowView.DescriptionText != null) rowView.DescriptionText.text = item.Description;
                        if (rowView.StatusText != null)
                        {
                            rowView.StatusText.text = item.Completed ? "<color=#4ADE80>✓</color>" : "<color=#4B5563>○</color>";
                        }
                        if (rowView.ProgressText != null)
                        {
                            rowView.ProgressText.text = $"{item.Progress} / {item.TargetValue}";
                        }

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
                                    rowView.ClaimButton.onClick.AddListener(() => {
                                        if (_onClaimReward != null)
                                        {
                                            _onClaimReward(index);
                                            PopulateChallenges(); // Refresh panel
                                        }
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
                                ? new Color(1f, 0.85f, 0.4f, 0.15f) // Completed gold tint
                                : new Color(0.1f, 0.1f, 0.12f, 0.6f); // Standard background
                        }
                    }
                }
                else
                {
                    // Fallback to programmatic creation
                    rowGo = new GameObject($"ChallengeRow_{i}");
                    rowGo.transform.SetParent(_challengeRowContainer, false);
                    rowRt = rowGo.AddComponent<RectTransform>();
                    rowRt.anchorMin = new Vector2(0, 0.5f);
                    rowRt.anchorMax = new Vector2(1, 0.5f);
                    rowRt.pivot = new Vector2(0.5f, 0.5f);
                    rowRt.anchoredPosition = new Vector2(0, startY - i * rowHeight);
                    rowRt.sizeDelta = new Vector2(0, 100f);

                    // Add row background Image
                    var bgImg = rowGo.AddComponent<Image>();
                    bgImg.color = item.Completed 
                        ? new Color(1f, 0.85f, 0.4f, 0.15f) // Completed gold tint
                        : new Color(0.1f, 0.1f, 0.12f, 0.6f); // Standard background

                    // Status text (icon)
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

                    // Title and Description
                    var textColGo = new GameObject("TextColumn");
                    textColGo.transform.SetParent(rowGo.transform, false);
                    var textColRt = textColGo.AddComponent<RectTransform>();
                    textColRt.anchorMin = new Vector2(0, 0.5f);
                    textColRt.anchorMax = new Vector2(1, 0.5f);
                    textColRt.pivot = new Vector2(0, 0.5f);
                    // Adjust right side based on whether there's a claim button
                    float rightOffset = (item.Completed && !item.Claimed) ? -180f : -150f;
                    textColRt.anchoredPosition = new Vector2(80, 10);
                    textColRt.sizeDelta = new Vector2(rightOffset - 80f, 80);

                    var titleGo = new GameObject("Title");
                    titleGo.transform.SetParent(textColGo.transform, false);
                    var titleRt = titleGo.AddComponent<RectTransform>();
                    titleRt.anchorMin = new Vector2(0, 1);
                    titleRt.anchorMax = new Vector2(1, 1);
                    titleRt.pivot = new Vector2(0, 1);
                    titleRt.anchoredPosition = new Vector2(0, 0);
                    titleRt.sizeDelta = new Vector2(0, 35);
                    var titleText = titleGo.AddComponent<TextMeshProUGUI>();
                    titleText.text = item.Title;
                    titleText.fontSize = 24;
                    titleText.fontStyle = FontStyles.Bold;
                    titleText.color = Color.white;

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

                    // Progress Text (e.g. 500 / 1000)
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

                    // Progress Bar Background
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

                    // Progress Bar Fill
                    var barFillGo = new GameObject("ProgressBarFill");
                    barFillGo.transform.SetParent(barBgGo.transform, false);
                    var barFillRt = barFillGo.AddComponent<RectTransform>();
                    barFillRt.anchorMin = new Vector2(0, 0);
                    barFillRt.anchorMax = new Vector2(0, 1);
                    barFillRt.pivot = new Vector2(0, 0.5f);
                    barFillRt.anchoredPosition = Vector2.zero;
                    barFillRt.sizeDelta = new Vector2(0, 0); // width driven by code
                    var barFillImg = barFillGo.AddComponent<Image>();
                    
                    // Color transition from blue to green
                    float ratio = item.TargetValue > 0 ? (float)item.Progress / item.TargetValue : 0f;
                    barFillImg.color = Color.Lerp(new Color(0.2f, 0.6f, 1.0f), new Color(0.3f, 0.8f, 0.4f), ratio);

                    // Animate progress bar fill with DOFillAmount / width scaling
                    float targetWidth = ratio * barBgRt.sizeDelta.x;
                    barFillRt.sizeDelta = new Vector2(0, 0);
                    barFillRt.DOSizeDelta(new Vector2(targetWidth, 0), 0.75f).SetEase(Ease.OutQuad).SetUpdate(true);

                    // Claim Button / Label
                    if (item.Completed)
                    {
                        if (item.Claimed)
                        {
                            // Show Claimed Label
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
                            // Show Pulsing Claim Button
                            var claimBtnGo = new GameObject("ClaimButton");
                            claimBtnGo.transform.SetParent(rowGo.transform, false);
                            var claimBtnRt = claimBtnGo.AddComponent<RectTransform>();
                            claimBtnRt.anchorMin = new Vector2(1, 0.5f);
                            claimBtnRt.anchorMax = new Vector2(1, 0.5f);
                            claimBtnRt.pivot = new Vector2(1, 0.5f);
                            claimBtnRt.anchoredPosition = new Vector2(-20, 0);
                            claimBtnRt.sizeDelta = new Vector2(140, 60);
                            var claimBtnImg = claimBtnGo.AddComponent<Image>();
                            claimBtnImg.color = new Color(0.95f, 0.75f, 0.2f); // gold-yellow
                            var btn = claimBtnGo.AddComponent<Button>();
                            btn.onClick.AddListener(() => {
                                if (_onClaimReward != null)
                                {
                                    _onClaimReward(index);
                                    PopulateChallenges(); // Refresh panel
                                }
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

                            // Pulse animation using DOTween
                            claimBtnRt.DOScale(1.08f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true);
                        }
                    }
                    else
                    {
                        // If not completed, show reward points label
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

                // Fade in row
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

        public void SetCellColor(int r, int c, TileKind k)
        {
            _cellImages[r, c].color = ColorFor(k);
        }

        public void TriggerClearShake(int tileCount)
        {
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
            float shake = _settings.ShakeBase + Mathf.Max(0, tileCount - 4) * _settings.ShakePerExtraTile;
            _shake.AddShake(shake, 0.12f);
        }

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

            // Important: Reset positions at the end of animation to maintain grid slot mapping
            for (int r = 0; r < _settings.Rows; r++)
            for (int c = 0; c < _settings.Columns; c++)
            {
                _cellImages[r, c].rectTransform.anchoredPosition = new Vector2(c * cw + 4f, r * cellH + 4f);
            }
        }

        public IEnumerator SlideGridUpCoroutine(float slideTime)
        {
            float cellH = 920f / _settings.Rows; 
            _gridRoot.anchoredPosition = new Vector2(0, -cellH);
            _gridRoot.DOAnchorPosY(0f, slideTime).SetEase(Ease.OutBack);
            yield return new WaitForSeconds(slideTime);
            _gridRoot.anchoredPosition = Vector2.zero;
        }

        string PlacedName(TileKind k) =>
            k switch
            {
                TileKind.Wild => "Wild",
                TileKind.Bomb => "Bomb",
                _ => k.ToString()
            };

        Color ColorFor(TileKind k)
        {
            return k switch
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
        }

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

            if (_hudRoot != null)
            {
                _hudRoot.SetActive(true);
            }


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
                rt.anchorMin = new Vector2(0, 0);
                rt.anchorMax = new Vector2(0, 0);
                rt.pivot = new Vector2(0, 0);
                rt.anchoredPosition = new Vector2(c * cw + 4f, r * cellH + 4f);
                var img = cell.AddComponent<Image>();
                img.color = ColorFor(TileKind.Empty);
                _cellImages[r, c] = img;
            }

            float colY = 270f; 
            for (int c = 0; c < _settings.Columns; c++)
            {
                int col = c;
                var btnGo = new GameObject($"Col{c}");
                btnGo.transform.SetParent(_hudRoot.transform, false);
                var rt = btnGo.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0);
                rt.anchorMax = new Vector2(0.5f, 0);
                rt.pivot = new Vector2(0.5f, 0);
                rt.sizeDelta = new Vector2(cw - 2f, 80f);
                rt.anchoredPosition = new Vector2((c - (_settings.Columns - 1) / 2f) * cw, colY);
                var img = btnGo.AddComponent<Image>();
                img.color = new Color(1f, 1f, 1f, 0.05f);
                var btn = btnGo.AddComponent<Button>();
                var colors = btn.colors;
                colors.highlightedColor = new Color(1f, 1f, 1f, 0.15f);
                colors.pressedColor = new Color(1f, 1f, 1f, 0.3f);
                btn.colors = colors;
                btn.onClick.AddListener(() => {
                    _onColumnClicked?.Invoke(col);
                });

                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(btnGo.transform, false);
                var lrt = labelGo.AddComponent<RectTransform>();
                lrt.anchorMin = new Vector2(0.5f, 0f);
                lrt.anchorMax = new Vector2(0.5f, 0f);
                lrt.pivot = new Vector2(0.5f, 0f);
                lrt.sizeDelta = new Vector2(cw, 80f);
                lrt.anchoredPosition = new Vector2(0, 0f);
                var lt = labelGo.AddComponent<TextMeshProUGUI>();
                lt.text = (c + 1).ToString();
                lt.fontSize = 54;
                lt.alignment = TextAlignmentOptions.Center;
                lt.color = new Color(0.8f, 0.8f, 0.8f, 0.4f);
                lt.raycastTarget = false;
            }

            if (_gameOverRoot != null)
            {
                _gameOverRoot.SetActive(false);
                if (_gameOverRoot.GetComponent<CanvasGroup>() == null)
                    _gameOverRoot.AddComponent<CanvasGroup>();
            }

            if (_mainCanvas != null)
            {
                BuildLoadingScreen(_mainCanvas.transform);
            }
            else
            {
                BuildLoadingScreen(transform);
            }

            BuildLeaderboardPanel();
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
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
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
            {
                _loadingStatusText.text = status;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Leaderboard
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Called from StackSurgeGame after auth completes so the delegates are ready.</summary>
        public void SetLeaderboardCallbacks(
            Func<Task<LeaderboardEntryData[]>> getLeaderboardScores,
            Func<string, Task>                 setPlayerName,
            Func<string>                       getPlayerName)
        {
            _getLeaderboardScores = getLeaderboardScores;
            _setPlayerName        = setPlayerName;
            _getPlayerName        = getPlayerName;

            if (_playerNameInput != null)
                _playerNameInput.text = getPlayerName?.Invoke() ?? "";
        }

        public void ToggleLeaderboard()
        {
            if (_leaderboardRoot == null) return;
            bool active = !_leaderboardRoot.activeSelf;
            _leaderboardRoot.SetActive(active);
            Time.timeScale = active ? 0f : 1f;

            if (active)
            {
                var cg = _leaderboardRoot.GetComponent<CanvasGroup>();
                cg.alpha = 0f;
                cg.DOFade(1f, 0.25f).SetUpdate(true);
                _leaderboardRoot.transform.localScale = Vector3.one * 0.9f;
                _leaderboardRoot.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetUpdate(true);

                if (_playerNameInput != null && _getPlayerName != null)
                    _playerNameInput.text = _getPlayerName();

                PopulateLeaderboard();
            }
        }

        async void PopulateLeaderboard()
        {
            if (_leaderboardRowContainer == null) return;

            foreach (Transform child in _leaderboardRowContainer)
                Destroy(child.gameObject);

            if (_leaderboardStatusText != null)
            {
                _leaderboardStatusText.gameObject.SetActive(true);
                _leaderboardStatusText.text = "Loading scores...";
            }

            LeaderboardEntryData[] entries = Array.Empty<LeaderboardEntryData>();
            if (_getLeaderboardScores != null)
            {
                try   { entries = await _getLeaderboardScores(); }
                catch { entries = Array.Empty<LeaderboardEntryData>(); }
            }

            if (_leaderboardStatusText != null)
                _leaderboardStatusText.gameObject.SetActive(false);

            if (entries.Length == 0)
            {
                if (_leaderboardStatusText != null)
                {
                    _leaderboardStatusText.gameObject.SetActive(true);
                    _leaderboardStatusText.text =
                        "Could not load scores.\n<size=80%><color=#606060>Play online to appear here!</color></size>";
                }
                return;
            }

            for (int i = 0; i < entries.Length; i++)
                BuildLeaderboardRow(entries[i], i);
        }

        void BuildLeaderboardRow(LeaderboardEntryData entry, int delayIndex)
        {
            var rowGo = new GameObject($"LBRow_{entry.Rank}");
            rowGo.transform.SetParent(_leaderboardRowContainer, false);
            var rowRt = rowGo.AddComponent<RectTransform>();
            rowRt.sizeDelta = new Vector2(0f, 66f);

            bool isPlayer = entry.IsCurrentPlayer;
            rowGo.AddComponent<Image>().color = isPlayer
                ? new Color(1f, 0.85f, 0.3f, 0.18f)
                : new Color(0.1f, 0.1f, 0.13f, 0.7f);

            // Rank badge
            var rankGo = new GameObject("Rank");
            rankGo.transform.SetParent(rowGo.transform, false);
            var rankRt = rankGo.AddComponent<RectTransform>();
            rankRt.anchorMin = new Vector2(0f, 0.5f);
            rankRt.anchorMax = new Vector2(0f, 0.5f);
            rankRt.pivot     = new Vector2(0f, 0.5f);
            rankRt.anchoredPosition = new Vector2(10f, 0f);
            rankRt.sizeDelta = new Vector2(64f, 60f);
            var rankTmp = rankGo.AddComponent<TextMeshProUGUI>();
            rankTmp.alignment = TextAlignmentOptions.Center;
            rankTmp.raycastTarget = false;
            switch (entry.Rank)
            {
                case 1:  rankTmp.text = "\U0001F947"; rankTmp.fontSize = 34; break; // 🥇
                case 2:  rankTmp.text = "\U0001F948"; rankTmp.fontSize = 34; break; // 🥈
                case 3:  rankTmp.text = "\U0001F949"; rankTmp.fontSize = 34; break; // 🥉
                default:
                    rankTmp.text = $"#{entry.Rank}";
                    rankTmp.fontSize = 22;
                    rankTmp.color = new Color(0.55f, 0.55f, 0.55f);
                    break;
            }

            // Player name
            var nameGo = new GameObject("Name");
            nameGo.transform.SetParent(rowGo.transform, false);
            var nameRt = nameGo.AddComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0f, 0.5f);
            nameRt.anchorMax = new Vector2(1f, 0.5f);
            nameRt.pivot     = new Vector2(0f, 0.5f);
            nameRt.anchoredPosition = new Vector2(80f, 0f);
            nameRt.sizeDelta = new Vector2(-280f, 52f);
            var nameTmp = nameGo.AddComponent<TextMeshProUGUI>();
            nameTmp.text = entry.PlayerName +
                           (isPlayer ? " <color=#FFD700><size=70%>(You)</size></color>" : "");
            nameTmp.fontSize    = 24;
            nameTmp.color       = isPlayer ? new Color(1f, 0.9f, 0.5f) : Color.white;
            nameTmp.fontStyle   = isPlayer ? FontStyles.Bold : FontStyles.Normal;
            nameTmp.alignment   = TextAlignmentOptions.Left;
            nameTmp.overflowMode = TextOverflowModes.Ellipsis;
            nameTmp.raycastTarget = false;

            // Score
            var scoreGo = new GameObject("Score");
            scoreGo.transform.SetParent(rowGo.transform, false);
            var scoreRt = scoreGo.AddComponent<RectTransform>();
            scoreRt.anchorMin = new Vector2(1f, 0.5f);
            scoreRt.anchorMax = new Vector2(1f, 0.5f);
            scoreRt.pivot     = new Vector2(1f, 0.5f);
            scoreRt.anchoredPosition = new Vector2(-14f, 0f);
            scoreRt.sizeDelta = new Vector2(180f, 52f);
            var scoreTmp = scoreGo.AddComponent<TextMeshProUGUI>();
            scoreTmp.text      = ((int)entry.Score).ToString("N0");
            scoreTmp.fontSize  = 26;
            scoreTmp.fontStyle = FontStyles.Bold;
            scoreTmp.color     = isPlayer ? new Color(1f, 0.9f, 0.4f) : new Color(0.85f, 0.85f, 0.85f);
            scoreTmp.alignment = TextAlignmentOptions.Right;
            scoreTmp.raycastTarget = false;

            // Staggered pop-in
            rowRt.localScale = new Vector3(0.88f, 0.88f, 1f);
            rowRt.DOScale(1f, 0.28f).SetEase(Ease.OutBack).SetDelay(delayIndex * 0.055f).SetUpdate(true);
        }

        async void OnSetPlayerName()
        {
            if (_playerNameInput == null || _setPlayerName == null) return;
            string name = _playerNameInput.text.Trim();
            if (name.Length < 1 || name.Length > 20) return;

            if (_leaderboardStatusText != null)
            {
                _leaderboardStatusText.gameObject.SetActive(true);
                _leaderboardStatusText.text = "Updating name...";
            }

            await _setPlayerName(name);

            // Brief wait for UGS to propagate the name before refreshing
            await Task.Delay(600);
            PopulateLeaderboard();
        }

        void BuildLeaderboardPanel()
        {
            Transform panelParent = _mainCanvas != null ? _mainCanvas.transform : transform;

            // ── Outer panel ──────────────────────────────────────────────────
            var panelGo = new GameObject("LeaderboardPanel");
            panelGo.transform.SetParent(panelParent, false);
            var panelRt = panelGo.AddComponent<RectTransform>();
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot     = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(720f, 960f);
            panelRt.anchoredPosition = Vector2.zero;
            panelGo.AddComponent<Image>().color = new Color(0.07f, 0.07f, 0.10f, 0.97f);
            panelGo.AddComponent<CanvasGroup>();
            _leaderboardRoot = panelGo;

            // ── Header ───────────────────────────────────────────────────────
            var headerGo = new GameObject("Header");
            headerGo.transform.SetParent(panelGo.transform, false);
            var headerRt = headerGo.AddComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot     = new Vector2(0.5f, 1f);
            headerRt.anchoredPosition = Vector2.zero;
            headerRt.sizeDelta = new Vector2(0f, 115f);

            // Title
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(headerGo.transform, false);
            var titleRt  = titleGo.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.04f, 0.52f);
            titleRt.anchorMax = new Vector2(0.85f, 1f);
            titleRt.offsetMin = titleRt.offsetMax = Vector2.zero;
            var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
            titleTmp.text      = "\U0001F3C6 LEADERBOARD"; // 🏆
            titleTmp.fontSize  = 36;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color     = new Color(1f, 0.85f, 0.3f);
            titleTmp.alignment = TextAlignmentOptions.Left;
            titleTmp.raycastTarget = false;

            // Subtitle
            var subGo = new GameObject("Subtitle");
            subGo.transform.SetParent(headerGo.transform, false);
            var subRt  = subGo.AddComponent<RectTransform>();
            subRt.anchorMin = new Vector2(0.04f, 0f);
            subRt.anchorMax = new Vector2(0.85f, 0.48f);
            subRt.offsetMin = subRt.offsetMax = Vector2.zero;
            var subTmp = subGo.AddComponent<TextMeshProUGUI>();
            subTmp.text      = "Overall Score  ·  Resets every Sunday";
            subTmp.fontSize  = 20;
            subTmp.color     = new Color(0.55f, 0.55f, 0.55f, 0.9f);
            subTmp.alignment = TextAlignmentOptions.Left;
            subTmp.raycastTarget = false;

            // Close button
            LBMakeCloseBtn(headerGo.transform);

            // Header bottom separator
            LBMakeSeparator(panelGo.transform, -115f);

            // ── Name input row ───────────────────────────────────────────────
            var nameRowGo = new GameObject("NameRow");
            nameRowGo.transform.SetParent(panelGo.transform, false);
            var nameRowRt = nameRowGo.AddComponent<RectTransform>();
            nameRowRt.anchorMin = new Vector2(0f, 1f);
            nameRowRt.anchorMax = new Vector2(1f, 1f);
            nameRowRt.pivot     = new Vector2(0.5f, 1f);
            nameRowRt.anchoredPosition = new Vector2(0f, -120f);
            nameRowRt.sizeDelta = new Vector2(-30f, 78f);
            nameRowGo.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.17f, 0.85f);

            // Label
            var lblGo = new GameObject("Label");
            lblGo.transform.SetParent(nameRowGo.transform, false);
            var lblRt  = lblGo.AddComponent<RectTransform>();
            lblRt.anchorMin = new Vector2(0f, 0.5f);
            lblRt.anchorMax = new Vector2(0f, 0.5f);
            lblRt.pivot     = new Vector2(0f, 0.5f);
            lblRt.anchoredPosition = new Vector2(16f, 0f);
            lblRt.sizeDelta = new Vector2(155f, 55f);
            var lblTmp = lblGo.AddComponent<TextMeshProUGUI>();
            lblTmp.text      = "Your Name:";
            lblTmp.fontSize  = 21;
            lblTmp.color     = new Color(0.65f, 0.65f, 0.65f);
            lblTmp.alignment = TextAlignmentOptions.Left;
            lblTmp.raycastTarget = false;

            // Input field
            var inputGo = new GameObject("NameInput");
            inputGo.transform.SetParent(nameRowGo.transform, false);
            var inputRt = inputGo.AddComponent<RectTransform>();
            inputRt.anchorMin = new Vector2(0f, 0.5f);
            inputRt.anchorMax = new Vector2(1f, 0.5f);
            inputRt.pivot     = new Vector2(0.5f, 0.5f);
            inputRt.anchoredPosition = new Vector2(-55f, 0f);
            inputRt.sizeDelta = new Vector2(-300f, 52f);
            inputGo.AddComponent<Image>().color = new Color(0.18f, 0.18f, 0.24f, 1f);

            var textAreaGo = new GameObject("Text Area");
            textAreaGo.transform.SetParent(inputGo.transform, false);
            var taRt = textAreaGo.AddComponent<RectTransform>();
            taRt.anchorMin = Vector2.zero; taRt.anchorMax = Vector2.one;
            taRt.offsetMin = new Vector2(8f, 2f); taRt.offsetMax = new Vector2(-8f, -2f);
            textAreaGo.AddComponent<RectMask2D>();

            var inputTextGo = new GameObject("Text");
            inputTextGo.transform.SetParent(textAreaGo.transform, false);
            var itRt = inputTextGo.AddComponent<RectTransform>();
            itRt.anchorMin = Vector2.zero; itRt.anchorMax = Vector2.one;
            itRt.offsetMin = itRt.offsetMax = Vector2.zero;
            var inputTmp = inputTextGo.AddComponent<TextMeshProUGUI>();
            inputTmp.fontSize  = 23;
            inputTmp.color     = Color.white;
            inputTmp.alignment = TextAlignmentOptions.Left;

            var phGo = new GameObject("Placeholder");
            phGo.transform.SetParent(textAreaGo.transform, false);
            var phRt = phGo.AddComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero; phRt.anchorMax = Vector2.one;
            phRt.offsetMin = phRt.offsetMax = Vector2.zero;
            var phTmp = phGo.AddComponent<TextMeshProUGUI>();
            phTmp.text      = "Enter display name...";
            phTmp.fontSize  = 23;
            phTmp.color     = new Color(0.45f, 0.45f, 0.45f, 0.7f);
            phTmp.fontStyle = FontStyles.Italic;
            phTmp.alignment = TextAlignmentOptions.Left;

            var inputField = inputGo.AddComponent<TMP_InputField>();
            inputField.textComponent  = inputTmp;
            inputField.placeholder    = phTmp;
            inputField.characterLimit = 20;
            inputField.text           = _getPlayerName?.Invoke() ?? "";
            _playerNameInput = inputField;

            // SET button
            var setBtnGo = new GameObject("SetNameBtn");
            setBtnGo.transform.SetParent(nameRowGo.transform, false);
            var setBtnRt = setBtnGo.AddComponent<RectTransform>();
            setBtnRt.anchorMin = new Vector2(1f, 0.5f);
            setBtnRt.anchorMax = new Vector2(1f, 0.5f);
            setBtnRt.pivot     = new Vector2(1f, 0.5f);
            setBtnRt.anchoredPosition = new Vector2(-10f, 0f);
            setBtnRt.sizeDelta = new Vector2(105f, 52f);
            setBtnGo.AddComponent<Image>().color = new Color(0.22f, 0.55f, 1f, 1f);
            var setBtn = setBtnGo.AddComponent<Button>();
            setBtn.onClick.AddListener(OnSetPlayerName);
            var setBtnTxtGo = new GameObject("Text");
            setBtnTxtGo.transform.SetParent(setBtnGo.transform, false);
            var sbtRt = setBtnTxtGo.AddComponent<RectTransform>();
            sbtRt.anchorMin = Vector2.zero; sbtRt.anchorMax = Vector2.one;
            sbtRt.offsetMin = sbtRt.offsetMax = Vector2.zero;
            var setBtnTmp = setBtnTxtGo.AddComponent<TextMeshProUGUI>();
            setBtnTmp.text      = "SET";
            setBtnTmp.fontSize  = 22;
            setBtnTmp.fontStyle = FontStyles.Bold;
            setBtnTmp.alignment = TextAlignmentOptions.Center;
            setBtnTmp.color     = Color.white;
            setBtnTmp.raycastTarget = false;

            // Separator below name row
            LBMakeSeparator(panelGo.transform, -202f);

            // ── Row container (VerticalLayoutGroup) ──────────────────────────
            var rowContainerGo = new GameObject("RowContainer");
            rowContainerGo.transform.SetParent(panelGo.transform, false);
            var rcRt = rowContainerGo.AddComponent<RectTransform>();
            rcRt.anchorMin = new Vector2(0f, 0f);
            rcRt.anchorMax = new Vector2(1f, 1f);
            rcRt.offsetMin = new Vector2(15f, 15f);
            rcRt.offsetMax = new Vector2(-15f, -210f);
            var vlg = rowContainerGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing           = 7f;
            vlg.childControlWidth  = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(0, 0, 4, 4);
            _leaderboardRowContainer = rowContainerGo.transform;

            // ── Status / loading text ────────────────────────────────────────
            var statusGo = new GameObject("StatusText");
            statusGo.transform.SetParent(panelGo.transform, false);
            var statusRt = statusGo.AddComponent<RectTransform>();
            statusRt.anchorMin = new Vector2(0.5f, 0.5f);
            statusRt.anchorMax = new Vector2(0.5f, 0.5f);
            statusRt.sizeDelta = new Vector2(560f, 100f);
            statusRt.anchoredPosition = new Vector2(0f, -100f);
            _leaderboardStatusText = statusGo.AddComponent<TextMeshProUGUI>();
            _leaderboardStatusText.text      = "Loading scores...";
            _leaderboardStatusText.fontSize  = 26;
            _leaderboardStatusText.color     = new Color(0.55f, 0.55f, 0.55f);
            _leaderboardStatusText.alignment = TextAlignmentOptions.Center;
            _leaderboardStatusText.raycastTarget = false;
            statusGo.SetActive(false);

            // ── Trophy button (fallback if not inspector-assigned) ────────────
            if (_leaderboardButton == null)
            {
                var btnParent = _hudRoot != null ? _hudRoot.transform : panelParent;
                var btnGo = new GameObject("LeaderboardBtn");
                btnGo.transform.SetParent(btnParent, false);
                var btnRt = btnGo.AddComponent<RectTransform>();
                btnRt.anchorMin = new Vector2(1f, 1f);
                btnRt.anchorMax = new Vector2(1f, 1f);
                btnRt.pivot     = new Vector2(1f, 1f);
                btnRt.anchoredPosition = new Vector2(-18f, -18f);
                btnRt.sizeDelta = new Vector2(82f, 82f);
                btnGo.AddComponent<Image>().color = new Color(0.14f, 0.14f, 0.19f, 0.92f);
                _leaderboardButton = btnGo.AddComponent<Button>();
                var btnColors = _leaderboardButton.colors;
                btnColors.highlightedColor = new Color(1f, 1f, 1f, 0.15f);
                btnColors.pressedColor     = new Color(1f, 1f, 1f, 0.3f);
                _leaderboardButton.colors = btnColors;
                _leaderboardButton.onClick.AddListener(ToggleLeaderboard);

                var btnTxtGo = new GameObject("Text");
                btnTxtGo.transform.SetParent(btnGo.transform, false);
                var btRt = btnTxtGo.AddComponent<RectTransform>();
                btRt.anchorMin = Vector2.zero; btRt.anchorMax = Vector2.one;
                btRt.offsetMin = btRt.offsetMax = Vector2.zero;
                var btnTmp = btnTxtGo.AddComponent<TextMeshProUGUI>();
                btnTmp.text      = "\U0001F3C6"; // 🏆
                btnTmp.fontSize  = 36;
                btnTmp.alignment = TextAlignmentOptions.Center;
                btnTmp.raycastTarget = false;
            }

            _leaderboardRoot.SetActive(false);
        }

        void LBMakeSeparator(Transform parent, float yFromTop)
        {
            var sep = new GameObject("Separator");
            sep.transform.SetParent(parent, false);
            var rt = sep.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.03f, 1f);
            rt.anchorMax = new Vector2(0.97f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, yFromTop);
            rt.sizeDelta = new Vector2(0f, 1f);
            sep.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.07f);
        }

        void LBMakeCloseBtn(Transform parent)
        {
            var go = new GameObject("CloseBtn");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot     = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-14f, 0f);
            rt.sizeDelta = new Vector2(68f, 68f);
            go.AddComponent<Image>().color = new Color(0.28f, 0.10f, 0.10f, 0.75f);
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(ToggleLeaderboard);

            var txtGo = new GameObject("X");
            txtGo.transform.SetParent(go.transform, false);
            var tRt = txtGo.AddComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
            tRt.offsetMin = tRt.offsetMax = Vector2.zero;
            var t = txtGo.AddComponent<TextMeshProUGUI>();
            t.text      = "✕";
            t.fontSize  = 30;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.color     = new Color(1f, 0.55f, 0.55f);
            t.raycastTarget = false;
        }

        public void EnableStartButton()
        {
            if (_loadingRoot == null) return;
            var startBtn = _loadingRoot.GetComponent<Button>();
            if (_loadingStatusText != null)
            {
                _loadingStatusText.transform.DOKill();
                _loadingStatusText.text = "TAP TO START";
                _loadingStatusText.color = new Color(0.4f, 0.8f, 1.0f, 0.8f);
                _loadingStatusText.transform.DOScale(1.1f, 0.8f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true);
            }
            if (startBtn != null) startBtn.interactable = true;
        }

        private void BuildLoadingScreen(Transform parent)
        {
            // FREEZE THE GAME: This is the ONLY way to guarantee the game timer doesn't tick during load.
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

            // Hook up the Button
            var startBtn = _loadingRoot.GetComponent<Button>();
            if (startBtn != null)
            {
                startBtn.interactable = false;
                startBtn.onClick.RemoveAllListeners();
                startBtn.onClick.AddListener(() => {
                    startBtn.interactable = false;
                    Time.timeScale = 1f; // UNFREEZE THE GAME
                    HideLoadingScreen(1.2f);
                });
            }

            // Animate Title
            if (_loadingTitleText != null)
            {
                _loadingTitleText.transform.DOKill();
                _loadingTitleText.transform.localScale = Vector3.one;
                _loadingTitleText.transform.DOScale(1.04f, 3f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true);
            }

            // Animate Glows
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

        public Action OnLoadingDone;

        public void HideLoadingScreen(float duration = 1.2f)
        {
            if (_loadingRoot == null) return;
            var cg = _loadingRoot.GetComponent<CanvasGroup>();
            cg.DOFade(0, duration).SetEase(Ease.InOutQuad).SetUpdate(true).OnComplete(() => {
                _loadingRoot.SetActive(false);
                OnLoadingDone?.Invoke();
            });
        }
    }
}
