using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Models;
using UnityEngine;

namespace StackSurge.Meta
{
    /// <summary>
    /// Data returned per leaderboard row to the UI layer.
    /// Named *Data to avoid collision with the SDK's own LeaderboardEntry model.
    /// </summary>
    public struct LeaderboardEntryData
    {
        public int    Rank;
        public string PlayerId;
        public string PlayerName;
        public double Score;
        public bool   IsCurrentPlayer;
    }

    /// <summary>
    /// Thin async wrapper around Unity Gaming Services Leaderboards.
    /// Leaderboard ID: all_time_highs — Overall Score, highest-to-lowest, best score, weekly reset.
    ///
    /// Offline behaviour: scores are saved locally and submitted automatically the next time
    /// the service is online (either at startup or after reconnecting).
    /// </summary>
    public class LeaderboardService
    {
        const string LeaderboardId = "all_time_highs";
        const int    TopCount      = 10;

        readonly bool     _isOnline;
        readonly SaveData _save;

        public LeaderboardService(bool isOnline, SaveData save)
        {
            _isOnline = isOnline;
            _save     = save;

            // Flush any score that was queued while the player was offline
            if (_isOnline && _save != null && _save.PendingLeaderboardScore >= 0)
            {
                _ = FlushPendingScoreAsync();
            }
        }

        // ── Submit ──────────────────────────────────────────────────────────
        /// <summary>
        /// Submits the player's score. If offline the score is saved locally and will be
        /// flushed automatically on the next online session.
        /// The dashboard "Best score" update type means UGS only stores the value if it is higher.
        /// </summary>
        public async Task SubmitScoreAsync(int score)
        {
            if (!_isOnline)
            {
                // Queue the best score locally for later
                if (_save != null && score > _save.PendingLeaderboardScore)
                {
                    _save.PendingLeaderboardScore = score;
                    LocalProgress.Save(_save);
                    Debug.Log($"[LeaderboardService] Offline — score {score} queued for later upload.");
                }
                return;
            }

            await SubmitToCloudAsync(score);
        }

        // ── Flush pending offline score ─────────────────────────────────────
        private async Task FlushPendingScoreAsync()
        {
            if (_save == null || _save.PendingLeaderboardScore < 0) return;

            int pending = _save.PendingLeaderboardScore;
            Debug.Log($"[LeaderboardService] Flushing offline-queued score: {pending}");

            await SubmitToCloudAsync(pending);

            // Clear the queue only on success (SubmitToCloudAsync swallows its own exceptions)
            _save.PendingLeaderboardScore = -1;
            LocalProgress.Save(_save);
        }

        // ── Internal submit ─────────────────────────────────────────────────
        private async Task SubmitToCloudAsync(int score)
        {
            try
            {
                await LeaderboardsService.Instance.AddPlayerScoreAsync(LeaderboardId, score);
                Debug.Log($"[LeaderboardService] Score {score} submitted to '{LeaderboardId}'.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LeaderboardService] Submit failed: " + e.Message);
            }
        }

        // ── Fetch top scores ────────────────────────────────────────────────
        public async Task<LeaderboardEntryData[]> GetTopScoresAsync()
        {
            if (!_isOnline) return Array.Empty<LeaderboardEntryData>();
            try
            {
                var options = new GetScoresOptions { Limit = TopCount, Offset = 0 };
                var page    = await LeaderboardsService.Instance.GetScoresAsync(LeaderboardId, options);

                string currentId = AuthenticationService.Instance.PlayerId;
                var    entries   = new LeaderboardEntryData[page.Results.Count];
                Debug.Log($"[LeaderboardService] Fetched {page.Results.Count} scores from '{LeaderboardId}'.");

                for (int i = 0; i < page.Results.Count; i++)
                {
                    LeaderboardEntry r = page.Results[i];
                    entries[i] = new LeaderboardEntryData
                    {
                        Rank            = r.Rank + 1, // SDK ranks are 0-indexed
                        PlayerId        = r.PlayerId,
                        PlayerName      = string.IsNullOrEmpty(r.PlayerName)
                                              ? FormatAnonymousId(r.PlayerId)
                                              : r.PlayerName,
                        Score           = r.Score,
                        IsCurrentPlayer = r.PlayerId == currentId
                    };
                }
                return entries;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LeaderboardService] Fetch failed: " + e.Message);
                return Array.Empty<LeaderboardEntryData>();
            }
        }

        // ── Set display name ────────────────────────────────────────────────
        /// <summary>
        /// Updates the player name on UGS Authentication. The leaderboard will
        /// reflect this name on subsequent score submissions and fetches.
        /// </summary>
        public async Task SetPlayerNameAsync(string name)
        {
            if (!_isOnline) return;
            try
            {
                await AuthenticationService.Instance.UpdatePlayerNameAsync(name);
                Debug.Log("[LeaderboardService] Player name updated to: " + name);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LeaderboardService] Name update failed: " + e.Message);
            }
        }

        // ── Helpers ─────────────────────────────────────────────────────────
        static string FormatAnonymousId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "Player";
            return "Player #" + (id.Length >= 6 ? id.Substring(id.Length - 6).ToUpper() : id.ToUpper());
        }
    }
}
