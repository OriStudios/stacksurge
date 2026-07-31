using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.RemoteConfig;

namespace StackSurge.Meta
{
    /// <summary>
    /// Async service that manages the full challenge lifecycle:
    /// authentication → Remote Config fetch → Cloud Save load/save → offline fallback.
    /// </summary>
    public class ChallengeProvider
    {
        // ── Public state ──────────────────────────────────────────────
        public ChallengeDefinition[] ActiveChallenges { get; private set; } = Array.Empty<ChallengeDefinition>();
        public int[] Progress { get; private set; } = Array.Empty<int>();
        public int[] CompletionBits { get; private set; } = Array.Empty<int>();
        public bool[] Claimed { get; private set; } = Array.Empty<bool>();
        public bool IsOnline { get; private set; }
        public int PendingRewardPoints { get; private set; }

        // ── Private ───────────────────────────────────────────────────
        SaveData _save;
        bool _initialised;

        const string CloudKey_Progress = "challenge_progress";
        const string CloudKey_Bits = "challenge_bits";
        const string CloudKey_Claimed = "challenge_claimed";
        const string CloudKey_SetDate = "challenge_set_date";

        // ── Remote Config fetch attributes (required by the API) ─────
        struct UserAttributes { }
        struct AppAttributes { }

        // ── Initialise ───────────────────────────────────────────────
        public async Task InitializeAsync(SaveData save, ChallengeDefinition[] fallbackAssets)
        {
            _save = save;

            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();

                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                IsOnline = true;
                Debug.Log("[ChallengeProvider] Signed in. PlayerID: " + AuthenticationService.Instance.PlayerId);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ChallengeProvider] Auth failed, falling back to local: " + e.Message);
                IsOnline = false;
            }

            // Fetch challenge definitions
            await FetchDefinitionsAsync(fallbackAssets);

            // Load progress (cloud or local)
            await LoadProgressAsync();

            // Check daily rotation
            CheckDailyRotation();

