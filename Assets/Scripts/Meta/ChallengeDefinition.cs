using UnityEngine;

namespace StackSurge.Meta
{
    public enum ChallengeType
    {
        ScoreInRun,
        SurviveSeconds,
        MaxComboMultiplier
    }

    [CreateAssetMenu(fileName = "Challenge", menuName = "Stack Surge/Challenge")]
    public class ChallengeDefinition : ScriptableObject
    {
        public string Title;
        [TextArea] public string Description;
        public ChallengeType Type;
        public int TargetValue;
    }
}
