using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using StackSurge.Meta;

namespace StackSurge.UI
{
    /// Component attached to a leaderboard row prefab to hold UI references.
    public class LeaderboardRowView : MonoBehaviour
    {
        [SerializeField] private Image _rankIcon;
        [SerializeField] private TextMeshProUGUI _rankText;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _scoreText;
        [SerializeField] private Image _background;

        public void Setup(LeaderboardEntryData entry, Sprite gold, Sprite silver, Sprite bronze, int delayIndex)
        {
            bool isPlayer = entry.IsCurrentPlayer;

            // Visual State
            _background.color = isPlayer ? new Color(1f, 0.85f, 0.3f, 0.18f) : new Color(0.1f, 0.1f, 0.13f, 0.7f);

            // Rank Display (Icon or Text)
            if (entry.Rank <= 3)
            {
                _rankIcon.gameObject.SetActive(true);
                _rankText.gameObject.SetActive(false);
                _rankIcon.sprite = entry.Rank switch { 1 => gold, 2 => silver, 3 => bronze, _ => null };
            }
            else
            {
                _rankIcon.gameObject.SetActive(false);
                _rankText.gameObject.SetActive(true);
                _rankText.text = $"#{entry.Rank}";
            }

            // Name and Score
            _nameText.text = entry.PlayerName + (isPlayer ? " <color=#FFD700><size=70%>(You)</size></color>" : "");
            _nameText.fontStyle = isPlayer ? FontStyles.Bold : FontStyles.Normal;
            _scoreText.text = ((int)entry.Score).ToString("N0", CultureInfo.InvariantCulture);

            // Staggered Animation
            transform.localScale = Vector3.one * 0.88f;
            transform.DOScale(1f, 0.28f).SetEase(Ease.OutBack).SetDelay(delayIndex * 0.055f).SetUpdate(true);
        }
    }
}
