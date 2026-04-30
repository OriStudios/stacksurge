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
        TextMeshProUGUI _scoreText;
        TextMeshProUGUI _hudText;
        
        // Visual Previews
        Image _nextPreviewImg;
        Image _queuedPreviewImg;
        TextMeshProUGUI _nextLabel;
        TextMeshProUGUI _queuedLabel;

        TextMeshProUGUI _helpText;
        GameObject _hudRoot;
        GameObject _gameOverRoot;
        GameObject _loadingRoot;
        TextMeshProUGUI _gameOverScore;
        GameObject _challengesRoot;
        TextMeshProUGUI _challengesBody;
        ScreenShake _shake;

        Action<int> _onColumnClicked;
        Action _onRetry;
        Action _onShare;
        Func<string> _getChallengesText;

        private int _lastScore = 0;

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
            
            UpdatePreviewSquare(_nextPreviewImg, _nextLabel, current);
            UpdatePreviewSquare(_queuedPreviewImg, _queuedLabel, next);
        }

        private void UpdatePreviewSquare(Image img, TextMeshProUGUI label, TileKind k)
        {
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
            _gameOverRoot.SetActive(false);
            _hudRoot.SetActive(true);
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
            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            _shake = canvasGo.AddComponent<ScreenShake>();
            _shake.Target = canvasGo.transform;

            var root = new GameObject("Root");
            root.transform.SetParent(canvasGo.transform, false);
            var rootRt = root.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            // Dark Background
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(root.transform, false);
            var bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;
            bgGo.AddComponent<Image>().color = new Color(0.06f, 0.06f, 0.08f, 1f);

            // HUD Root (holds all gameplay elements)
            _hudRoot = new GameObject("HudRoot");
            _hudRoot.transform.SetParent(root.transform, false);
            var hrt_root = _hudRoot.AddComponent<RectTransform>();
            hrt_root.anchorMin = Vector2.zero;
            hrt_root.anchorMax = Vector2.one;
            hrt_root.offsetMin = Vector2.zero;
            hrt_root.offsetMax = Vector2.zero;

            // Score Panel (Dark Glass)
            var scorePanel = CreatePanel(_hudRoot.transform, "ScorePanel", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -180), new Vector2(600, 240));
            scorePanel.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f, 0.8f);
            
            _scoreText = CreateTmp(scorePanel.transform, "Score", 140, FontStyles.Bold, TextAlignmentOptions.Center);
            _scoreText.rectTransform.anchoredPosition = new Vector2(0, 0);
            _scoreText.color = Color.white;
            
            _hudText = CreateTmp(_hudRoot.transform, "Hud", 40, FontStyles.Normal, TextAlignmentOptions.Center);
            _hudText.rectTransform.anchoredPosition = new Vector2(0, -320);
            _hudText.color = new Color(0.7f, 0.7f, 0.7f, 1f);

            // Preview Section (Left Side)
            var previewRoot = new GameObject("Previews");
            previewRoot.transform.SetParent(_hudRoot.transform, false);
            var prt = previewRoot.AddComponent<RectTransform>();
            prt.anchorMin = new Vector2(0, 1);
            prt.anchorMax = new Vector2(0, 1);
            prt.pivot = new Vector2(0, 1);
            prt.anchoredPosition = new Vector2(40, -100);
            prt.sizeDelta = new Vector2(240, 500);

            _nextPreviewImg = CreatePreviewSlot(previewRoot.transform, "NEXT TILE", new Vector2(0, 0), out _nextLabel);
            _queuedPreviewImg = CreatePreviewSlot(previewRoot.transform, "QUEUED", new Vector2(0, -220), out _queuedLabel);

            // Help Button (Top Right)
            var helpBtn = CreateButton(_hudRoot.transform, "?", new Vector2(460, -80), () => _helpText.gameObject.SetActive(!_helpText.gameObject.activeSelf));
            var hrt = helpBtn.GetComponent<RectTransform>();
            hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 1f);
            hrt.sizeDelta = new Vector2(100, 100);
            helpBtn.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f, 0.8f);
            helpBtn.GetComponentInChildren<TextMeshProUGUI>().color = Color.white;

            _helpText = CreateTmp(_hudRoot.transform, "HelpOverlay", 32, FontStyles.Normal, TextAlignmentOptions.Center);
            _helpText.color = new Color(0.9f, 0.9f, 0.95f, 0.95f);
            _helpText.rectTransform.sizeDelta = new Vector2(900, 400);
            _helpText.rectTransform.anchoredPosition = new Vector2(0, -600);
            _helpText.text = "Tap columns to drop tiles.\nMatch 3+ to clear!";
            _helpText.gameObject.SetActive(false);

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

            _gameOverRoot = CreatePanel(root.transform, "GameOver", Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero); // Fullscreen stretch
            _gameOverRoot.AddComponent<CanvasGroup>();
            _gameOverRoot.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 0.98f);
            
            // Fix Game Over Text Layout (Refined Spacing)
            var goTitle = CreateTmp(_gameOverRoot.transform, "Title", 120, FontStyles.Bold, TextAlignmentOptions.Center);
            goTitle.text = "GAME OVER";
            goTitle.color = Color.white;
            goTitle.rectTransform.anchorMin = goTitle.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            goTitle.rectTransform.sizeDelta = new Vector2(900, 200);
            goTitle.rectTransform.anchoredPosition = new Vector2(0, 550);
            
            _gameOverScore = CreateTmp(_gameOverRoot.transform, "GOScore", 54, FontStyles.Normal, TextAlignmentOptions.Center);
            _gameOverScore.color = Color.white;
            _gameOverScore.rectTransform.anchorMin = _gameOverScore.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _gameOverScore.rectTransform.sizeDelta = new Vector2(900, 500);
            _gameOverScore.rectTransform.anchoredPosition = new Vector2(0, 150);

            CreateButton(_gameOverRoot.transform, "RETRY", new Vector2(0, -350), () => _onRetry?.Invoke());
            CreateButton(_gameOverRoot.transform, "SHARE", new Vector2(0, -500), () => _onShare?.Invoke());

            _gameOverRoot.SetActive(false);

            // Challenges Button (Bottom Right)
            {
                var chBtn = CreateButton(root.transform, "Challenges", new Vector2(400, 100), ToggleChallengesClick);
                var crt = chBtn.GetComponent<RectTransform>();
                crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0f);
                crt.sizeDelta = new Vector2(250, 100);
                chBtn.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f, 0.8f);
                chBtn.GetComponentInChildren<TextMeshProUGUI>().text = "TROPHY"; // No emoji to avoid font error
                chBtn.GetComponentInChildren<TextMeshProUGUI>().fontSize = 32;
            }
            
            _challengesRoot = CreatePanel(root.transform, "ChallengesPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(950, 1300));
            _challengesRoot.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 0.98f);
            
            var chTitle = CreateTmp(_challengesRoot.transform, "Title", 80, FontStyles.Bold, TextAlignmentOptions.Center);
            chTitle.text = "CHALLENGES";
            chTitle.color = Color.white;
            chTitle.rectTransform.anchoredPosition = new Vector2(0, 560);
            
            _challengesBody = CreateTmp(_challengesRoot.transform, "Body", 36, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            _challengesBody.color = Color.white;
            _challengesBody.rectTransform.anchorMin = new Vector2(0, 1);
            _challengesBody.rectTransform.anchorMax = new Vector2(1, 1);
            _challengesBody.rectTransform.pivot = new Vector2(0.5f, 1);
            _challengesBody.rectTransform.offsetMin = new Vector2(80, 200);
            _challengesBody.rectTransform.offsetMax = new Vector2(-80, -200);
            
            CreateButton(_challengesRoot.transform, "CLOSE", new Vector2(0, -560), () => _challengesRoot.SetActive(false));
            _challengesRoot.SetActive(false);

            BuildLoadingScreen(canvasGo.transform);
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

        void ToggleChallengesClick()
        {
            bool on = !_challengesRoot.activeSelf;
            _challengesRoot.SetActive(on);
            if (on && _getChallengesText != null)
            {
                _challengesBody.text = _getChallengesText();
            }
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

            _loadingRoot = new GameObject("LoadingScreen");
            _loadingRoot.transform.SetParent(parent, false);
            _loadingRoot.transform.SetAsLastSibling(); // Force it to the front
            
            var rt = _loadingRoot.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            
            var cg = _loadingRoot.AddComponent<CanvasGroup>();
            cg.alpha = 1f; // Force absolute opacity

            // Dedicated background child to ensure perfect opacity and layering
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(_loadingRoot.transform, false);
            var bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.04f, 0.04f, 0.06f, 1f);
            bgImg.raycastTarget = true;

            var startBtn = _loadingRoot.AddComponent<Button>();
            startBtn.interactable = false;
            startBtn.onClick.AddListener(() => {
                startBtn.interactable = false;
                Time.timeScale = 1f; // UNFREEZE THE GAME
                HideLoadingScreen(1.2f);
            });

            // Layered Depth Glow (from the 2.0 version)
            CreateGlow(_loadingRoot.transform, new Color(0.4f, 0.7f, 1.0f, 0.08f), 800, 2.5f);
            CreateGlow(_loadingRoot.transform, new Color(0.8f, 0.4f, 1.0f, 0.04f), 1000, 4.0f);

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(_loadingRoot.transform, false);
            var trt = titleGo.AddComponent<RectTransform>();
            trt.sizeDelta = new Vector2(1200, 300);
            
            var title = titleGo.AddComponent<TextMeshProUGUI>();
            title.text = "STACK SURGE";
            title.fontSize = 84;
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Center;
            title.characterSpacing = 25; 
            title.color = Color.white;
            title.transform.DOScale(1.04f, 3f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true);

            var subGo = new GameObject("Subtitle");
            subGo.transform.SetParent(_loadingRoot.transform, false);
            var sub = subGo.AddComponent<TextMeshProUGUI>();
            sub.text = "INITIALIZING CORE SYSTEM";
            sub.fontSize = 20;
            sub.fontStyle = FontStyles.Bold;
            sub.alignment = TextAlignmentOptions.Center;
            sub.characterSpacing = 10;
            sub.color = new Color(1, 1, 1, 0.3f);
            sub.rectTransform.anchoredPosition = new Vector2(0, -120);

            // Transition to 'TAP TO START' after initialization delay
            DG.Tweening.DOVirtual.DelayedCall(2.0f, () => {
                if (sub == null) return;
                sub.text = "TAP TO START";
                sub.color = new Color(0.4f, 0.8f, 1.0f, 0.8f);
                sub.transform.DOScale(1.1f, 0.8f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true);
                startBtn.interactable = true;
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
