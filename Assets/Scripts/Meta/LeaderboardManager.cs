using System;
using System.Threading.Tasks;
using DG.Tweening;
using StackSurge.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StackSurge.Meta
{
    public class LeaderboardManager : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject _leaderboardRoot;
        [SerializeField] private Transform _leaderboardRowContainer;
        [SerializeField] private TextMeshProUGUI _leaderboardStatusText;
        [SerializeField] private TMP_InputField _playerNameInput;
        [SerializeField] private TextMeshProUGUI _yourPositionText;

        [Header("Leaderboard Tabs")]
        [SerializeField] private Button _dailyLeaderboardTabButton;
        [SerializeField] private Button _weeklyLeaderboardTabButton;
        [SerializeField] private Button _allTimeLeaderboardTabButton;
        [SerializeField] private TextMeshProUGUI _leaderboardTitleText;

        [SerializeField] private Button _leaderboardButton;

        [Header("Assets")]
        [SerializeField] private LeaderboardRowView _rowPrefab;
        [SerializeField] private Sprite _goldMedal, _silverMedal, _bronzeMedal;

        private Func<LeaderboardScope, Task<LeaderboardEntryData[]>> _getLeaderboardScores;
        private Func<LeaderboardScope, Task<LeaderboardEntryData?>> _getPlayerEntry;
        private Func<string, Task> _setPlayerName;
        private Func<string> _getPlayerName;
        private Action<string> _onAddFriend;
        private LeaderboardScope _activeScope = LeaderboardScope.AllTime;
        private int _refreshGeneration = 0; // incremented on every refresh; stale calls self-abort

        [SerializeField] private Button _setPlayerNameButton;
        [SerializeField] private Button _closeButton;

        //restore old leaderboard scores from archive
        //[SerializeField] private Button _restoreArchiveButton;
        //[SerializeField] private string _leaderboardId = "all_time_highs";
        //[SerializeField] private string _archivedVersionId = "20260617043612741436307";

        void Awake()
        {
            if (_leaderboardButton != null)
                _leaderboardButton.onClick.AddListener(ToggleLeaderboard);

            if (_dailyLeaderboardTabButton != null)
                _dailyLeaderboardTabButton.onClick.AddListener(() => SetLeaderboardScope(LeaderboardScope.Daily));
            if (_weeklyLeaderboardTabButton != null)
                _weeklyLeaderboardTabButton.onClick.AddListener(() => SetLeaderboardScope(LeaderboardScope.Weekly));
            if (_allTimeLeaderboardTabButton != null)
                _allTimeLeaderboardTabButton.onClick.AddListener(() => SetLeaderboardScope(LeaderboardScope.AllTime));

            if (_setPlayerNameButton != null)
                _setPlayerNameButton.onClick.AddListener(OnSetPlayerName);

            if (_closeButton != null)
                _closeButton.onClick.AddListener(ToggleLeaderboard);

            //if (_restoreArchiveButton != null)
            //    _restoreArchiveButton.onClick.AddListener(OnRestoreArchive);
        }

        //Restore archived leaderboard scores when the button is clicked
        /*
        public async void OnRestoreArchive()
        {
            _leaderboardStatusText.text = "Restoring archived scores...";
            _leaderboardStatusText.gameObject.SetActive(true);

            await ArchiveRestoreService.RestoreFromArchive(_leaderboardId, _archivedVersionId);

            await Task.Delay(800); // Allow UGS to propagate
            _ = RefreshLeaderboard();
        } */

        // --- INITIALIZATION ---
        public void SetLeaderboardCallbacks(
            Func<LeaderboardScope, Task<LeaderboardEntryData[]>> getLeaderboardScores,
            Func<LeaderboardScope, Task<LeaderboardEntryData?>> getPlayerEntry,
            Func<string, Task> setPlayerName,
            Func<string> getPlayerName,
            Action<string> onAddFriend = null)
        {
            _getLeaderboardScores = getLeaderboardScores;
            _getPlayerEntry       = getPlayerEntry;
            _setPlayerName        = setPlayerName;
            _getPlayerName        = getPlayerName;
            _onAddFriend          = onAddFriend;

            if (_playerNameInput != null)
                _playerNameInput.text = _getPlayerName?.Invoke() ?? "";

            RefreshLeaderboardScopeUi();
        }

        // --- REFRESH LOGIC ---
        public async Task RefreshLeaderboard()
        {
            if (_leaderboardRowContainer == null) return;

            // Claim this refresh slot; any older in-flight call will see a stale generation and abort.
            int generation = ++_refreshGeneration;

            // Clear existing rows
            foreach (Transform child in _leaderboardRowContainer)
                Destroy(child.gameObject);

            _leaderboardStatusText.text = $"Loading {_activeScope.ToString().ToLower()} scores...";
            _leaderboardStatusText.gameObject.SetActive(true);

            LeaderboardEntryData[] entries = Array.Empty<LeaderboardEntryData>();
            if (_getLeaderboardScores != null)
            {
                try { entries = await _getLeaderboardScores(_activeScope); }
                catch { entries = Array.Empty<LeaderboardEntryData>(); }
            }

            // Another tab was clicked while we were awaiting – discard these results.
            if (generation != _refreshGeneration) return;

            _leaderboardStatusText.gameObject.SetActive(false);

            // Fetch the current player's personal entry (rank + score)
            LeaderboardEntryData? playerEntry = null;
            if (_getPlayerEntry != null)
            {
                try { playerEntry = await _getPlayerEntry(_activeScope); }
                catch { /* swallow – non-critical */ }
            }

            // Discard again if superseded during the second await.
            if (generation != _refreshGeneration) return;

            // Populate "Your position" header
            if (_yourPositionText != null)
            {
                _yourPositionText.text = playerEntry.HasValue
                    ? $"Your position: #{playerEntry.Value.Rank}"
                    : "Your position: —";
            }

            if (entries.Length == 0)
            {
                _leaderboardStatusText.gameObject.SetActive(true);
                _leaderboardStatusText.text = "Could not load scores.";
                return;
            }

            // Determine whether the current player is already visible in the top list
            bool playerInTop = false;
            for (int i = 0; i < entries.Length; i++)
                if (entries[i].IsCurrentPlayer) { playerInTop = true; break; }

            // Build top-15 rows
            for (int i = 0; i < entries.Length; i++)
            {
                var row = Instantiate(_rowPrefab, _leaderboardRowContainer, false);
                row.Setup(entries[i], _goldMedal, _silverMedal, _bronzeMedal, i, _onAddFriend);
            }

            // If the player is outside the top 15, append their row as #16
            if (!playerInTop && playerEntry.HasValue)
            {
                var overflowRow = Instantiate(_rowPrefab, _leaderboardRowContainer, false);
                overflowRow.Setup(playerEntry.Value, _goldMedal, _silverMedal, _bronzeMedal, entries.Length, _onAddFriend);
            }
        }

        public void HideLeaderboard()
        {
            if (_leaderboardRoot != null)
            {
                _leaderboardRoot.SetActive(false);
            }
        }

        // --- UI INTERACTION ---
        public void ToggleLeaderboard()
        {
            if (_leaderboardRoot == null) return;

            bool active = !_leaderboardRoot.activeSelf;
            _leaderboardRoot.SetActive(active);
            Time.timeScale = active ? 0f : 1f;

            if (active)
            {
                // Animation logic
                var cg = _leaderboardRoot.GetComponent<CanvasGroup>();
                if (cg)
                {
                    cg.alpha = 0f;
                    cg.DOFade(1f, 0.25f).SetUpdate(true);
                }
                _leaderboardRoot.transform.localScale = Vector3.one * 0.9f;
                _leaderboardRoot.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetUpdate(true);

                if (_playerNameInput != null && _getPlayerName != null)
                    _playerNameInput.text = _getPlayerName();

                RefreshLeaderboardScopeUi();
                _ = RefreshLeaderboard(); // Trigger refresh when opening
            }
        }

        public async void OnSetPlayerName()
        {
            if (_playerNameInput == null || _setPlayerName == null) return;
            string name = _playerNameInput.text.Trim();
            if (name.Length < 1 || name.Length > 20) return;

            _leaderboardStatusText.text = "Updating name...";
            _leaderboardStatusText.gameObject.SetActive(true);

            await _setPlayerName(name);
            await Task.Delay(600); // Wait for propagation
            _ = RefreshLeaderboard();
        }

        public void SetLeaderboardScope(LeaderboardScope scope)
        {
            if (_activeScope == scope) return;
            _activeScope = scope;
            RefreshLeaderboardScopeUi();
            _ = RefreshLeaderboard();
        }

        private void RefreshLeaderboardScopeUi()
        {
            if (_leaderboardTitleText != null)
            {
                _leaderboardTitleText.text = _activeScope switch
                {
                    LeaderboardScope.Daily   => "DAILY LEADERBOARD",
                    LeaderboardScope.Weekly  => "WEEKLY LEADERBOARD",
                    _                        => "ALL-TIME LEADERBOARD",
                };
            }

            if (_dailyLeaderboardTabButton != null)
                _dailyLeaderboardTabButton.interactable = _activeScope != LeaderboardScope.Daily;
            if (_weeklyLeaderboardTabButton != null)
                _weeklyLeaderboardTabButton.interactable = _activeScope != LeaderboardScope.Weekly;
            if (_allTimeLeaderboardTabButton != null)
                _allTimeLeaderboardTabButton.interactable = _activeScope != LeaderboardScope.AllTime;
        }
    }

}