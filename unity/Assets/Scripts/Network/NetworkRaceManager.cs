using System;
using System.Collections.Generic;
using UnityEngine;

namespace MissionRacer.Network
{
    public enum RaceState
    {
        LobbyWait,
        Countdown,
        Racing,
        PodiumFinished
    }

    public class NetworkRaceManager : MonoBehaviour
    {
        public static NetworkRaceManager Instance { get; private set; }

        public const int TOTAL_LAPS = 3;

        [Header("Race Flow")]
        public RaceState currentRaceState = RaceState.Racing;
        public float countdownTimer = 3.99f;

        [Header("Checkpoints")]
        [SerializeField] private Transform[] trackCheckpoints;

        public event Action<int> OnCountdownTick;
        public event Action OnRaceStarted;
        public event Action<string, int> OnPlayerFinished;

        private readonly List<ulong> _finishOrder = new List<ulong>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            if (currentRaceState == RaceState.Countdown)
            {
                countdownTimer -= Time.deltaTime;
                int secondInt = Mathf.CeilToInt(countdownTimer);
                OnCountdownTick?.Invoke(secondInt);

                if (countdownTimer <= 0f)
                {
                    currentRaceState = RaceState.Racing;
                    OnRaceStarted?.Invoke();
                }
            }
        }

        public void StartCountdown()
        {
            countdownTimer = 3.99f;
            currentRaceState = RaceState.Countdown;
        }
    }
}
