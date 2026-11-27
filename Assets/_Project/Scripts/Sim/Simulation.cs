using System;
using System.Collections.Generic;

namespace RajaSawit.Sim
{
    /// <summary>
    /// The palm oil farm, in plain C#. No UnityEngine here, so it runs in EditMode tests,
    /// in offline catch-up, and later on a server for PvP.
    /// The view sends commands (Plant, Harvest, ClearLand ...) and reads events back.
    /// </summary>
    public sealed class Simulation
    {
        private const int MaxBufferedEvents = 1024;

        // 0 north (+y), 1 east (+x), 2 south (-y), 3 west (-x)
        private static readonly int[] DirX = { 0, 1, 0, -1 };
        private static readonly int[] DirY = { 1, 0, -1, 0 };

        private readonly List<SimEvent> events = new List<SimEvent>();
        private readonly List<int> igniteBuffer = new List<int>();
        private readonly List<int> burnedPlotBuffer = new List<int>();

        public BalanceConfig Config { get; }
        public GameState State { get; }

        public IReadOnlyList<SimEvent> PendingEvents => events;

        public Simulation(BalanceConfig config, GameState state)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            State = state ?? throw new ArgumentNullException(nameof(state));
            State.PlotSizeHint = config.plotSize;
            EnsureTrucks();
        }

        // ------------------------------------------------------------------ new game

        public static GameState NewGame(BalanceConfig config, int seed)
        {
            var s = new GameState();
            s.gridSize = config.gridSize;
            s.tiles = new LandType[config.gridSize * config.gridSize];
            s.fireTicksLeft = new int[config.gridSize * config.gridSize];
            s.rupiah = config.startingRupiah;
            s.cpoPricePerKg = config.startingCpoPrice;
            s.rng.Seed(seed);
            s.windDirection = s.rng.Range(0, 4);
            s.PlotSizeHint = config.plotSize;

            for (int y = config.startClearedMin; y < config.startClearedMax; y++)
                for (int x = config.startClearedMin; x < config.startClearedMax; x++)
                    if (s.InBounds(x, y)) s.SetTile(x, y, LandType.Cleared);

            int m = config.startClearedMin;
            PlaceBuilding(s, BuildingType.RumahJuragan, m, m, 4, 4);
            PlaceBuilding(s, BuildingType.Pks, m + 5, m, 4, 4);

            // One producing starter plot so the first TBS shows up within a minute.
            var starter = new Plot
            {
                id = s.nextPlotId++,
                x = m,
                y = m + 5,
                stage = PlotStage.Menghasilkan,
                harvestTimer = Math.Max(0, config.harvestIntervalTicks - 60),
            };
            s.plots.Add(starter);
            MarkArea(s, starter.x, starter.y, config.plotSize, LandType.Plot);

            for (int i = 0; i < config.truckCount; i++) s.trucks.Add(new TruckState());
            return s;
        }

        private static void PlaceBuilding(GameState s, BuildingType type, int x, int y, int w, int h)
        {
            s.buildings.Add(new Building { type = type, x = x, y = y, width = w, height = h });
            for (int yy = y; yy < y + h; yy++)
                for (int xx = x; xx < x + w; xx++)
                    s.SetTile(xx, yy, LandType.Building);
        }

        private static void MarkArea(GameState s, int x, int y, int size, LandType type)
        {
            for (int yy = y; yy < y + size; yy++)
                for (int xx = x; xx < x + size; xx++)
                    s.SetTile(xx, yy, type);
        }

        private void EnsureTrucks()
        {
            while (State.trucks.Count < Config.truckCount) State.trucks.Add(new TruckState());
        }

        // ------------------------------------------------------------------ events

        public void DrainEvents(List<SimEvent> into)
        {
            into.AddRange(events);
            events.Clear();
        }

        public void ClearEvents() => events.Clear();

        private void Emit(SimEventType type, int plotId = 0, int x = 0, int y = 0, float amount = 0f)
        {
            if (events.Count < MaxBufferedEvents) events.Add(new SimEvent(type, plotId, x, y, amount));
        }

        // ------------------------------------------------------------------ time

        /// <summary>Runs up to <paramref name="ticks"/> ticks. Used for offline catch-up too.</summary>
        public void Advance(long ticks)
        {
            for (long i = 0; i < ticks; i++) Tick();
        }

