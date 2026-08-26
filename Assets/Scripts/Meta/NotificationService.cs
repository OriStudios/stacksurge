using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StackSurge.UI;
using UnityEngine;
using Unity.Services.Analytics;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using OneSignalSDK;

#if UNITY_ANDROID
using Unity.Notifications.Android;
#elif UNITY_IOS
using Unity.Notifications.iOS;
#endif

namespace StackSurge.Meta
{
    public class NotificationService : MonoBehaviour
    {
        private static NotificationService _instance;
        public static NotificationService Instance => _instance;

        public const string LeaderboardChannelId = "leaderboard_updates";
        public const string FriendsChannelId = "friends_activity";
        public const string RemindersChannelId = "game_reminders";

        private SaveData _save;
        private bool _isInitialized = false;

        public static void EnsureInstance(SaveData save)
        {
            if (_instance == null)
            {
                GameObject obj = new GameObject("[NotificationService]");
                _instance = obj.AddComponent<NotificationService>();
                DontDestroyOnLoad(obj);
            }
            _instance.Initialize(save);
        }

        public void Initialize(SaveData save)
        {
            _save = save;
            if (_isInitialized) return;

            SetupNotificationChannels();
            _ = InitOneSignalAndPermissionsAsync();

            _isInitialized = true;
            Debug.Log("[NotificationService] Initialized notification channels and permissions check.");
        }

        private async Task InitOneSignalAndPermissionsAsync()
        {
#if !UNITY_EDITOR
            try
            {
                // 1. Initialize OneSignal SDK first
                string appId = OneSignalPushHelper.AppId;
                if (!string.IsNullOrEmpty(appId))
                {
                    OneSignal.Initialize(appId);
                    Debug.Log($"[NotificationService] OneSignal SDK initialized with App ID: {appId}");

                    // Listen to incoming foreground notifications
                    OneSignal.Notifications.ForegroundWillDisplay += (sender, notificationEvent) =>
                    {
                        var notif = notificationEvent.Notification;
                        Debug.Log($"[OneSignal] Foreground Notification Received: {notif.Title} - {notif.Body}");
                        InAppNotificationView.Show(notif.Title, notif.Body, NotificationType.Reminder);
                    };

                    // Prompt for Push Permission (Android 13+ / iOS)
                    OneSignal.Notifications.RequestPermissionAsync(true);
                }

                // 2. Start Analytics collection
                AnalyticsService.Instance.StartDataCollection();

                // 3. Bind PlayerId to OneSignal if signed in
                if (AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn)
                {
                    string playerId = AuthenticationService.Instance.PlayerId;
                    BindUserToPushService(playerId);
                }

                AnalyticsService.Instance.Flush();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[NotificationService] OneSignal setup warning: {ex.Message}");
            }
#else
            Debug.Log("[NotificationService] Skipping push token registration — not supported in Unity Editor. Run on a real device.");
#endif
            await Task.CompletedTask;
        }

        /// <summary>
        /// Binds the authenticated UGS PlayerId to OneSignal Remote Push Service.
        /// </summary>
        public void BindUserToPushService(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return;

            try
            {
#if !UNITY_EDITOR
                OneSignal.Login(playerId);
#endif
                Debug.Log($"[NotificationService] Bound UGS PlayerId '{playerId}' to OneSignal Remote Push.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NotificationService] OneSignal.Login warning: {ex.Message}");
            }

            if (_save != null)
            {
                _save.DevicePushToken = playerId;
                LocalProgress.Save(_save);
            }
        }

        private void OnRemotePushReceived(string title, string body)
        {
            Debug.Log($"[NotificationService] Remote push received — {title}: {body}");
            InAppNotificationView.Show(title, body, NotificationType.Reminder);
        }

