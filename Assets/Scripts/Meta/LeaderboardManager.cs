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

        [SerializeField] private Button _leaderboardButton;

        [Header("Assets")]
        [SerializeField] private LeaderboardRowView _rowPrefab;
        [SerializeField] private Sprite _goldMedal, _silverMedal, _bronzeMedal;

        private Func<Task<LeaderboardEntryData[]>> _getLeaderboardScores;
        private Func<string, Task> _setPlayerName;
        private Func<string> _getPlayerName;

        [SerializeField] private Button _setPlayerNameButton;
        [SerializeField] private Button _closeButton;

        void Awake()
        {
            if (_leaderboardButton != null)
                _leaderboardButton.onClick.AddListener(ToggleLeaderboard);

            if (_setPlayerNameButton != null)
                _setPlayerNameButton.onClick.AddListener(OnSetPlayerName);

            if (_closeButton != null)
                _closeButton.onClick.AddListener(ToggleLeaderboard);
        }

        // --- INITIALIZATION ---
        public void SetLeaderboardCallbacks(
            Func<Task<LeaderboardEntryData[]>> getLeaderboardScores,
            Func<string, Task> setPlayerName,
            Func<string> getPlayerName)
        {
            _getLeaderboardScores = getLeaderboardScores;
            _setPlayerName = setPlayerName;
            _getPlayerName = getPlayerName;

            if (_playerNameInput != null)
                _playerNameInput.text = _getPlayerName?.Invoke() ?? "";
        }

        // --- REFRESH LOGIC ---
        public async Task RefreshLeaderboard()
        {
            if (_leaderboardRowContainer == null) return;

            // Clear existing rows
            foreach (Transform child in _leaderboardRowContainer)
                Destroy(child.gameObject);

            _leaderboardStatusText.text = "Loading scores...";
            _leaderboardStatusText.gameObject.SetActive(true);

            LeaderboardEntryData[] entries = Array.Empty<LeaderboardEntryData>();
            if (_getLeaderboardScores != null)
            {
                try { entries = await _getLeaderboardScores(); }
                catch { entries = Array.Empty<LeaderboardEntryData>(); }
            }

            _leaderboardStatusText.gameObject.SetActive(false);

            if (entries.Length == 0)
            {
                _leaderboardStatusText.gameObject.SetActive(true);
                _leaderboardStatusText.text = "Could not load scores.";
                return;
            }

            // Build new rows
            for (int i = 0; i < entries.Length; i++)
            {
                var row = Instantiate(_rowPrefab, _leaderboardRowContainer, false);
                row.Setup(entries[i], _goldMedal, _silverMedal, _bronzeMedal, i);
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
    }

}