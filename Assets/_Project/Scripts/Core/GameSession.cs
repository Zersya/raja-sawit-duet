using System;
using System.Collections.Generic;
using RajaSawit.Data;
using RajaSawit.Sim;
using UnityEngine;

namespace RajaSawit.Core
{
    /// <summary>
    /// Owns the Simulation. Runs one tick per real second, saves on pause and quit,
    /// and catches up the time the player was away (capped by maxOfflineTicks).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameSession : MonoBehaviour
    {
        [SerializeField] private BalanceConfigAsset config;
        [Tooltip("Debug only: 1 = normal speed, 60 = one real second runs a game hour.")]
        [SerializeField, Min(0f)] private float timeScale = 1f;
        [Tooltip("0 = random seed for a new game.")]
        [SerializeField] private int newGameSeed;
        [SerializeField, Min(5f)] private float autosaveSeconds = 30f;

        public static GameSession Instance { get; private set; }

        public Simulation Sim { get; private set; }
        public long LastOfflineTicks { get; private set; }

        /// <summary>Fired for every simulation event, after each tick batch or command.</summary>
        public event Action<SimEvent> EventRaised;
        /// <summary>Fired once per frame in which the state changed.</summary>
        public event Action StateChanged;

        private readonly List<SimEvent> buffer = new List<SimEvent>();
        private float accumulator;
        private float autosaveTimer;
        private DateTime pausedAtUtc;
        private bool isPaused;

        public float TimeScale
        {
            get => timeScale;
            set => timeScale = Mathf.Max(0f, value);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BalanceConfig balance = config != null ? config.Balance : new BalanceConfig();
            LoadOrCreate(balance);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void LoadOrCreate(BalanceConfig balance)
        {
            SaveEnvelope saved = SaveService.Load();
            int expectedTiles = balance.gridSize * balance.gridSize;
            if (saved != null && saved.state.tiles != null && saved.state.tiles.Length == expectedTiles)
            {
                Sim = new Simulation(balance, saved.state);
                long away = (DateTime.UtcNow.Ticks - saved.savedUtcTicks) / TimeSpan.TicksPerSecond;
                CatchUp(away);
            }
            else
            {
                int seed = newGameSeed != 0 ? newGameSeed : Environment.TickCount;
                Sim = new Simulation(balance, Simulation.NewGame(balance, seed));
            }
        }

        private void CatchUp(long seconds)
        {
            long ticks = Math.Max(0, Math.Min(seconds, Sim.Config.maxOfflineTicks));
            LastOfflineTicks = ticks;
            if (ticks == 0) return;
            Sim.Advance(ticks);
            // Offline events are summarized by the UI from LastOfflineTicks, not replayed one by one.
            Sim.ClearEvents();
            StateChanged?.Invoke();
        }

        private void Update()
        {
            if (isPaused) return;

            accumulator += Time.unscaledDeltaTime * timeScale;
            int ran = 0;
            while (accumulator >= 1f && ran < 3600)
            {
                accumulator -= 1f;
                Sim.Tick();
                ran++;
            }
            if (ran > 0) Flush();

            autosaveTimer += Time.unscaledDeltaTime;
            if (autosaveTimer >= autosaveSeconds)
            {
                autosaveTimer = 0f;
                Save();
            }
        }

        /// <summary>Run a player command, then push its events to the view right away.</summary>
        public CommandResult Run(Func<Simulation, CommandResult> command)
        {
            CommandResult result = command(Sim);
            Flush();
            return result;
        }

        public void Flush()
        {
            Sim.DrainEvents(buffer);
            for (int i = 0; i < buffer.Count; i++) EventRaised?.Invoke(buffer[i]);
            buffer.Clear();
            StateChanged?.Invoke();
        }

        public void Save() => SaveService.Save(Sim.State);

        public void ResetGame()
        {
            SaveService.Delete();
            int seed = newGameSeed != 0 ? newGameSeed : Environment.TickCount;
            Sim = new Simulation(Sim.Config, Simulation.NewGame(Sim.Config, seed));
            LastOfflineTicks = 0;
            Flush();
        }

        private void OnApplicationPause(bool paused)
        {
            if (Sim == null) return;
            if (paused)
            {
                isPaused = true;
                pausedAtUtc = DateTime.UtcNow;
                Save();
            }
            else if (isPaused)
            {
                isPaused = false;
                accumulator = 0f;
                CatchUp((long)(DateTime.UtcNow - pausedAtUtc).TotalSeconds);
            }
        }

        private void OnApplicationQuit()
        {
            if (Sim != null) Save();
        }
    }
}
