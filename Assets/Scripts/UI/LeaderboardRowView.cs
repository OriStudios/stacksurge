using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace StackSurge.UI
{
    /// <summary>
    /// Component attached to a leaderboard row prefab to hold UI references.
    /// Can also be left unused when rows are built fully programmatically in GameView.
    /// </summary>
    public class LeaderboardRowView : MonoBehaviour
    {
        [Header("Text Fields")]
        [SerializeField] TextMeshProUGUI _rankText;
        [SerializeField] TextMeshProUGUI _nameText;
        [SerializeField] TextMeshProUGUI _scoreText;

        [Header("Visuals")]
        [SerializeField] Image _backgroundImage;

        public TextMeshProUGUI RankText        => _rankText;
        public TextMeshProUGUI NameText        => _nameText;
        public TextMeshProUGUI ScoreText       => _scoreText;
        public Image           BackgroundImage => _backgroundImage;
    }
}