        private void SetupNotificationChannels()
        {
#if UNITY_ANDROID
            // Fallback / Default Channels for Unity Dashboard campaigns
            var defaultChannel = new AndroidNotificationChannel()
            {
                Id = "default",
                Name = "General Notifications",
                Importance = Importance.High,
                Description = "General game updates and push notifications",
            };
            AndroidNotificationCenter.RegisterNotificationChannel(defaultChannel);

            // Leaderboard Drop Channel
            var leaderboardChannel = new AndroidNotificationChannel()
            {
                Id = LeaderboardChannelId,
                Name = "Leaderboard Rank Updates",
                Importance = Importance.High,
                Description = "Notifications when your position on the leaderboard drops",
            };
            AndroidNotificationCenter.RegisterNotificationChannel(leaderboardChannel);

            // Friends Activity Channel
            var friendsChannel = new AndroidNotificationChannel()
            {
                Id = FriendsChannelId,
                Name = "Friend Activity",
                Importance = Importance.High,
                Description = "Notifications for new friend requests and social updates",
            };
            AndroidNotificationCenter.RegisterNotificationChannel(friendsChannel);

            // Reminders Channel
            var remindersChannel = new AndroidNotificationChannel()
            {
                Id = RemindersChannelId,
                Name = "Game Reminders & Streaks",
                Importance = Importance.Default,
                Description = "Streak protection reminders and leaderboard reset countdowns",
            };
            AndroidNotificationCenter.RegisterNotificationChannel(remindersChannel);
#endif
        }

        public async Task RequestPermissionsAsync()
        {
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
            try
            {
                OneSignal.Notifications.RequestPermissionAsync(true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NotificationService] OneSignal permission request warning: {ex.Message}");
            }
#endif

#if UNITY_ANDROID
            if (UnityEngine.Android.Permission.HasUserAuthorizedPermission("android.permission.POST_NOTIFICATIONS") == false)
            {
                UnityEngine.Android.Permission.RequestUserPermission("android.permission.POST_NOTIFICATIONS");
            }
#elif UNITY_IOS
            using (var req = new AuthorizationRequest(AuthorizationOption.Alert | AuthorizationOption.Badge | AuthorizationOption.Sound, true))
            {
                while (!req.IsFinished)
                {
                    await Task.Yield();
                }
                if (req.Granted && !string.IsNullOrEmpty(req.DeviceToken))
                {
                    if (_save != null)
                    {
                        _save.DevicePushToken = req.DeviceToken;
                        LocalProgress.Save(_save);
                    }
                }
            }
#endif
            await Task.CompletedTask;
        }

        // ── Leaderboard Rank Drop Detection ─────────────────────────────────
        /// <summary>
        /// Checks player's new rank against the saved rank for that specific scope.
        /// Only fires if the rank genuinely worsened since last time we saw it.
        /// </summary>
        public void CheckAndNotifyRankDrop(int currentRank, LeaderboardScope scope, string leaderboardName = "Leaderboard")
        {
            if (_save == null || !_save.NotifyLeaderboardDrops || currentRank <= 0) return;

            int prevRank = GetSavedRank(scope);

            // First time we've seen a rank for this scope — just record it
            if (prevRank <= 0)
            {
                SetSavedRank(scope, currentRank);
                LocalProgress.Save(_save);
                return;
            }

            // Rank improved or stayed the same — update silently
            if (currentRank <= prevRank)
            {
                SetSavedRank(scope, currentRank);
                LocalProgress.Save(_save);
                return;
            }

            // Rank dropped — notify
            int placesDropped = currentRank - prevRank;
            string title = "📉 Leaderboard Rank Dropped";
            string message = $"You dropped {placesDropped} place{(placesDropped > 1 ? "s" : "")} — now #{currentRank} on {leaderboardName}. Play to reclaim your spot!";

            SetSavedRank(scope, currentRank);
            LocalProgress.Save(_save);

            InAppNotificationView.Show(title, message, NotificationType.LeaderboardDrop);
        }

        private int GetSavedRank(LeaderboardScope scope)
        {
            return scope switch
            {
                LeaderboardScope.Daily => _save.LastKnownRankDaily,
                LeaderboardScope.Weekly => _save.LastKnownRankWeekly,
                _ => _save.LastKnownRankAllTime,
            };
        }

        private void SetSavedRank(LeaderboardScope scope, int rank)
        {
            switch (scope)
            {
                case LeaderboardScope.Daily: _save.LastKnownRankDaily = rank; break;
                case LeaderboardScope.Weekly: _save.LastKnownRankWeekly = rank; break;
                default: _save.LastKnownRankAllTime = rank; break;
            }
        }


