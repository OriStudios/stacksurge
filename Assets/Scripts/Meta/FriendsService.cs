using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Friends.Models;
using Unity.Services.Friends.Notifications;
using UnityEngine;

// Alias to avoid name collision between StackSurge.Meta.FriendsService and Unity.Services.Friends.FriendsService
using UgsFriendsService = Unity.Services.Friends.FriendsService;

namespace StackSurge.Meta
{
    public enum PresenceStatus
    {
        Offline,
        Online,
        InGame,
        Busy
    }

    public struct FriendData
    {
        public string RelationshipId;
        public string PlayerId;
        public string PlayerName;
        public PresenceStatus Status;
        public string Activity;
    }

    public struct FriendRequestData
    {
        public string RelationshipId;
        public string PlayerId;
        public string PlayerName;
        public bool IsIncoming;
    }

    /// <summary>
    /// Async service wrapper for Unity Gaming Services (UGS) Friends.
    /// Handles friend listing, sending/accepting requests, removing friends, blocking users,
    /// and presence tracking. Includes offline mock data support for testing without a live backend connection.
    /// </summary>
    public class FriendsService
    {
        private readonly bool _isOnline;
        private readonly SaveData _save;
        private bool _isInitialized = false;

        public event Action OnFriendsUpdated;

        // Mock & Local tracking storage
        private readonly List<FriendData> _mockFriends = new List<FriendData>();
        private readonly List<FriendRequestData> _mockRequests = new List<FriendRequestData>();
        private readonly List<FriendRequestData> _trackedOutgoingRequests = new List<FriendRequestData>();
        private readonly List<string> _mockBlocked = new List<string>();

        public FriendsService(bool isOnline, SaveData save)
        {
            _isOnline = isOnline;
            _save = save;

            if (!_isOnline)
            {
                InitializeMockData();
            }
        }

