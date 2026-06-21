using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Models;
using UnityEngine;

namespace StackSurge.Meta
{
    public static class ArchiveRestoreService
    {
        /// <summary>
        /// Restores the currently signed-in player's score from an archived leaderboard version.
        /// - If the player has no active score, the archived score is inserted.
        /// - If the player exists in both, only the higher score is kept.
        /// </summary>
        public static async Task RestoreFromArchive(string leaderboardId, string archivedVersionId)
        {
            try
            {
                string currentPlayerId = AuthenticationService.Instance.PlayerId;

                var archivedEntries = await FetchAllArchivedEntries(leaderboardId, archivedVersionId);
                var activeEntries   = await FetchAllActiveEntries(leaderboardId);

                // Find this player's archived entry
                var archivedEntry = archivedEntries.Find(e => e.PlayerId == currentPlayerId);
                if (archivedEntry == null)
                {
                    Debug.Log("[ArchiveRestore] No archived score found for this player.");
                    return;
                }

                // Find this player's active entry if any
                var activeEntry = activeEntries.Find(e => e.PlayerId == currentPlayerId);

                bool hasNoActiveScore = activeEntry == null;
                bool archivedIsHigher = activeEntry != null && archivedEntry.Score > activeEntry.Score;

                if (hasNoActiveScore || archivedIsHigher)
                {
                    await LeaderboardsService.Instance.AddPlayerScoreAsync(
                        leaderboardId, archivedEntry.Score);
                    Debug.Log($"[ArchiveRestore] Restored score {archivedEntry.Score} for player {currentPlayerId}");
                }
                else
                {
                    Debug.Log("[ArchiveRestore] Active score is already higher, skipping.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ArchiveRestore] Failed: {e.Message}");
            }
        }

        private static async Task<List<LeaderboardEntry>> FetchAllActiveEntries(string leaderboardId)
        {
            var all = new List<LeaderboardEntry>();
            int offset = 0;
            const int limit = 100;

            while (true)
            {
                var page = await LeaderboardsService.Instance.GetScoresAsync(
                    leaderboardId,
                    new GetScoresOptions { Offset = offset, Limit = limit });

                if (page.Results == null || page.Results.Count == 0) break;

                all.AddRange(page.Results);

                if (all.Count >= page.Total) break;
                offset += limit;
            }

            return all;
        }

        private static async Task<List<LeaderboardEntry>> FetchAllArchivedEntries(
            string leaderboardId, string versionId)
        {
            var all = new List<LeaderboardEntry>();
            int offset = 0;
            const int limit = 100;

            while (true)
            {
                var page = await LeaderboardsService.Instance.GetVersionScoresAsync(
                    leaderboardId,
                    versionId,
                    new GetVersionScoresOptions { Offset = offset, Limit = limit });

                if (page.Results == null || page.Results.Count == 0) break;

                all.AddRange(page.Results);

                if (all.Count >= page.Total) break;
                offset += limit;
            }

            return all;
        }
    }
}