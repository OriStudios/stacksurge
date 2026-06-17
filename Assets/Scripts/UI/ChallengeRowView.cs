using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace StackSurge.UI
{
    /// <summary>
    /// Component attached to the challenge row prefab to hold UI references.
    /// </summary>
    public class ChallengeRowView : MonoBehaviour
    {
        [Header("Text Fields")]
        [SerializeField] TextMeshProUGUI _statusText;
        [SerializeField] TextMeshProUGUI _titleText;
        [SerializeField] TextMeshProUGUI _descriptionText;
        [SerializeField] TextMeshProUGUI _progressText;
        [SerializeField] TextMeshProUGUI _rewardText;
        [SerializeField] TextMeshProUGUI _claimButtonText;

        [Header("Interactive & Visuals")]
        [SerializeField] Image _progressBarBg;
        [SerializeField] Image _progressBarFill;
        [SerializeField] Button _claimButton;
        [SerializeField] Image _backgroundImage;

        // Public getters for controller binding
        public TextMeshProUGUI StatusText => _statusText;
        public TextMeshProUGUI TitleText => _titleText;
        public TextMeshProUGUI DescriptionText => _descriptionText;
        public TextMeshProUGUI ProgressText => _progressText;
        public TextMeshProUGUI RewardText => _rewardText;
        public TextMeshProUGUI ClaimButtonText => _claimButtonText;
        public Image ProgressBarBg => _progressBarBg;
        public Image ProgressBarFill => _progressBarFill;
        public Button ClaimButton => _claimButton;
        public Image BackgroundImage => _backgroundImage;
    }
}