        // ── Friend Activity Events ─────────────────────────────────────────

        /// <summary>
        /// Shows an in-app toast to the SENDER that their request was sent,
        /// and triggers a Cloud Code push to the RECIPIENT's device via OneSignal.
        /// </summary>
        public void NotifyFriendRequestReceived(string senderName, string targetPlayerId = null)
        {
            if (_save != null && !_save.NotifyFriendActivity) return;

            // In-app confirmation for the sender (currently open)
            InAppNotificationView.Show("Friend Request Sent!",
                $"Your friend request to {targetPlayerId ?? "that player"} has been sent.",
                NotificationType.FriendActivity);

            // Push to the RECIPIENT via Cloud Code (secure – API key never leaves the server)
            if (!string.IsNullOrEmpty(targetPlayerId))
            {
                _ = CallCloudCodePushAsync("NotifyFriendRequest", new Dictionary<string, object>
                {
                    { "recipientPlayerId",  targetPlayerId },
                    { "senderDisplayName", senderName }
                });
            }
        }

        /// <summary>
        /// Shows an in-app toast and triggers a Cloud Code push to the ORIGINAL SENDER
        /// letting them know their request was accepted.
        /// </summary>
        public void NotifyFriendRequestAccepted(string friendName, string originalSenderPlayerId = null)
        {
            if (_save != null && !_save.NotifyFriendActivity) return;

            // In-app toast for the person who just accepted
            InAppNotificationView.Show("Friend Request Accepted!",
                $"You and {friendName} are now friends! Compete on the Friends Leaderboard.",
                NotificationType.FriendActivity);

            // Push to the ORIGINAL SENDER via Cloud Code
            if (!string.IsNullOrEmpty(originalSenderPlayerId))
            {
                string myName = _save?.PlayerDisplayName ?? "A StackSurge Player";
                _ = CallCloudCodePushAsync("NotifyFriendRequestAccepted", new Dictionary<string, object>
                {
                    { "originalSenderPlayerId", originalSenderPlayerId },
                    { "acceptorDisplayName",    myName }
                });
            }
        }

        /// <summary>
        /// Shows an in-app toast and triggers a Cloud Code push to the FRIEND whose score was beaten.
        /// </summary>
        public void NotifyFriendBeatScore(string friendName, int newScore, string beatenFriendPlayerId = null, string scope = null)
        {
            if (_save != null && !_save.NotifyFriendActivity) return;

            // In-app toast for the local player who just set the high score
            InAppNotificationView.Show("New High Score!",
                $"You beat {friendName}'s score with {newScore} points!",
                NotificationType.FriendActivity);

            // Push to the BEATEN FRIEND via Cloud Code
            if (!string.IsNullOrEmpty(beatenFriendPlayerId))
            {
                string myName = _save?.PlayerDisplayName ?? "A StackSurge Player";
                _ = CallCloudCodePushAsync("NotifyFriendBeatScore", new Dictionary<string, object>
                {
                    { "beatenFriendPlayerId", beatenFriendPlayerId },
                    { "scorerDisplayName",    myName },
                    { "newScore",             newScore },
                    { "leaderboardScope",     scope ?? "AllTime" }
                });
            }
        }

        /// <summary>
        /// Calls a UGS Cloud Code Script endpoint and passes parameters.
        /// The Cloud Code script holds the OneSignal REST API key securely on the server.
        /// </summary>
        private async Task CallCloudCodePushAsync(string scriptName, Dictionary<string, object> args)
        {
            try
            {
                await CloudCodeService.Instance.CallEndpointAsync(scriptName, args);
                Debug.Log($"[NotificationService] Cloud Code push dispatched: {scriptName}");
            }
            catch (Exception ex)
            {
                // Non-fatal: in-app toast already shown, remote push is best-effort
                Debug.LogWarning($"[NotificationService] Cloud Code push '{scriptName}' failed: {ex.Message}");
            }
        }

