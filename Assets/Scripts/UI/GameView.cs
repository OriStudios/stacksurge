using System;
using System.Collections;
using System.Collections.Generic;
using StackSurge.Core;
using StackSurge.Settings;
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
        [SerializeField] TextMeshProUGUI _challengesBody;
        [SerializeField] Button _challengesButton;
        [SerializeField] Button _helpButton;
        ScreenShake _shake;

        Action<int> _onColumnClicked;
        Action _onRetry;
        Action _onShare;
        Func<string> _getChallengesText;

        private int _lastScore = 0;

        void Awake()
        {
            if(_closeHelpButton != null) _closeHelpButton.onClick.AddListener(ToggleHelp);
        }

        public void Build(StackSurgeSettings settings, Action<int> onColumnClicked, Action onRetry, Action onShare, Func<string> getChallengesText)
        {
            _settings = settings;
            _onColumnClicked = onColumnClicked;
            _onRetry = onRetry;
            _onShare = onShare;
            _getChallengesText = getChallengesText;

            EnsureInputSystemUi();
            BuildUi();
            
            DOTween.SetTweensCapacity(500, 50);
            _retryButton.onClick.AddListener(Retry);
            _shareButton.onClick.AddListener(Share);

            if (_challengesButton != null) _challengesButton.onClick.AddListener(ToggleChallenges);
            if (_helpButton != null) _helpButton.onClick.AddListener(ToggleHelp);
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
            
            if (active && _getChallengesText != null)
            {
                _challengesBody.text = _getChallengesText();
                
                // Hook up the close button inside the challenges panel
                var closeBtn = _challengesRoot.GetComponentInChildren<Button>();
                if (closeBtn != null && closeBtn != _challengesButton)
                {
                    // Remove existing listeners to avoid duplicates
                    closeBtn.onClick.RemoveAllListeners();
                    closeBtn.onClick.AddListener(ToggleChallenges);
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

            // Transition to 'TAP TO START' after initialization delay
            DG.Tweening.DOVirtual.DelayedCall(2.0f, () => {
                if (_loadingStatusText != null)
                {
                    _loadingStatusText.text = "TAP TO START";
                    _loadingStatusText.color = new Color(0.4f, 0.8f, 1.0f, 0.8f);
                    _loadingStatusText.transform.DOScale(1.1f, 0.8f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true);
                }
                if (startBtn != null) startBtn.interactable = true;
            }).SetUpdate(true);
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
