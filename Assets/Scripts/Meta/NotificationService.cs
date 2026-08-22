using System;
using System.Threading.Tasks;
using StackSurge.UI;
using UnityEngine;
using Unity.Services.PushNotifications;

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
            _ = RequestPermissionsAsync();
            _ = RegisterPushTokenAsync();

            _isInitialized = true;
            Debug.Log("[NotificationService] Initialized notification channels and permissions check.");
        }

        private async Task RegisterPushTokenAsync()
        {
#if UNITY_EDITOR
            Debug.Log("[NotificationService] Skipping push token registration — not supported in Unity Editor. Run on a real device.");
            await Task.CompletedTask;
#else
            try
            {
                // Must subscribe BEFORE calling RegisterForPushNotificationsAsync
                PushNotificationsService.Instance.OnRemoteNotificationReceived += OnRemotePushReceived;

                string token = await PushNotificationsService.Instance.RegisterForPushNotificationsAsync();
                Debug.Log($"[NotificationService] Device push token: {token}");

                if (_save != null && !string.IsNullOrEmpty(token))
                {
                    _save.DevicePushToken = token;
                    LocalProgress.Save(_save);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[NotificationService] Push token registration failed: {ex.Message}");
            }
#endif
        }

        private void OnRemotePushReceived(System.Collections.Generic.Dictionary<string, object> payload)
        {
            string title = payload.ContainsKey("title") ? payload["title"].ToString() : "StackSurge";
            string body  = payload.ContainsKey("body")  ? payload["body"].ToString()  : "";
            Debug.Log($"[NotificationService] Push received — {title}: {body}");
            InAppNotificationView.Show(title, body, NotificationType.Reminder);
        }

        private void SetupNotificationChannels()
        {
#if UNITY_ANDROID
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

            ScheduleLocalNotification(
                title,
                message,
                DateTime.Now.AddMinutes(5),
                LeaderboardChannelId
            );
        }

        private int GetSavedRank(LeaderboardScope scope)
        {
            return scope switch
            {
                LeaderboardScope.Daily   => _save.LastKnownRankDaily,
                LeaderboardScope.Weekly  => _save.LastKnownRankWeekly,
                _                        => _save.LastKnownRankAllTime,
            };
        }

        private void SetSavedRank(LeaderboardScope scope, int rank)
        {
            switch (scope)
            {
                case LeaderboardScope.Daily:   _save.LastKnownRankDaily   = rank; break;
                case LeaderboardScope.Weekly:  _save.LastKnownRankWeekly  = rank; break;
                default:                       _save.LastKnownRankAllTime = rank; break;
            }
        }


        // ── Friend Activity Events ─────────────────────────────────────────
        public void NotifyFriendRequestReceived(string senderName)
        {
            if (_save != null && !_save.NotifyFriendActivity) return;

            string title = "New Friend Request!";
            string message = $"{senderName} sent you a friend request. Accept it to compete on the Friends Leaderboard!";

            InAppNotificationView.Show(title, message, NotificationType.FriendActivity);

            ScheduleLocalNotification(
                title,
                message,
                DateTime.Now.AddSeconds(2),
                FriendsChannelId
            );
        }

        public void NotifyFriendRequestAccepted(string friendName)
        {
            if (_save != null && !_save.NotifyFriendActivity) return;

            string title = "Friend Request Accepted!";
            string message = $"{friendName} accepted your friend request! You can now view their high scores.";

            InAppNotificationView.Show(title, message, NotificationType.FriendActivity);
        }

        public void NotifyFriendBeatScore(string friendName, int newScore)
        {
            if (_save != null && !_save.NotifyFriendActivity) return;

            string title = "Friend Beat Your High Score!";
            string message = $"{friendName} just scored {newScore} points and passed you on the Friends Leaderboard!";

            InAppNotificationView.Show(title, message, NotificationType.FriendActivity);

            ScheduleLocalNotification(
                title,
                message,
                DateTime.Now.AddSeconds(5),
                FriendsChannelId
            );
        }

        // ── Scheduled Reminders ─────────────────────────────────────────────
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
                RemindersChannelId
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
                    RemindersChannelId
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
                RemindersChannelId
            );
        }

        // ── Low-Level Local Notification Dispatcher ─────────────────────────
        public void ScheduleLocalNotification(string title, string body, DateTime fireTime, string channelId)
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
            AndroidNotificationCenter.SendNotification(androidNotif, channelId);
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
                Identifier = Guid.NewGuid().ToString(),
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