        // Fixed notification IDs for recurring reminders (prevents duplicate stacking)
        public const int StreakReminderNotificationId = 1001;
        public const int LeaderboardResetNotificationId = 1002;
        public const int InactivityReminderNotificationId = 1003;

        // ── Scheduled Reminders ─────────────────────────────────────────────
        public void CancelAllScheduledNotifications()
        {
#if UNITY_ANDROID
            AndroidNotificationCenter.CancelAllScheduledNotifications();
#elif UNITY_IOS
            iOSNotificationCenter.RemoveAllScheduledNotifications();
#endif
        }

        public void ScheduleStreakProtectionReminder()
        {
            if (_save != null && !_save.NotifyReminders) return;

            // Schedule notification for 8:00 PM today if player hasn't completed run
            DateTime targetTime = DateTime.Today.AddHours(20);
            if (DateTime.Now >= targetTime)
            {
                targetTime = targetTime.AddDays(1);
            }

            ScheduleLocalNotification(
                "Protect Your Daily Streak! 🔥",
                $"You have a {_save.Streak}-day streak! Play StackSurge today to keep your streak multiplier active.",
                targetTime,
                RemindersChannelId,
                StreakReminderNotificationId,
                "streak_reminder"
            );
        }

        public void ScheduleLeaderboardResetReminder()
        {
            if (_save != null && !_save.NotifyReminders) return;

            // Schedule notification 2 hours before midnight UTC (daily reset)
            DateTime nextResetUtc = DateTime.UtcNow.Date.AddDays(1).AddHours(-2);
            DateTime targetLocal = nextResetUtc.ToLocalTime();

            if (targetLocal > DateTime.Now)
            {
                ScheduleLocalNotification(
                    "Leaderboard Resetting Soon! 🏆",
                    "The Daily Leaderboard resets in 2 hours. Stack high and lock in your top ranking!",
                    targetLocal,
                    RemindersChannelId,
                    LeaderboardResetNotificationId,
                    "leaderboard_reset_reminder"
                );
            }
        }

        public void ScheduleInactivityReminder()
        {
            if (_save != null && !_save.NotifyReminders) return;

            // 3 days inactivity reminder
            ScheduleLocalNotification(
                "We Miss You in StackSurge! 🧱",
                "Your high score is waiting! Jump back in and see if you can break your personal record.",
                DateTime.Now.AddDays(3),
                RemindersChannelId,
                InactivityReminderNotificationId,
                "inactivity_reminder"
            );
        }

        // ── Low-Level Local Notification Dispatcher ─────────────────────────
        public void ScheduleLocalNotification(
            string title, 
            string body, 
            DateTime fireTime, 
            string channelId, 
            int notificationId = -1, 
            string iosIdentifier = null)
        {
#if UNITY_ANDROID
            var androidNotif = new AndroidNotification
            {
                Title = title,
                Text = body,
                FireTime = fireTime,
                SmallIcon = "icon_small",
                LargeIcon = "icon_large"
            };

            if (notificationId > 0)
            {
                AndroidNotificationCenter.SendNotificationWithExplicitID(androidNotif, channelId, notificationId);
            }
            else
            {
                AndroidNotificationCenter.SendNotification(androidNotif, channelId);
            }
#elif UNITY_IOS
            var timeTrigger = new iOSNotificationCalendarTrigger
            {
                Year = fireTime.Year,
                Month = fireTime.Month,
                Day = fireTime.Day,
                Hour = fireTime.Hour,
                Minute = fireTime.Minute,
                Second = fireTime.Second
            };

            var iosNotif = new iOSNotification
            {
                Identifier = string.IsNullOrEmpty(iosIdentifier) ? Guid.NewGuid().ToString() : iosIdentifier,
                Title = title,
                Body = body,
                ShowInForeground = true,
                ForegroundPresentationOption = PresentationOption.Alert | PresentationOption.Sound,
                CategoryIdentifier = channelId,
                Trigger = timeTrigger
            };
            iOSNotificationCenter.ScheduleNotification(iosNotif);
#else
            Debug.Log($"[NotificationService Local Simulation] '{title}' - '{body}' scheduled for {fireTime}");
#endif
        }
    }
}
