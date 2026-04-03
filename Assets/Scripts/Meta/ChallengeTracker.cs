using UnityEngine;

namespace StackSurge.Meta
{
    public class ChallengeTracker
    {
        readonly ChallengeDefinition[] _defs;

        public ChallengeTracker(ChallengeDefinition[] defs) => _defs = defs ?? System.Array.Empty<ChallengeDefinition>();

        public void TickRun(float timeAlive, int score, int bestComboMult, SaveData save)
        {
            if (_defs.Length == 0) return;
            EnsureBits(save, _defs.Length);

            for (int i = 0; i < _defs.Length; i++)
            {
                if (save.ChallengeBits[i] != 0) continue;
                var d = _defs[i];
                if (d == null) continue;
                bool done = d.Type switch
                {
                    ChallengeType.ScoreInRun => score >= d.TargetValue,
                    ChallengeType.SurviveSeconds => timeAlive >= d.TargetValue,
                    ChallengeType.MaxComboMultiplier => bestComboMult >= d.TargetValue,
                    _ => false
                };
                if (done) save.ChallengeBits[i] = 1;
            }
        }

        static void EnsureBits(SaveData save, int len)
        {
            if (save.ChallengeBits == null || save.ChallengeBits.Length < len)
            {
                var n = new int[len];
                if (save.ChallengeBits != null)
                    System.Array.Copy(save.ChallengeBits, n, save.ChallengeBits.Length);
                save.ChallengeBits = n;
            }
        }
    }
}