            _initialised = true;
        }

        // ── Fetch definitions from Remote Config (or use fallback) ───
        async Task FetchDefinitionsAsync(ChallengeDefinition[] fallbackAssets)
        {
            if (IsOnline)
            {
                try
                {
                    RemoteConfigService.Instance.FetchCompleted += ApplyRemoteConfig;
                    await RemoteConfigService.Instance.FetchConfigsAsync(new UserAttributes(), new AppAttributes());
                    RemoteConfigService.Instance.FetchCompleted -= ApplyRemoteConfig;

                    // If remote fetch populated challenges, we're done
                    if (ActiveChallenges.Length > 0) return;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[ChallengeProvider] Remote Config fetch failed: " + e.Message);
                    RemoteConfigService.Instance.FetchCompleted -= ApplyRemoteConfig;
                }
            }

            // Fallback: use inspector assets or hardcoded defaults
            ActiveChallenges = fallbackAssets != null && fallbackAssets.Length > 0
                ? fallbackAssets
                : BuildDefaultChallenges();
        }

        void ApplyRemoteConfig(ConfigResponse response)
        {
            try
            {
                string json = RemoteConfigService.Instance.appConfig.GetJson("daily_challenges", "");
                if (string.IsNullOrEmpty(json))
                {
                    Debug.Log("[ChallengeProvider] No 'daily_challenges' key in Remote Config.");
                    return;
                }

                // JsonUtility needs a wrapper object for arrays
                string wrapped = "{\"Items\":" + json + "}";
                var list = JsonUtility.FromJson<ChallengeDataList>(wrapped);

                if (list?.Items == null || list.Items.Length == 0)
                {
                    Debug.LogWarning("[ChallengeProvider] Remote Config JSON parsed but no challenges found.");
                    return;
                }

                var defs = new ChallengeDefinition[list.Items.Length];
                for (int i = 0; i < list.Items.Length; i++)
                    defs[i] = ChallengeDefinition.FromData(list.Items[i]);

                ActiveChallenges = defs;
                Debug.Log($"[ChallengeProvider] Loaded {defs.Length} challenges from Remote Config.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ChallengeProvider] Failed to parse Remote Config JSON: " + e.Message);
            }
        }

        // ── Load progress from Cloud Save or local ───────────────────
        async Task LoadProgressAsync()
        {
            int count = ActiveChallenges.Length;

            if (IsOnline)
            {
                try
                {
                    var keys = new HashSet<string>
                    {
                        CloudKey_Progress,
                        CloudKey_Bits,
                        CloudKey_Claimed,
                        CloudKey_SetDate
                    };

                    var data = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

                    Progress = DeserializeIntArray(data, CloudKey_Progress, count);
                    CompletionBits = DeserializeIntArray(data, CloudKey_Bits, count);
                    Claimed = DeserializeBoolArray(data, CloudKey_Bits, CloudKey_Claimed, count);

                    if (data.TryGetValue(CloudKey_SetDate, out var dateItem))
                        _save.ChallengeSetDate = dateItem.Value.GetAs<string>() ?? "";

                    // Sync to local save as backup
                    _save.ChallengeProgress = (int[])Progress.Clone();
                    _save.ChallengeBits = (int[])CompletionBits.Clone();
                    LocalProgress.Save(_save);

                    Debug.Log("[ChallengeProvider] Progress loaded from Cloud Save.");
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[ChallengeProvider] Cloud Save load failed, using local: " + e.Message);
                }
            }

            // Fallback to local
            EnsureArrays(count);
            Progress = EnsureLength(_save.ChallengeProgress, count);
            CompletionBits = EnsureLength(_save.ChallengeBits, count);
            Claimed = new bool[count];
            PendingRewardPoints = _save.PendingRewardPoints;
        }

        // ── Daily rotation check ─────────────────────────────────────
        void CheckDailyRotation()
        {
            string today = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
            if (_save.ChallengeSetDate != today)
            {
                Debug.Log($"[ChallengeProvider] Daily rotation: {_save.ChallengeSetDate} → {today}");
                int count = ActiveChallenges.Length;
                Progress = new int[count];
                CompletionBits = new int[count];
                Claimed = new bool[count];

                _save.ChallengeSetDate = today;
                _save.ChallengeProgress = new int[count];
                _save.ChallengeBits = new int[count];
                LocalProgress.Save(_save);
            }
        }

        // ── Record progress for a specific challenge ─────────────────
        public void RecordProgress(int index, int value)
        {
            if (index < 0 || index >= ActiveChallenges.Length) return;
            if (CompletionBits[index] != 0) return; // already complete

            Progress[index] = Mathf.Max(Progress[index], value);

            if (Progress[index] >= ActiveChallenges[index].TargetValue)
            {
                CompletionBits[index] = 1;
            }
        }

        // ── Claim reward ─────────────────────────────────────────────
        public bool ClaimReward(int index)
        {
            if (index < 0 || index >= ActiveChallenges.Length) return false;
            if (CompletionBits[index] == 0 || Claimed[index]) return false;

            Claimed[index] = true;
            PendingRewardPoints += ActiveChallenges[index].RewardPoints;
            _save.PendingRewardPoints = PendingRewardPoints;
            LocalProgress.Save(_save);
            return true;
        }

        /// <summary>Consume pending reward points (call at run start).</summary>
        public int ConsumePendingRewards()
        {
            int pts = PendingRewardPoints;
            PendingRewardPoints = 0;
            _save.PendingRewardPoints = 0;
            LocalProgress.Save(_save);
            return pts;
        }

        // ── Save progress to Cloud Save + local ──────────────────────
        public async Task SaveAsync()
        {
            // Always persist locally
            _save.ChallengeProgress = (int[])Progress.Clone();
            _save.ChallengeBits = (int[])CompletionBits.Clone();
            LocalProgress.Save(_save);

            if (!IsOnline) return;

            try
            {
                var data = new Dictionary<string, object>
                {
                    { CloudKey_Progress, JsonUtility.ToJson(new IntArrayWrapper { Values = Progress }) },
                    { CloudKey_Bits, JsonUtility.ToJson(new IntArrayWrapper { Values = CompletionBits }) },
                    { CloudKey_Claimed, JsonUtility.ToJson(new BoolArrayWrapper { Values = Claimed }) },
                    { CloudKey_SetDate, _save.ChallengeSetDate }
                };

                await CloudSaveService.Instance.Data.Player.SaveAsync(data);
                Debug.Log("[ChallengeProvider] Progress saved to Cloud Save.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ChallengeProvider] Cloud Save write failed: " + e.Message);
            }
        }

        // ── Get display data for the UI ──────────────────────────────
        public ChallengeDisplayData[] GetDisplayData()
        {
            var result = new ChallengeDisplayData[ActiveChallenges.Length];
            for (int i = 0; i < ActiveChallenges.Length; i++)
            {
                var def = ActiveChallenges[i];
                result[i] = new ChallengeDisplayData
                {
                    Title = def.Title,
                    Description = def.Description,
                    Progress = Mathf.Min(Progress[i], def.TargetValue),
                    TargetValue = def.TargetValue,
                    Completed = CompletionBits[i] != 0,
                    Claimed = Claimed[i],
                    RewardPoints = def.RewardPoints
                };
            }
            return result;
        }

        /// <summary>
        /// Returns the time remaining until the next daily rotation (midnight UTC).
        /// </summary>
        public TimeSpan GetTimeUntilReset()
        {
            var now = DateTime.UtcNow;
            var midnight = now.Date.AddDays(1);
            return midnight - now;
        }

        // ── Default challenges (offline fallback) ────────────────────
        static ChallengeDefinition[] BuildDefaultChallenges()
        {
            ChallengeDefinition C(string id, string title, string desc, ChallengeType ty, int target, int reward)
            {
                var c = ScriptableObject.CreateInstance<ChallengeDefinition>();
                c.Id = id;
                c.Title = title;
                c.Description = desc;
                c.Type = ty;
                c.TargetValue = target;
                c.RewardPoints = reward;
                c.Category = "daily";
                return c;
            }

            return new[]
            {
                C("score_1000", "Score 1000", "Reach 1,000 points in one run.", ChallengeType.ScoreInRun, 1000, 500),
                C("survive_180", "Survivalist", "Stay alive for 3 minutes.", ChallengeType.SurviveSeconds, 180, 500),
                C("combo_5", "Combo Hunter", "Reach a x5 combo multiplier.", ChallengeType.MaxComboMultiplier, 5, 500)
            };
        }

        // ── Helpers ──────────────────────────────────────────────────
        void EnsureArrays(int count)
        {
            if (_save.ChallengeProgress == null || _save.ChallengeProgress.Length < count)
                _save.ChallengeProgress = new int[count];
            if (_save.ChallengeBits == null || _save.ChallengeBits.Length < count)
                _save.ChallengeBits = new int[count];
        }

        static int[] EnsureLength(int[] arr, int len)
        {
            if (arr != null && arr.Length >= len) return arr;
            var n = new int[len];
            if (arr != null) Array.Copy(arr, n, Math.Min(arr.Length, len));
            return n;
        }

        static int[] DeserializeIntArray(Dictionary<string, Unity.Services.CloudSave.Models.Item> data, string key, int count)
        {
            if (data.TryGetValue(key, out var item))
            {
                try
                {
                    var wrapper = JsonUtility.FromJson<IntArrayWrapper>(item.Value.GetAs<string>());
                    if (wrapper?.Values != null && wrapper.Values.Length >= count)
                        return wrapper.Values;
                }
                catch { }
            }
            return new int[count];
        }

        static bool[] DeserializeBoolArray(Dictionary<string, Unity.Services.CloudSave.Models.Item> data, string bitsKey, string key, int count)
        {
            if (data.TryGetValue(key, out var item))
            {
                try
                {
                    var wrapper = JsonUtility.FromJson<BoolArrayWrapper>(item.Value.GetAs<string>());
                    if (wrapper?.Values != null && wrapper.Values.Length >= count)
                        return wrapper.Values;
                }
                catch { }
            }
            return new bool[count];
        }

        [Serializable]
        class IntArrayWrapper { public int[] Values; }

        [Serializable]
        class BoolArrayWrapper { public bool[] Values; }
    }
}
