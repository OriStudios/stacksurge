using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using StackSurge.Meta;

namespace StackSurge.UI
{
    /// <summary>
    /// Component attached to a leaderboard row prefab to hold UI references.
    /// Supports adding friends directly from leaderboard entries.
    /// </summary>
    public class LeaderboardRowView : MonoBehaviour
    {
        [SerializeField] private Image _rankIcon;
        [SerializeField] private TextMeshProUGUI _rankText;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _scoreText;
        [SerializeField] private Image _background;
        [SerializeField] private Button _addFriendButton;

        public void Setup(
            LeaderboardEntryData entry,
            Sprite gold,
            Sprite silver,
            Sprite bronze,
            int delayIndex,
            Action<string> onAddFriend = null)
        {
            bool isPlayer = entry.IsCurrentPlayer;

            // Visual State
            if (_background != null)
            {
                _background.color = isPlayer ? new Color(1f, 0.85f, 0.3f, 0.18f) : new Color(0.1f, 0.1f, 0.13f, 0.7f);
            }

            // Rank Display (Icon or Text)
            if (entry.Rank <= 3)
            {
                if (_rankIcon != null) { _rankIcon.gameObject.SetActive(true); _rankIcon.sprite = entry.Rank switch { 1 => gold, 2 => silver, 3 => bronze, _ => null }; }
                if (_rankText != null)   _rankText.gameObject.SetActive(false);
            }
            else
            {
                if (_rankIcon != null) _rankIcon.gameObject.SetActive(false);
                if (_rankText != null) { _rankText.gameObject.SetActive(true); _rankText.text = $"{entry.Rank}"; }
            }

            // Name and Score
            if (_nameText != null)
            {
                _nameText.text = entry.PlayerName + (isPlayer ? " <color=#FFD700><size=70%>(You)</size></color>" : "");
                _nameText.fontStyle = isPlayer ? FontStyles.Bold : FontStyles.Normal;
            }

            if (_scoreText != null)
            {
                _scoreText.text = ((int)entry.Score).ToString("N0", CultureInfo.InvariantCulture);
            }

            // Add Friend Button
            if (!isPlayer && onAddFriend != null && _addFriendButton != null)
            {
                _addFriendButton.gameObject.SetActive(true);
                _addFriendButton.onClick.RemoveAllListeners();
                _addFriendButton.onClick.AddListener(() =>
                {
                    onAddFriend.Invoke(entry.PlayerId);
                    SetButtonAddedState(_addFriendButton);
                });
            }
            else if (_addFriendButton != null)
            {
                _addFriendButton.gameObject.SetActive(false);
            }

            // Staggered Animation
            transform.localScale = Vector3.one * 0.88f;
            transform.DOScale(1f, 0.28f).SetEase(Ease.OutBack).SetDelay(delayIndex * 0.055f).SetUpdate(true);
        }

        private void SetButtonAddedState(Button btn)
        {
            if (btn == null) return;
            Image img = btn.GetComponent<Image>();
            if (img != null) img.color = new Color(0.35f, 0.35f, 0.4f);

            TextMeshProUGUI txt = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null) txt.text = "Sent!";
            btn.interactable = false;
        }
    }
}
