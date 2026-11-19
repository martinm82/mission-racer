using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MissionRacer.Network
{
    [Serializable]
    public struct LobbyPlayerData : IEquatable<LobbyPlayerData>
    {
        public ulong clientId;
        public string callsign;
        public string vehicleCode;
        public string paintHex;
        public string neonHex;
        public int driverAge;
        public bool isReady;

        public bool Equals(LobbyPlayerData other) => clientId == other.clientId;
    }

    public class FamilyLobbyManager : MonoBehaviour
    {
        public static FamilyLobbyManager Instance { get; private set; }

        public const int MAX_PLAYERS = 6;
        public const string RACE_SCENE_NAME = "MissionRacer_MonacoGP_3D";

        [Header("Active Lobby State")]
        public List<LobbyPlayerData> lobbyPlayers = new List<LobbyPlayerData>();
        private string _activeRelayJoinCode = "MR-7094";

        public string ActiveRelayJoinCode => _activeRelayJoinCode;
        public event Action<string> OnRelayCodeGenerated;
        public event Action<List<LobbyPlayerData>> OnLobbyUpdated;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void CreateFamilyHostRoom()
        {
            _activeRelayJoinCode = "MR-7094";
            OnRelayCodeGenerated?.Invoke(_activeRelayJoinCode);
            OnLobbyUpdated?.Invoke(lobbyPlayers);
        }

        public void JoinFamilyRoomByCode(string joinCode)
        {
            _activeRelayJoinCode = joinCode;
            OnLobbyUpdated?.Invoke(lobbyPlayers);
        }
    }
}
