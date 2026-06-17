using System;
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
        public string Id;
        public string Title;
        [TextArea] public string Description;
        public ChallengeType Type;
        public int TargetValue;
        public int RewardPoints = 500;
        public string Category = "daily";

        /// <summary>
        /// Creates a runtime ChallengeDefinition from a serialisable data object
        /// (used when hydrating from Remote Config JSON).
        /// </summary>
        public static ChallengeDefinition FromData(ChallengeData data)
        {
            var inst = CreateInstance<ChallengeDefinition>();
            inst.Id = data.Id;
            inst.Title = data.Title;
            inst.Description = data.Description;
            inst.Type = Enum.TryParse<ChallengeType>(data.Type, true, out var t) ? t : ChallengeType.ScoreInRun;
            inst.TargetValue = data.TargetValue;
            inst.RewardPoints = data.RewardPoints > 0 ? data.RewardPoints : 500;
            inst.Category = string.IsNullOrEmpty(data.Category) ? "daily" : data.Category;
            return inst;
        }
    }

    /// <summary>
    /// Plain serialisable struct matching the Remote Config JSON shape.
    /// </summary>
    [Serializable]
    public struct ChallengeData
    {
        public string Id;
        public string Title;
        public string Description;
        public string Type;
        public int TargetValue;
        public int RewardPoints;
        public string Category;
    }

    /// <summary>
    /// Wrapper for JSON array deserialisation via JsonUtility.
    /// </summary>
    [Serializable]
    public class ChallengeDataList
    {
        public ChallengeData[] Items;
    }

    /// <summary>
    /// Structured data passed from game logic to the UI for rendering challenge rows.
    /// </summary>
    public struct ChallengeDisplayData
    {
        public string Title;
        public string Description;
        public int Progress;
        public int TargetValue;
        public bool Completed;
        public bool Claimed;
        public int RewardPoints;
    }
}
