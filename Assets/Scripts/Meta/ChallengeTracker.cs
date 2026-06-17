using UnityEngine;

namespace StackSurge.Meta
{
    /// <summary>
    /// Evaluates per-run metrics against active challenge definitions
    /// and records granular progress through the ChallengeProvider.
    /// </summary>
    public class ChallengeTracker
    {
        readonly ChallengeProvider _provider;

        public ChallengeTracker(ChallengeProvider provider) => _provider = provider;

        /// <summary>
        /// Called each match-resolve wave and at run end to update challenge progress.
        /// </summary>
        public void TickRun(float timeAlive, int score, int bestComboMult)
        {
            if (_provider == null) return;
            var defs = _provider.ActiveChallenges;
            if (defs == null || defs.Length == 0) return;

            for (int i = 0; i < defs.Length; i++)
            {
                if (_provider.CompletionBits[i] != 0) continue;
                var d = defs[i];
                if (d == null) continue;

                int value = d.Type switch
                {
                    ChallengeType.ScoreInRun => score,
                    ChallengeType.SurviveSeconds => (int)timeAlive,
                    ChallengeType.MaxComboMultiplier => bestComboMult,
                    _ => 0
                };

                _provider.RecordProgress(i, value);
            }
        }
    }
}