        public void Tick()
        {
            State.tick++;
            TickClearing();
            TickFire();
            TickPlots();
            TickAging();
            TickTrucks();
            TickMill();
            if (State.tick % SimTime.TicksPerGameDay == 0) TickDaily();
        }

        private void TickClearing()
        {
            var jobs = State.clearingJobs;
            for (int i = jobs.Count - 1; i >= 0; i--)
            {
                ClearingJob job = jobs[i];
                job.ticksLeft--;
                if (job.ticksLeft > 0) continue;
                MarkArea(State, job.x, job.y, Config.plotSize, LandType.Cleared);
                jobs.RemoveAt(i);
                Emit(SimEventType.LandCleared, 0, job.x, job.y);
            }
        }

        private int activeFireTiles = -1;

        private void TickFire()
        {
            if (activeFireTiles < 0) activeFireTiles = CountFireTiles();
            if (activeFireTiles == 0) return;

            int n = State.gridSize;
            igniteBuffer.Clear();
            int stillBurning = 0;

            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    int idx = y * n + x;
                    if (State.tiles[idx] != LandType.Fire) continue;

                    for (int d = 0; d < 4; d++)
                    {
                        int nx = x + DirX[d], ny = y + DirY[d];
                        if (!State.InBounds(nx, ny)) continue;
                        LandType t = State.GetTile(nx, ny);
                        if (t != LandType.Forest && t != LandType.Plot) continue;
                        if (State.rng.Chance(Config.fireSpreadChancePerTick * WindFactor(d)))
                            igniteBuffer.Add(State.Index(nx, ny));
                    }

                    State.fireTicksLeft[idx]--;
                    if (State.fireTicksLeft[idx] <= 0)
                    {
                        State.fireTicksLeft[idx] = 0;
                        State.tiles[idx] = LandType.Cleared;
                    }
                    else
                    {
                        stillBurning++;
                    }
                }
            }

            burnedPlotBuffer.Clear();
            for (int i = 0; i < igniteBuffer.Count; i++)
            {
                int idx = igniteBuffer[i];
                int x = idx % n, y = idx / n;
                LandType t = State.tiles[idx];
                if (t == LandType.Forest)
                {
                    Ignite(idx);
                    stillBurning++;
                    State.asap = Clamp100(State.asap + Config.asapPerSpreadTile);
                    State.sorotan = Clamp100(State.sorotan + Config.sorotanPerSpreadTile);
                    Emit(SimEventType.FireSpread, 0, x, y);
                }
                else if (t == LandType.Plot)
                {
                    Plot p = State.PlotAt(x, y);
                    if (p == null || burnedPlotBuffer.Contains(p.id)) continue;
                    burnedPlotBuffer.Add(p.id);
                    for (int yy = p.y; yy < p.y + Config.plotSize; yy++)
                        for (int xx = p.x; xx < p.x + Config.plotSize; xx++)
                        {
                            Ignite(State.Index(xx, yy));
                            stillBurning++;
                        }
                    State.plots.Remove(p);
                    Emit(SimEventType.PlotBurned, p.id, p.x, p.y);
                }
            }

            if (stillBurning > 0)
            {
                CheckRazia();
            }
            else
            {
                Emit(SimEventType.FireOut);
            }
            activeFireTiles = stillBurning;
        }

        private void Ignite(int idx)
        {
            State.tiles[idx] = LandType.Fire;
            State.fireTicksLeft[idx] = Config.burnTicks;
        }

        private float WindFactor(int direction)
        {
            if (direction == State.windDirection) return Config.windwardMultiplier;
            if (direction == (State.windDirection + 2) % 4) return Config.leewardMultiplier;
            return 1f;
        }

        private int CountFireTiles()
        {
            int c = 0;
            for (int i = 0; i < State.tiles.Length; i++)
                if (State.tiles[i] == LandType.Fire) c++;
            return c;
        }

        private void TickPlots()
        {
            var plots = State.plots;
            for (int i = 0; i < plots.Count; i++)
            {
                Plot p = plots[i];
                if (p.fertilizedTicksLeft > 0) p.fertilizedTicksLeft--;

                switch (p.stage)
                {
                    case PlotStage.Bibit:
                        if (++p.stageTicks >= Config.bibitTicks) SetStage(p, PlotStage.Remaja);
                        break;
                    case PlotStage.Remaja:
                        if (++p.stageTicks >= Config.remajaTicks) SetStage(p, PlotStage.Menghasilkan);
                        break;
                    case PlotStage.Menghasilkan:
                        // Ripe fruit that is not picked blocks the next bunch.
                        if (p.readyKg > 0f) break;
                        if (++p.harvestTimer >= Config.harvestIntervalTicks)
                        {
                            p.harvestTimer = 0;
                            p.readyKg = YieldFor(p);
                            Emit(SimEventType.PlotReady, p.id, p.x, p.y, p.readyKg);
                        }
                        break;
                }
            }
        }

        private void SetStage(Plot p, PlotStage stage)
        {
            p.stage = stage;
            p.stageTicks = 0;
            p.harvestTimer = 0;
            Emit(SimEventType.PlotStageChanged, p.id, p.x, p.y, (float)stage);
        }

        public float YieldFor(Plot p)
        {
            float kg = Config.yieldPerHarvestKg;
            if (p.fertilizedTicksLeft > 0) kg *= Config.fertilizerYieldBonus;
            if (p.hasPest) kg *= Config.pestYieldFactor;
            if (State.asap >= Config.heavySmokeThreshold) kg *= Config.heavySmokeYieldFactor;
            return kg;
        }

        private void TickAging()
        {
            AgeAll(State.collection);
            AgeAll(State.ramp);
            for (int i = 0; i < State.trucks.Count; i++) AgeAll(State.trucks[i].cargo);
        }

        private static void AgeAll(List<TbsBatch> list)
        {
            for (int i = 0; i < list.Count; i++) list[i].ageTicks++;
        }

        private void TickTrucks()
        {
            for (int i = 0; i < State.trucks.Count; i++)
            {
                TruckState t = State.trucks[i];
                switch (t.phase)
                {
                    case TruckPhase.Idle:
                        if (State.collection.Count == 0) break;
                        float loaded = MoveKg(State.collection, t.cargo, Config.truckCapacityKg);
                        if (loaded <= 0f) break;
                        t.phase = TruckPhase.ToMill;
                        t.ticksLeft = Config.truckOneWayTicks;
                        Emit(SimEventType.TruckDeparted, i, 0, 0, loaded);
                        break;
                    case TruckPhase.ToMill:
                        if (--t.ticksLeft > 0) break;
                        float delivered = MoveKg(t.cargo, State.ramp, float.MaxValue);
                        t.phase = TruckPhase.Returning;
                        t.ticksLeft = Config.truckOneWayTicks;
                        Emit(SimEventType.TruckArrivedAtMill, i, 0, 0, delivered);
                        break;
                    case TruckPhase.Returning:
                        if (--t.ticksLeft <= 0) t.phase = TruckPhase.Idle;
                        break;
                }
            }
        }

        /// <summary>Moves up to maxKg from the oldest batches of <paramref name="from"/>, splitting the last one.</summary>
        private static float MoveKg(List<TbsBatch> from, List<TbsBatch> to, float maxKg)
        {
            float moved = 0f;
            while (from.Count > 0 && moved < maxKg)
            {
                TbsBatch b = from[0];
                float room = maxKg - moved;
                if (b.kg <= room)
                {
                    from.RemoveAt(0);
                    to.Add(b);
                    moved += b.kg;
                }
                else
                {
                    b.kg -= room;
                    to.Add(new TbsBatch { kg = room, ageTicks = b.ageTicks, rendemen = b.rendemen });
                    moved += room;
                }
            }
            return moved;
        }

        private bool tankFullReported;

        private void TickMill()
        {
            float budget = Config.millKgPerTick;
            while (budget > 0f && State.ramp.Count > 0)
            {
                float tankRoom = Config.tankCapacityKg - State.cpoKg;
                if (tankRoom <= 0.0001f)
                {
                    if (!tankFullReported) Emit(SimEventType.TankFull);
                    tankFullReported = true;
                    return;
                }
                tankFullReported = false;

                TbsBatch b = State.ramp[0];
                float cpoPerKg = b.rendemen * Freshness(b.ageTicks);
                float take = Math.Min(budget, b.kg);
                if (cpoPerKg > 0f) take = Math.Min(take, tankRoom / cpoPerKg);

                State.cpoKg += take * cpoPerKg;
                b.kg -= take;
                budget -= take;
                if (b.kg <= 0.0001f) State.ramp.RemoveAt(0);
            }
        }

        /// <summary>1 while fresh, then -1% per real hour after two hours. Rotten fruit gives no oil.</summary>
        public float Freshness(int ageTicks)
        {
            if (ageTicks <= Config.tbsFreshTicks) return 1f;
            float hoursOver = (ageTicks - Config.tbsFreshTicks) / (float)SimTime.TicksPerRealHour;
            return Math.Max(0f, 1f - Config.tbsDecayPerHour * hoursOver);
        }

        private void TickDaily()
        {
            State.windDirection = State.rng.Range(0, 4);

            float price = State.cpoPricePerKg;
            price *= 1f + State.rng.Range(-Config.cpoDailyVolatility, Config.cpoDailyVolatility);
            price += (Config.startingCpoPrice - price) * Config.cpoMeanReversion;
            State.cpoPricePerKg = Clamp(price, Config.cpoPriceMin, Config.cpoPriceMax);

            for (int i = 0; i < State.plots.Count; i++)
            {
                Plot p = State.plots[i];
                if (p.stage == PlotStage.Bibit || p.hasPest) continue;
                if (!State.rng.Chance(Config.pestChancePerDay)) continue;
                p.hasPest = true;
                Emit(SimEventType.PestAppeared, p.id, p.x, p.y);
            }

            if (State.asap >= Config.heavySmokeThreshold)
                State.sorotan = Clamp100(State.sorotan + Config.heavySmokeSorotanPerDay);
            State.asap = Clamp100(State.asap - Config.asapDecayPerDay);
            State.sorotan = Clamp100(State.sorotan - Config.sorotanDecayPerDay);
            CheckRazia();

            Emit(SimEventType.NewDay, 0, 0, 0, State.Day);
        }

        private void CheckRazia()
        {
            if (State.sorotan < 100f) return;
            long fine = (long)Math.Round(State.rupiah * (double)Config.raziaFineRate);
            State.rupiah -= fine;
            State.sorotan = Config.sorotanAfterRazia;
            State.raziaCount++;
            Emit(SimEventType.Razia, 0, 0, 0, fine);
        }

        // ------------------------------------------------------------------ commands

        public bool IsAreaAll(int x, int y, LandType type)
        {
            int size = Config.plotSize;
            for (int yy = y; yy < y + size; yy++)
                for (int xx = x; xx < x + size; xx++)
                    if (!State.InBounds(xx, yy) || State.GetTile(xx, yy) != type) return false;
            return true;
        }

        public int PlotCap => Config.PlotCap(State.level);

        public CommandResult Plant(int x, int y)
        {
            if (!IsAreaAll(x, y, LandType.Cleared)) return CommandResult.Fail(CommandError.InvalidArea);
            if (State.plots.Count >= PlotCap) return CommandResult.Fail(CommandError.PlotCapReached);
            if (!Spend(Config.seedCost)) return CommandResult.Fail(CommandError.NotEnoughMoney);

            var p = new Plot { id = State.nextPlotId++, x = x, y = y, stage = PlotStage.Bibit };
            State.plots.Add(p);
            MarkArea(State, x, y, Config.plotSize, LandType.Plot);
            Emit(SimEventType.PlotPlanted, p.id, x, y);
            return CommandResult.Success;
        }

        public CommandResult Harvest(int plotId)
        {
            Plot p = State.FindPlot(plotId);
            if (p == null) return CommandResult.Fail(CommandError.NotFound);
            if (p.readyKg <= 0f) return CommandResult.Fail(CommandError.NotReady);

            State.collection.Add(new TbsBatch
            {
                kg = p.readyKg,
                ageTicks = 0,
                rendemen = State.rng.Range(Config.rendemenMin, Config.rendemenMax),
            });
            Emit(SimEventType.PlotHarvested, p.id, p.x, p.y, p.readyKg);
            p.readyKg = 0f;
            return CommandResult.Success;
        }

        public int HarvestAll()
        {
            int count = 0;
            for (int i = 0; i < State.plots.Count; i++)
                if (Harvest(State.plots[i].id).Ok) count++;
            return count;
        }

        public CommandResult Fertilize(int plotId)
        {
            Plot p = State.FindPlot(plotId);
            if (p == null) return CommandResult.Fail(CommandError.NotFound);
            if (!Spend(Config.fertilizerCost)) return CommandResult.Fail(CommandError.NotEnoughMoney);
            p.fertilizedTicksLeft = Config.fertilizerDurationTicks;
            Emit(SimEventType.Fertilized, p.id, p.x, p.y);
            return CommandResult.Success;
        }

        public CommandResult TreatPest(int plotId)
        {
            Plot p = State.FindPlot(plotId);
            if (p == null) return CommandResult.Fail(CommandError.NotFound);
            if (!p.hasPest) return CommandResult.Fail(CommandError.NotReady);
            if (!Spend(Config.pestTreatCost)) return CommandResult.Fail(CommandError.NotEnoughMoney);
            p.hasPest = false;
            Emit(SimEventType.PestTreated, p.id, p.x, p.y);
            return CommandResult.Success;
        }

        public long ClearCost(ClearMethod method)
        {
            switch (method)
            {
                case ClearMethod.BuyCertified: return Config.buyLandCost;
                case ClearMethod.Mechanical: return Config.mechanicalCost;
                default: return Config.burnCost;
            }
        }

        public CommandResult ClearLand(int x, int y, ClearMethod method)
        {
            if (!IsAreaAll(x, y, LandType.Forest)) return CommandResult.Fail(CommandError.InvalidArea);
            if (!Spend(ClearCost(method))) return CommandResult.Fail(CommandError.NotEnoughMoney);

            switch (method)
            {
                case ClearMethod.BuyCertified:
                    MarkArea(State, x, y, Config.plotSize, LandType.Cleared);
                    Emit(SimEventType.LandCleared, 0, x, y);
                    break;

                case ClearMethod.Mechanical:
                    MarkArea(State, x, y, Config.plotSize, LandType.Clearing);
                    State.clearingJobs.Add(new ClearingJob { x = x, y = y, method = method, ticksLeft = Config.mechanicalTicks });
                    State.sorotan = Clamp100(State.sorotan + Config.mechanicalSorotan);
                    Emit(SimEventType.ClearingStarted, 0, x, y);
                    break;

                case ClearMethod.QuickBurn:
                    for (int yy = y; yy < y + Config.plotSize; yy++)
                        for (int xx = x; xx < x + Config.plotSize; xx++)
                            Ignite(State.Index(xx, yy));
                    if (activeFireTiles < 0) activeFireTiles = CountFireTiles();
                    else activeFireTiles += Config.plotSize * Config.plotSize;
                    State.asap = Clamp100(State.asap + Config.burnAsap);
                    State.sorotan = Clamp100(State.sorotan + Config.burnSorotan);
                    Emit(SimEventType.FireStarted, 0, x, y);
                    break;
            }

            CheckRazia();
            return CommandResult.Success;
        }

        public CommandResult SellCpo(float kg)
        {
            kg = Math.Min(kg, State.cpoKg);
            if (kg <= 0.0001f) return CommandResult.Fail(CommandError.NothingToSell);
            long revenue = (long)Math.Round(kg * (double)State.cpoPricePerKg);
            State.cpoKg -= kg;
            State.rupiah += revenue;
            State.totalRevenue += revenue;
            Emit(SimEventType.CpoSold, 0, 0, 0, revenue);
            return CommandResult.Success;
        }

        public CommandResult SellAllCpo() => SellCpo(State.cpoKg);

        public CommandResult UpgradeLevel()
        {
            if (State.level >= Config.MaxLevel) return CommandResult.Fail(CommandError.MaxLevel);
            if (!Spend(Config.LevelUpCost(State.level))) return CommandResult.Fail(CommandError.NotEnoughMoney);
            State.level++;
            Emit(SimEventType.LevelUp, 0, 0, 0, State.level);
            return CommandResult.Success;
        }

        private bool Spend(long amount)
        {
            if (State.rupiah < amount) return false;
            State.rupiah -= amount;
            return true;
        }

        private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
        private static float Clamp100(float v) => Clamp(v, 0f, 100f);
    }
}