        public async Task EnsureInitializedAsync()
        {
            if (!_isOnline || _isInitialized) return;

            try
            {
                if (UgsFriendsService.Instance != null && AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn)
                {
                    await UgsFriendsService.Instance.InitializeAsync();
                    _isInitialized = true;
                    SubscribeToServiceEvents();
                    Debug.Log("[FriendsService] UGS Friends Service initialized successfully.");

                    // Publish online status so friends can see us as Online
                    _ = PublishPresenceAsync(PresenceStatus.Online);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[FriendsService] Initialization failed (falling back to local mode): " + e.Message);
            }
        }

        /// <summary>
        /// Publishes the local player's presence availability to UGS Friends.
        /// Call with Online when signing in, Offline when quitting.
        /// No-op in offline/mock mode.
        /// </summary>
        public async Task PublishPresenceAsync(PresenceStatus status)
        {
            if (!_isOnline || !_isInitialized || UgsFriendsService.Instance == null) return;

            try
            {
                Unity.Services.Friends.Models.Availability availability = status switch
                {
                    PresenceStatus.Online  => Unity.Services.Friends.Models.Availability.Online,
                    PresenceStatus.Busy    => Unity.Services.Friends.Models.Availability.Busy,
                    PresenceStatus.InGame  => Unity.Services.Friends.Models.Availability.Online, // Map InGame -> Online for UGS
                    _                      => Unity.Services.Friends.Models.Availability.Offline
                };

                await UgsFriendsService.Instance.SetPresenceAvailabilityAsync(availability);
                Debug.Log($"[FriendsService] Presence published: {status}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FriendsService] PublishPresence failed: " + e.Message);
            }
        }

        /// <summary>
        /// Force-refresh the UGS Friends SDK local cache by calling InitializeAsync again.
        /// This is required after AddFriend/DeleteRelationship because OutgoingFriendRequests
        /// and IncomingFriendRequests are only updated during InitializeAsync.
        /// </summary>
        private async Task ForceRefreshAsync()
        {
            if (!_isOnline || !_isInitialized || UgsFriendsService.Instance == null) return;
            try
            {
                await UgsFriendsService.Instance.InitializeAsync();
                Debug.Log("[FriendsService] Cache refreshed via InitializeAsync.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[FriendsService] ForceRefresh failed: " + e.Message);
            }
        }

        private void SubscribeToServiceEvents()
        {
            try
            {
                if (UgsFriendsService.Instance != null)
                {
                    UgsFriendsService.Instance.RelationshipAdded += OnRelationshipAdded;
                    UgsFriendsService.Instance.RelationshipDeleted += OnRelationshipDeleted;
                    UgsFriendsService.Instance.PresenceUpdated += OnPresenceUpdated;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[FriendsService] Failed to subscribe to UGS Friends events: " + e.Message);
            }
        }

        private void OnRelationshipAdded(IRelationshipAddedEvent e)
        {
            Debug.Log($"[FriendsService] Relationship added event received.");
            OnFriendsUpdated?.Invoke();
        }

        private void OnRelationshipDeleted(IRelationshipDeletedEvent e)
        {
            Debug.Log($"[FriendsService] Relationship deleted event received.");
            OnFriendsUpdated?.Invoke();
        }

        private void OnPresenceUpdated(IPresenceUpdatedEvent e)
        {
            Debug.Log($"[FriendsService] Presence updated event received.");
            OnFriendsUpdated?.Invoke();
        }

        private void InitializeMockData()
        {
            _mockFriends.Add(new FriendData
            {
                RelationshipId = "mock_rel_1",
                PlayerId = "player_alpha_99",
                PlayerName = "StackMaster (ID: player_alpha_99)",
                Status = PresenceStatus.Online,
                Activity = "In Main Menu"
            });
            _mockFriends.Add(new FriendData
            {
                RelationshipId = "mock_rel_2",
                PlayerId = "player_beta_42",
                PlayerName = "SurgeKing (ID: player_beta_42)",
                Status = PresenceStatus.InGame,
                Activity = "Score: 14,200"
            });
            _mockFriends.Add(new FriendData
            {
                RelationshipId = "mock_rel_3",
                PlayerId = "player_gamma_07",
                PlayerName = "BlockBuster (ID: player_gamma_07)",
                Status = PresenceStatus.Offline,
                Activity = "Last seen 2h ago"
            });

            _mockRequests.Add(new FriendRequestData
            {
                RelationshipId = "mock_req_1",
                PlayerId = "player_delta_12",
                PlayerName = "ChallengerPro (ID: player_delta_12)",
                IsIncoming = true
            });
            _mockRequests.Add(new FriendRequestData
            {
                RelationshipId = "mock_req_2",
                PlayerId = "player_epsilon_88",
                PlayerName = "CasualStacker (ID: player_epsilon_88)",
                IsIncoming = false
            });
        }

        public string GetCurrentPlayerId()
        {
            if (_isOnline && AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn)
            {
                return AuthenticationService.Instance.PlayerId;
            }
            return _save != null ? "OfflineLocalPlayer" : "GuestPlayer";
        }

        // ── Fetch Friends List ──────────────────────────────────────────────
        public async Task<List<FriendData>> GetFriendsAsync()
        {
            await EnsureInitializedAsync();

            if (!_isOnline || !_isInitialized || UgsFriendsService.Instance == null) 
                return new List<FriendData>(_mockFriends);

            try
            {
                var friendsList = new List<FriendData>();
                IReadOnlyList<Relationship> relationships = UgsFriendsService.Instance.Friends;
                
                if (relationships != null)
                {
                    foreach (var rel in relationships)
                    {
                        var member = rel.Member;
                        string memberId = member?.Id ?? "Unknown";
                        // Prefer the UGS display name (player#NNNNN), fall back to raw ID
                        string name = !string.IsNullOrEmpty(member?.Profile?.Name)
                            ? member.Profile.Name
                            : FormatPlayerId(memberId);
                        
                        PresenceStatus status = PresenceStatus.Offline;
                        string activity = "Offline";
                        
                        if (member?.Presence != null)
                        {
                            status = ParsePresenceStatus(member.Presence.Availability.ToString());
                            activity = status == PresenceStatus.Online ? "Online" : "Offline";
                        }

                        friendsList.Add(new FriendData
                        {
                            RelationshipId = rel.Id,
                            PlayerId = memberId,
                            PlayerName = name,
                            Status = status,
                            Activity = activity
                        });
                    }
                }
                return friendsList;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[FriendsService] GetFriends failed: " + e.Message);
                return new List<FriendData>(_mockFriends);
            }
        }

        // ── Fetch Incoming & Outgoing Requests ──────────────────────────────
        public async Task<List<FriendRequestData>> GetIncomingRequestsAsync()
        {
            await EnsureInitializedAsync();

            if (!_isOnline || !_isInitialized || UgsFriendsService.Instance == null)
            {
                return _mockRequests.FindAll(r => r.IsIncoming);
            }

            try
            {
                var requests = new List<FriendRequestData>();
                IReadOnlyList<Relationship> incoming = UgsFriendsService.Instance.IncomingFriendRequests;
                if (incoming != null)
                {
                    foreach (var rel in incoming)
                    {
                        var member = rel.Member;
                        string memberId = member?.Id ?? "Unknown";
                        string name = !string.IsNullOrEmpty(member?.Profile?.Name)
                            ? member.Profile.Name
                            : FormatPlayerId(memberId);
                        requests.Add(new FriendRequestData
                        {
                            RelationshipId = rel.Id,
                            PlayerId = memberId,
                            PlayerName = name,
                            IsIncoming = true
                        });
                    }
                }
                return requests;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[FriendsService] GetIncomingRequests failed: " + e.Message);
                return _mockRequests.FindAll(r => r.IsIncoming);
            }
        }

        public async Task<List<FriendRequestData>> GetOutgoingRequestsAsync()
        {
            await EnsureInitializedAsync();

            var requests = new List<FriendRequestData>(_trackedOutgoingRequests);

            if (!_isOnline || !_isInitialized || UgsFriendsService.Instance == null)
            {
                foreach (var mock in _mockRequests.FindAll(r => !r.IsIncoming))
                {
                    if (!requests.Exists(r => r.PlayerId == mock.PlayerId))
                    {
                        requests.Add(mock);
                    }
                }
                return requests;
            }

            try
            {
                IReadOnlyList<Relationship> outgoing = UgsFriendsService.Instance.OutgoingFriendRequests;
                if (outgoing != null)
                {
                    foreach (var rel in outgoing)
                    {
                        var member = rel.Member;
                        string memberId = member?.Id ?? "Unknown";
                        if (!requests.Exists(r => r.PlayerId == memberId))
                        {
                            string name = !string.IsNullOrEmpty(member?.Profile?.Name)
                                ? member.Profile.Name
                                : FormatPlayerId(memberId);
                            requests.Add(new FriendRequestData
                            {
                                RelationshipId = rel.Id,
                                PlayerId = memberId,
                                PlayerName = name,
                                IsIncoming = false
                            });
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[FriendsService] GetOutgoingRequests failed: " + e.Message);
            }

            return requests;
        }

        // ── Add / Request Friend ────────────────────────────────────────────
        public async Task<bool> SendFriendRequestAsync(string targetInput)
        {
            if (string.IsNullOrWhiteSpace(targetInput)) return false;

            string cleanInput = targetInput.Trim();

            await EnsureInitializedAsync();

            if (!_isOnline || !_isInitialized || UgsFriendsService.Instance == null)
            {
                string reqId = "mock_req_" + UnityEngine.Random.Range(100, 999);
                var mockReq = new FriendRequestData
                {
                    RelationshipId = reqId,
                    PlayerId = cleanInput,
                    PlayerName = FormatPlayerId(cleanInput),
                    IsIncoming = false
                };
                _mockRequests.Add(mockReq);
                if (!_trackedOutgoingRequests.Exists(r => r.PlayerId == cleanInput))
                {
                    _trackedOutgoingRequests.Add(mockReq);
                }
                Debug.Log($"[FriendsService] Mock friend request sent to: {cleanInput}");
                OnFriendsUpdated?.Invoke();
                return true;
            }

            try
            {
                // Track locally so it immediately shows on Requests tab
                TrackOutgoingRequest(cleanInput);

                // 1. Try adding by display name first
                try
                {
                    await UgsFriendsService.Instance.AddFriendByNameAsync(cleanInput);
                    Debug.Log($"[FriendsService] Friend request sent by username: {cleanInput}");
                    await ForceRefreshAsync();
                    OnFriendsUpdated?.Invoke();
                    return true;
                }
                catch
                {
                    // Fallthrough to AddFriendAsync by Player ID
                }

                // 2. Try adding by Player ID directly
                await UgsFriendsService.Instance.AddFriendAsync(cleanInput);
                Debug.Log($"[FriendsService] Friend request sent by Player ID: {cleanInput}");
                await ForceRefreshAsync();
                OnFriendsUpdated?.Invoke();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FriendsService] SendFriendRequest to '{cleanInput}' failed: " + e.Message);
                return false;
            }
        }

        private void TrackOutgoingRequest(string targetId)
        {
            if (!_trackedOutgoingRequests.Exists(r => r.PlayerId == targetId))
            {
                _trackedOutgoingRequests.Add(new FriendRequestData
                {
                    RelationshipId = "tracked_req_" + targetId,
                    PlayerId = targetId,
                    PlayerName = FormatPlayerId(targetId),
                    IsIncoming = false
                });
            }
        }

        // ── Accept Request ──────────────────────────────────────────────────
        public async Task<bool> AcceptFriendRequestAsync(string targetPlayerIdOrRelationshipId)
        {
            if (string.IsNullOrWhiteSpace(targetPlayerIdOrRelationshipId)) return false;

            await EnsureInitializedAsync();

            if (!_isOnline || !_isInitialized || UgsFriendsService.Instance == null)
            {
                int index = _mockRequests.FindIndex(r => r.RelationshipId == targetPlayerIdOrRelationshipId || r.PlayerId == targetPlayerIdOrRelationshipId);
                if (index >= 0)
                {
                    var req = _mockRequests[index];
                    _mockRequests.RemoveAt(index);
                    _mockFriends.Add(new FriendData
                    {
                        RelationshipId = "mock_rel_" + UnityEngine.Random.Range(100, 999),
                        PlayerId = req.PlayerId,
                        PlayerName = req.PlayerName,
                        Status = PresenceStatus.Online,
                        Activity = "Just connected"
                    });
                    Debug.Log($"[FriendsService] Mock friend request accepted: {targetPlayerIdOrRelationshipId}");
                    OnFriendsUpdated?.Invoke();
                    return true;
                }
                return false;
            }

            try
            {
                // In UGS Friends, calling AddFriendAsync with an existing requester memberId accepts the request
                await UgsFriendsService.Instance.AddFriendAsync(targetPlayerIdOrRelationshipId);
                Debug.Log($"[FriendsService] Friend request accepted for target: {targetPlayerIdOrRelationshipId}");
                await ForceRefreshAsync();
                OnFriendsUpdated?.Invoke();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FriendsService] AcceptFriendRequest '{targetPlayerIdOrRelationshipId}' failed: " + e.Message);
                return false;
            }
        }

        // ── Decline / Remove Friend ─────────────────────────────────────────
        public async Task<bool> DeleteRelationshipAsync(string relationshipId)
        {
            if (string.IsNullOrWhiteSpace(relationshipId)) return false;

            _trackedOutgoingRequests.RemoveAll(r => r.RelationshipId == relationshipId || r.PlayerId == relationshipId);

            await EnsureInitializedAsync();

            if (!_isOnline || !_isInitialized || UgsFriendsService.Instance == null)
            {
                _mockFriends.RemoveAll(f => f.RelationshipId == relationshipId);
                _mockRequests.RemoveAll(r => r.RelationshipId == relationshipId);
                Debug.Log($"[FriendsService] Mock relationship deleted: {relationshipId}");
                OnFriendsUpdated?.Invoke();
                return true;
            }

            try
            {
                await UgsFriendsService.Instance.DeleteRelationshipAsync(relationshipId);
                Debug.Log($"[FriendsService] Relationship deleted: {relationshipId}");
                await ForceRefreshAsync();
                OnFriendsUpdated?.Invoke();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FriendsService] DeleteRelationship '{relationshipId}' failed: " + e.Message);
                return false;
            }
        }

        // ── Blocked Users List ──────────────────────────────────────────────
        public async Task<List<FriendData>> GetBlockedUsersAsync()
        {
            await EnsureInitializedAsync();

            if (!_isOnline || !_isInitialized || UgsFriendsService.Instance == null)
            {
                // Return mock blocked users as FriendData so the UI can render them
                var mockList = new List<FriendData>();
                foreach (string pid in _mockBlocked)
                {
                    mockList.Add(new FriendData
                    {
                        RelationshipId = "blocked_" + pid,
                        PlayerId = pid,
                        PlayerName = pid,
                        Status = PresenceStatus.Offline,
                        Activity = "Blocked"
                    });
                }
                return mockList;
            }

            try
            {
                var blockedList = new List<FriendData>();
                IReadOnlyList<Relationship> blocks = UgsFriendsService.Instance.Blocks;
                if (blocks != null)
                {
                    foreach (var rel in blocks)
                    {
                        var member = rel.Member;
                        string memberId = member?.Id ?? "Unknown";
                        string name = !string.IsNullOrEmpty(member?.Profile?.Name)
                            ? member.Profile.Name
                            : FormatPlayerId(memberId);
                        blockedList.Add(new FriendData
                        {
                            RelationshipId = rel.Id,
                            PlayerId = memberId,
                            PlayerName = name,
                            Status = PresenceStatus.Offline,
                            Activity = "Blocked"
                        });
                    }
                }
                return blockedList;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[FriendsService] GetBlockedUsers failed: " + e.Message);
                return new List<FriendData>();
            }
        }

        // ── Block / Unblock User ────────────────────────────────────────────
        public async Task<bool> BlockUserAsync(string targetPlayerId)
        {
            if (string.IsNullOrWhiteSpace(targetPlayerId)) return false;

            await EnsureInitializedAsync();

            if (!_isOnline || !_isInitialized || UgsFriendsService.Instance == null)
            {
                if (!_mockBlocked.Contains(targetPlayerId))
                {
                    _mockBlocked.Add(targetPlayerId);
                    _mockFriends.RemoveAll(f => f.PlayerId == targetPlayerId);
                    _mockRequests.RemoveAll(r => r.PlayerId == targetPlayerId);
                }
                Debug.Log($"[FriendsService] Mock user blocked: {targetPlayerId}");
                OnFriendsUpdated?.Invoke();
                return true;
            }

            try
            {
                await UgsFriendsService.Instance.AddBlockAsync(targetPlayerId);
                Debug.Log($"[FriendsService] User blocked: {targetPlayerId}");
                await ForceRefreshAsync();
                OnFriendsUpdated?.Invoke();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FriendsService] BlockUser '{targetPlayerId}' failed: " + e.Message);
                return false;
            }
        }

        public async Task<bool> UnblockUserAsync(string targetPlayerId)
        {
            if (string.IsNullOrWhiteSpace(targetPlayerId)) return false;

            await EnsureInitializedAsync();

            if (!_isOnline || !_isInitialized || UgsFriendsService.Instance == null)
            {
                _mockBlocked.Remove(targetPlayerId);
                Debug.Log($"[FriendsService] Mock user unblocked: {targetPlayerId}");
                OnFriendsUpdated?.Invoke();
                return true;
            }

            try
            {
                await UgsFriendsService.Instance.DeleteBlockAsync(targetPlayerId);
                Debug.Log($"[FriendsService] User unblocked: {targetPlayerId}");
                OnFriendsUpdated?.Invoke();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FriendsService] UnblockUser '{targetPlayerId}' failed: " + e.Message);
                return false;
            }
        }

        // ── Helpers ─────────────────────────────────────────────────────────
        private PresenceStatus ParsePresenceStatus(string availabilityStr)
        {
            if (string.IsNullOrEmpty(availabilityStr)) return PresenceStatus.Offline;

            if (availabilityStr.IndexOf("Online", StringComparison.OrdinalIgnoreCase) >= 0) return PresenceStatus.Online;
            if (availabilityStr.IndexOf("Busy", StringComparison.OrdinalIgnoreCase) >= 0) return PresenceStatus.Busy;
            if (availabilityStr.IndexOf("Game", StringComparison.OrdinalIgnoreCase) >= 0) return PresenceStatus.InGame;

            return PresenceStatus.Offline;
        }

        private static string FormatPlayerId(string id)
        {
            // Raw UGS player IDs are long hex strings. Show them shortened if no display name is available.
            if (string.IsNullOrEmpty(id)) return "Player";
            // If it looks like a UGS hex ID (>16 chars, no spaces), shorten it
            if (id.Length > 16 && !id.Contains(' '))
                return "player#" + id.Substring(id.Length - 5).ToUpper();
            return id;
        }
    }
}