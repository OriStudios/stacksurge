using System;
using System.Collections;
using StackSurge.Core;
using StackSurge.Meta;
using StackSurge.Settings;
using StackSurge.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace StackSurge
{
    /// <summary>
    /// Main game loop: queue, placement, bomb, cascades, rising pressure, scoring, meta.
    /// </summary>
    public class StackSurgeGame : MonoBehaviour
    {
        [SerializeField] StackSurgeSettings _settings;
        [SerializeField] ChallengeDefinition[] _challengeAssets;

        GameBoard _board;
        ScoreService _score;
        SaveData _save;
        ChallengeTracker _challenges;

        TileKind _current;
        TileKind _next;

        float _timeAlive;
        float _riseAccumulator;
        float _slowFillBuff;
        bool _playing;
        bool _resolvingMatches;

        Image[,] _cellImages;
        RectTransform _gridRoot;
        TextMeshProUGUI _scoreText;
        TextMeshProUGUI _hudText;
        TextMeshProUGUI _previewText;
        TextMeshProUGUI _helpText;
        GameObject _gameOverRoot;
        TextMeshProUGUI _gameOverScore;
        GameObject _challengesRoot;
        TextMeshProUGUI _challengesBody;
        ScreenShake _shake;

        [SerializeField] float _blinkTime;
        static ChallengeDefinition[] BuildDefaultChallenges()
        {
            ChallengeDefinition C(string title, string desc, ChallengeType ty, int target)
            {
                var c = ScriptableObject.CreateInstance<ChallengeDefinition>();
                c.Title = title;
                c.Description = desc;
                c.Type = ty;
                c.TargetValue = target;
                return c;
            }

            return new[]
            {
                C("Score 1000", "Reach 1000 points in one run.", ChallengeType.ScoreInRun, 1000),
                C("Survivalist", "Stay alive for 3 minutes.", ChallengeType.SurviveSeconds, 180),
                C("Combo hunter", "Reach x5 combo multiplier.", ChallengeType.MaxComboMultiplier, 5)
            };
        }

        void Awake()
        {
            if (_settings == null) _settings = ScriptableObject.CreateInstance<StackSurgeSettings>();

            _save = LocalProgress.Load();
            var defs = _challengeAssets != null && _challengeAssets.Length > 0 ? _challengeAssets : BuildDefaultChallenges();
            _challenges = new ChallengeTracker(defs);

            BuildUi();
        }

        void Start()
        {
            EnsureInputSystemUi();
            BeginRun();
        }

        void Update()
        {
            if (!_playing || _resolvingMatches) return;

            _timeAlive += Time.deltaTime;
            _score.TickSurvivalBonus(Time.deltaTime, _board.GetOccupancy01());

            if (_slowFillBuff > 0f) _slowFillBuff -= Time.deltaTime;

            float interval = _slowFillBuff > 0f ? 8f : DifficultyCurve.GetRowIntervalSeconds(_timeAlive);
            if (_timeAlive >= _settings.InitialRiseGraceSeconds) _riseAccumulator += Time.deltaTime;
            if (_riseAccumulator >= interval)
            {
                _riseAccumulator = 0f;
                StartCoroutine(RiseRowAndResolve());
            }

            UpdateHud();

#if UNITY_EDITOR
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.bKey.wasPressedThisFrame) _slowFillBuff = 5f;
                if (kb.rKey.wasPressedThisFrame && ! _resolvingMatches)
                    StartCoroutine(RiseRowAndResolve());
            }
#endif
        }

        void BeginRun()
        {
            _board = new GameBoard(_settings.Columns, _settings.Rows);
            _score = new ScoreService();
            _score.Reset(_settings.ComboWindowSeconds);
            _timeAlive = 0f;
            _riseAccumulator = 0f;
            _slowFillBuff = 0f;
            _playing = true;
            _resolvingMatches = false;
            _current = RollIncomingTile();
            _next = RollIncomingTile();
            _gameOverRoot.SetActive(false);
            RefreshGrid();
            UpdateHud();
        }

        TileKind RollIncomingTile()
        {
            float wild = DifficultyCurve.GetWildChance(_timeAlive);
            if (UnityEngine.Random.value < wild) return TileKind.Wild;
            if (UnityEngine.Random.value < _settings.BombChance) return TileKind.Bomb;
            return (TileKind)UnityEngine.Random.Range((int)TileKind.Red, (int)TileKind.Purple + 1);
        }

        TileKind RollRisingCell()
        {
            if (UnityEngine.Random.value < DifficultyCurve.GetWildChance(_timeAlive)) return TileKind.Wild;
            return (TileKind)UnityEngine.Random.Range((int)TileKind.Red, (int)TileKind.Purple + 1);
        }

        public void OnColumnClicked(int col)
        {
            if (!_playing || _resolvingMatches) return;

            int row = _board.GetLowestEmptyRow(col);
            if (row < 0)
            {
                EndRun();
                return;
            }

            var placed = _current;
            _current = _next;
            _next = RollIncomingTile();

            if (placed == TileKind.Bomb)
            {
                StartCoroutine(ExplodeBombAndResolve(col, row));
                UpdateHud();
                return;
            }

            _board.SetCell(col, row, placed);
            RefreshGrid();
            UpdateHud();

            StartCoroutine(ResolveMatchesAnimCoroutine());
        }

        IEnumerator ResolveMatchesAnimCoroutineInner()
        {
            bool emptyAfter = false;
            var matches = new System.Collections.Generic.HashSet<(int r, int c)>();

            while (true)
            {
                MatchFinder.CollectMatches(_board.Cells, _board.Width, _board.Height, matches, out int largestInWave);
                if (matches.Count == 0) break;

                // Animate/blink tiles before clearing
                float blinkTime = _blinkTime;
                float elapsed = 0f;
                while (elapsed < blinkTime)
                {
                    elapsed += Time.deltaTime;
                    bool flashToggle = (int)(elapsed / 0.05f) % 2 == 0;
                    foreach (var m in matches)
                    {
                        if (flashToggle)
                            _cellImages[m.r, m.c].color = Color.white;
                        else
                            _cellImages[m.r, m.c].color = ColorFor(_board.Cells[m.r, m.c]);
                    }
                    yield return null;
                }

                bool hasFullRow = HasEntireRowInMatch(matches);
                int cleared = matches.Count;

                foreach (var m in matches)
                {
                    _board.Cells[m.r, m.c] = TileKind.Empty;
                    _cellImages[m.r, m.c].color = ColorFor(TileKind.Empty);
                }

                yield return AnimateGravityCoroutine();

                bool perfect = _board.IsBoardEmpty();
                _score.RegisterClearWave(largestInWave, cleared, hasFullRow, perfect, Time.time, out _);

                OnCleared(cleared);
                RefreshGrid();
                UpdateHud();

                yield return new WaitForSeconds(0.1f);

                if (perfect)
                {
                    emptyAfter = true;
                    break;
                }
            }

            if (emptyAfter) _slowFillBuff = _settings.SlowFillBuffSeconds;

            _challenges.TickRun(_timeAlive, _score.TotalScore, _score.BestComboMultiplier, _save);
            UpdateHud();
        }

        bool HasEntireRowInMatch(System.Collections.Generic.HashSet<(int r, int c)> matchSet)
        {
            for (int r = 0; r < _board.Height; r++)
            {
                bool ok = true;
                for (int c = 0; c < _board.Width; c++)
                {
                    if (!matchSet.Contains((r, c)))
                    {
                        ok = false;
                        break;
                    }
                }
                if (ok) return true;
            }
            return false;
        }

        IEnumerator AnimateGravityCoroutine()
        {
            int[,] fallDistances = new int[_settings.Rows, _settings.Columns];
            bool anyFalls = false;
            for (int c = 0; c < _settings.Columns; c++)
            {
                int emptySum = 0;
                for (int r = 0; r < _settings.Rows; r++)
                {
                    if (_board.Cells[r, c] == TileKind.Empty) 
                        emptySum++;
                    else if (emptySum > 0)
                    {
                        fallDistances[r, c] = emptySum;
                        anyFalls = true;
                    }
                }
            }

            _board.ApplyGravity();

            if (anyFalls)
            {
                float fallTime = 0.2f;
                float fElapsed = 0f;
                float cellH = 900f / _settings.Rows;
                float cw = 700f / _settings.Columns;
                
                while (fElapsed < fallTime)
                {
                    fElapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(fElapsed / fallTime);
                    float easedT = t * t; // accelerate like gravity
                    
                    for (int r = 0; r < _settings.Rows; r++)
                    for (int c = 0; c < _settings.Columns; c++)
                    {
                        if (fallDistances[r, c] > 0)
                        {
                            float startY = r * cellH + 2f;
                            float endY = (r - fallDistances[r, c]) * cellH + 2f;
                            _cellImages[r, c].rectTransform.anchoredPosition = new Vector2(
                                c * cw + 2f, 
                                Mathf.Lerp(startY, endY, easedT)
                            );
                        }
                    }
                    yield return null;
                }
                
                for (int r = 0; r < _settings.Rows; r++)
                for (int c = 0; c < _settings.Columns; c++)
                {
                    if (fallDistances[r, c] > 0)
                    {
                        _cellImages[r, c].rectTransform.anchoredPosition = new Vector2(
                            c * cw + 2f, 
                            r * cellH + 2f
                        );
                    }
                }
            }
        }

        IEnumerator ResolveMatchesAnimCoroutine()
        {
            _resolvingMatches = true;
            yield return ResolveMatchesAnimCoroutineInner();
            _resolvingMatches = false;
        }

        IEnumerator ExplodeBombAndResolve(int col, int row)
        {
            _resolvingMatches = true;
            _board.SetCell(col, row, TileKind.Bomb);
            RefreshGrid();

            // Blink bomb area
            float blinkTime = 0.25f;
            float elapsed = 0f;
            while (elapsed < blinkTime)
            {
                elapsed += Time.deltaTime;
                bool flashToggle = (int)(elapsed / 0.05f) % 2 == 0;
                for (int dc = -1; dc <= 1; dc++)
                for (int dr = -1; dr <= 1; dr++)
                {
                    int cc = col + dc;
                    int rr = row + dr;
                    if (cc < 0 || cc >= _board.Width || rr < 0 || rr >= _board.Height) continue;
                    if (_board.Cells[rr, cc] == TileKind.Empty) continue;
                    
                    if (flashToggle)
                        _cellImages[rr, cc].color = Color.white;
                    else
                        _cellImages[rr, cc].color = ColorFor(_board.Cells[rr, cc]);
                }
                yield return null;
            }

            _board.ExplodeBomb3x3(col, row);
            
            for (int r = 0; r < _settings.Rows; r++)
            for (int c = 0; c < _settings.Columns; c++)
                if (_board.Cells[r, c] == TileKind.Empty)
                    _cellImages[r, c].color = ColorFor(TileKind.Empty);

            yield return AnimateGravityCoroutine();
            RefreshGrid();

            yield return ResolveMatchesAnimCoroutineInner();
            _resolvingMatches = false;
        }

        IEnumerator RiseRowAndResolve()
        {
            _resolvingMatches = true;

            if (!_board.TryRiseRow(RollRisingCell))
            {
                EndRun();
                yield break;
            }

            RefreshGrid();

            float slideTime = 0.2f;
            float elapsed = 0f;
            float cellH = 900f / _settings.Rows; // default logical height

            while (elapsed < slideTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / slideTime;
                
                // Use a curve so it feels a bit punchy but smooth
                float easedT = 1f - (1f - t) * (1f - t); 
                
                float yOffset = Mathf.Lerp(-cellH, 0f, easedT);
                _gridRoot.anchoredPosition = new Vector2(0, yOffset);
                yield return null;
            }

            _gridRoot.anchoredPosition = Vector2.zero;

            yield return ResolveMatchesAnimCoroutineInner();
            _resolvingMatches = false;
        }

        void OnCleared(int tileCount)
        {
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
            float shake = _settings.ShakeBase + Mathf.Max(0, tileCount - 4) * _settings.ShakePerExtraTile;
            _shake.AddShake(shake, 0.12f);
        }

        void EndRun()
        {
            _playing = false;
            _challenges.TickRun(_timeAlive, _score.TotalScore, _score.BestComboMultiplier, _save);
            LocalProgress.RegisterRunEnd(_score.TotalScore, _save);
            _gameOverScore.text =
                $"Score: {_score.TotalScore}\nDaily best: {_save.DailyBest}\nAll-time: {_save.AllTimeHigh}\nStreak: {_save.Streak} days";
            _gameOverRoot.SetActive(true);
        }

        public void Retry()
        {
            BeginRun();
        }

        public void ShareStub()
        {
            var msg = $"Stack Surge — {_score.TotalScore} points!";
            GUIUtility.systemCopyBuffer = msg;
            Debug.Log("Share (stub): " + msg);
        }

        void UpdateHud()
        {
            float interval = _slowFillBuff > 0f ? 8f : DifficultyCurve.GetRowIntervalSeconds(_timeAlive);
            float nextRise = Mathf.Max(0f, interval - _riseAccumulator);
            float graceLeft = Mathf.Max(0f, _settings.InitialRiseGraceSeconds - _timeAlive);
            string riseLine = graceLeft > 0f
                ? $"First auto rise in: {graceLeft:0.0}s"
                : $"Next auto rise: {nextRise:0.0}s";

            _scoreText.text = _score.TotalScore.ToString();
            _hudText.text =
                $"Time: {FormatTime(_timeAlive)}\n{riseLine}\nWild ~{DifficultyCurve.GetWildChance(_timeAlive) * 100f:0}%\n" +
                "- \"Rise\" pushes a full row in from the bottom; matches of 3+ clear.";
            _previewText.text = $"Your tile (drop next): {placedName(_current)}\nQueued after: {placedName(_next)}";
        }

        static string FormatTime(float t)
        {
            int m = (int)(t / 60f);
            int s = (int)(t % 60f);
            return $"{m:00}:{s:00}";
        }

        static string placedName(TileKind k) =>
            k switch
            {
                TileKind.Wild => "Wild",
                TileKind.Bomb => "Bomb",
                _ => k.ToString()
            };

        void RefreshGrid()
        {
            for (int r = 0; r < _board.Height; r++)
            for (int c = 0; c < _board.Width; c++)
            {
                _cellImages[r, c].color = ColorFor(_board.Cells[r, c]);
            }
        }

        static Color ColorFor(TileKind k)
        {
            return k switch
            {
                TileKind.Empty => new Color(0.15f, 0.15f, 0.18f, 1f),
                TileKind.Red => new Color(0.95f, 0.25f, 0.25f),
                TileKind.Blue => new Color(0.25f, 0.45f, 0.95f),
                TileKind.Green => new Color(0.35f, 0.85f, 0.35f),
                TileKind.Yellow => new Color(0.95f, 0.85f, 0.2f),
                TileKind.Purple => new Color(0.65f, 0.35f, 0.95f),
                TileKind.Wild => Color.white,
                TileKind.Bomb => new Color(0.2f, 0.2f, 0.2f),
                _ => Color.gray
            };
        }

        static void EnsureInputSystemUi()
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

            var top = CreatePanel(root.transform, "Top", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -40), new Vector2(1000, 200));
            _scoreText = CreateTmp(top.transform, "Score", 72, FontStyles.Bold, TextAlignmentOptions.Top);
            _scoreText.rectTransform.anchoredPosition = new Vector2(0, -20);
            _hudText = CreateTmp(top.transform, "Hud", 28, FontStyles.Normal, TextAlignmentOptions.Top);
            _hudText.rectTransform.anchoredPosition = new Vector2(0, -120);
            _previewText = CreateTmp(top.transform, "Preview", 32, FontStyles.Normal, TextAlignmentOptions.Top);
            _previewText.rectTransform.anchoredPosition = new Vector2(0, -290);
            _helpText = CreateTmp(top.transform, "Help", 26, FontStyles.Normal, TextAlignmentOptions.Top);
            _helpText.color = new Color(0.9f, 0.9f, 0.95f, 1f);
            _helpText.rectTransform.sizeDelta = new Vector2(0, 420);
            _helpText.rectTransform.anchoredPosition = new Vector2(0, -410);
            _helpText.text =
                "How to play\n" +
                "Tap a blue column strip below (1–7) to drop your tile into that column. " +
                "Tiles stack from the bottom up. Line up 3+ of the same color in a row, column, or L " +
                "to clear them (they will disappear and you score). " +
                "New rows also push up on a timer—don’t let the top overflow!";

            var gridMaskGo = new GameObject("GridMask");
            gridMaskGo.transform.SetParent(root.transform, false);
            var maskRt = gridMaskGo.AddComponent<RectTransform>();
            maskRt.anchorMin = new Vector2(0.5f, 0.5f);
            maskRt.anchorMax = new Vector2(0.5f, 0.5f);
            maskRt.sizeDelta = new Vector2(700, 900);
            maskRt.anchoredPosition = new Vector2(0, -80);
            gridMaskGo.AddComponent<UnityEngine.UI.RectMask2D>();

            var gridGo = new GameObject("Grid");
            gridGo.transform.SetParent(gridMaskGo.transform, false);
            _gridRoot = gridGo.AddComponent<RectTransform>();
            _gridRoot.anchorMin = Vector2.zero;
            _gridRoot.anchorMax = Vector2.one;
            _gridRoot.offsetMin = Vector2.zero;
            _gridRoot.offsetMax = Vector2.zero;

            _cellImages = new Image[_settings.Rows, _settings.Columns];
            float cw = 700f / _settings.Columns;
            float cellH = 900f / _settings.Rows;
            for (int r = 0; r < _settings.Rows; r++)
            for (int c = 0; c < _settings.Columns; c++)
            {
                var cell = new GameObject($"c_{r}_{c}");
                cell.transform.SetParent(_gridRoot, false);
                var rt = cell.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(cw - 4f, cellH - 4f);
                rt.anchorMin = new Vector2(0, 0);
                rt.anchorMax = new Vector2(0, 0);
                rt.pivot = new Vector2(0, 0);
                rt.anchoredPosition = new Vector2(c * cw + 2f, r * cellH + 2f);
                var img = cell.AddComponent<Image>();
                img.color = ColorFor(TileKind.Empty);
                _cellImages[r, c] = img;
            }

            float colY = 120f;
            for (int c = 0; c < _settings.Columns; c++)
            {
                int col = c;
                var btnGo = new GameObject($"Col{c}");
                btnGo.transform.SetParent(root.transform, false);
                var rt = btnGo.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0);
                rt.anchorMax = new Vector2(0.5f, 0);
                rt.pivot = new Vector2(0.5f, 0);
                rt.sizeDelta = new Vector2(cw - 2f, 340f);
                rt.anchoredPosition = new Vector2((c - (_settings.Columns - 1) / 2f) * cw, colY);
                var img = btnGo.AddComponent<Image>();
                img.color = new Color(0.35f, 0.55f, 0.95f, 0.22f);
                var btn = btnGo.AddComponent<Button>();
                var colors = btn.colors;
                colors.highlightedColor = new Color(0.5f, 0.7f, 1f, 0.45f);
                colors.pressedColor = new Color(0.25f, 0.45f, 0.85f, 0.5f);
                btn.colors = colors;
                btn.onClick.AddListener(() => OnColumnClicked(col));

                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(btnGo.transform, false);
                var lrt = labelGo.AddComponent<RectTransform>();
                lrt.anchorMin = new Vector2(0.5f, 1f);
                lrt.anchorMax = new Vector2(0.5f, 1f);
                lrt.pivot = new Vector2(0.5f, 1f);
                lrt.sizeDelta = new Vector2(cw, 56f);
                lrt.anchoredPosition = new Vector2(0, -4f);
                var lt = labelGo.AddComponent<TextMeshProUGUI>();
                lt.text = (c + 1).ToString();
                lt.fontSize = 40;
                lt.fontStyle = FontStyles.Bold;
                lt.alignment = TextAlignmentOptions.Center;
                lt.color = Color.white;
            }

            _gameOverRoot = CreatePanel(root.transform, "GameOver", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(900, 1100));
            var goBg = _gameOverRoot.GetComponent<Image>();
            goBg.color = new Color(0f, 0f, 0f, 0.82f);
            _gameOverScore = CreateTmp(_gameOverRoot.transform, "GOScore", 40, FontStyles.Normal, TextAlignmentOptions.Center);
            _gameOverScore.rectTransform.anchoredPosition = new Vector2(0, 120);

            var retry = CreateButton(_gameOverRoot.transform, "Retry", new Vector2(0, -80), () => Retry());
            var share = CreateButton(_gameOverRoot.transform, "Copy score", new Vector2(0, -200), ShareStub);

            _gameOverRoot.SetActive(false);

            {
                var chBtn = CreateButton(root.transform, "Challenges", Vector2.zero, ToggleChallenges);
                var crt = chBtn.GetComponent<RectTransform>();
                crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.06f);
                crt.pivot = new Vector2(0.5f, 0f);
                crt.sizeDelta = new Vector2(360, 76);
                crt.anchoredPosition = Vector2.zero;
            }
            _challengesRoot = CreatePanel(root.transform, "ChallengesPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(900, 1200));
            _challengesRoot.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f, 0.96f);
            _challengesBody = CreateTmp(_challengesRoot.transform, "Body", 28, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            _challengesBody.rectTransform.anchorMin = new Vector2(0, 1);
            _challengesBody.rectTransform.anchorMax = new Vector2(1, 1);
            _challengesBody.rectTransform.pivot = new Vector2(0.5f, 1);
            _challengesBody.rectTransform.offsetMin = new Vector2(40, 40);
            _challengesBody.rectTransform.offsetMax = new Vector2(-40, -120);
            CreateButton(_challengesRoot.transform, "Close", new Vector2(0, -520), () => _challengesRoot.SetActive(false));
            _challengesRoot.SetActive(false);
        }

        void ToggleChallenges()
        {
            bool on = !_challengesRoot.activeSelf;
            _challengesRoot.SetActive(on);
            if (!on) return;
            var defs = _challengeAssets != null && _challengeAssets.Length > 0 ? _challengeAssets : BuildDefaultChallenges();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Local challenges (no server)");
            sb.AppendLine();
            for (int i = 0; i < defs.Length; i++)
            {
                var d = defs[i];
                if (d == null) continue;
                bool done = i < _save.ChallengeBits.Length && _save.ChallengeBits[i] != 0;
                sb.AppendLine($"{(done ? "[x]" : "[ ]")} {d.Title}");
                sb.AppendLine(d.Description);
                sb.AppendLine();
            }

            _challengesBody.text = sb.ToString();
        }

        static GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos,
            Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            go.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.12f, 0.9f);
            return go;
        }

        static TextMeshProUGUI CreateTmp(Transform parent, string name, float size, FontStyles style,
            TextAlignmentOptions align)
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
            return t;
        }

        static Button CreateButton(Transform parent, string label, Vector2 pos, Action onClick)
        {
            var go = new GameObject(label + "Btn");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(400, 90);
            rt.anchoredPosition = pos;
            go.AddComponent<Image>().color = new Color(0.25f, 0.55f, 0.95f, 1f);
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
            t.fontSize = 36;
            t.alignment = TextAlignmentOptions.Center;
            t.color = Color.white;

            return btn;
        }
    }
}
