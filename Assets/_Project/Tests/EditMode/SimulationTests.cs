using System.Collections.Generic;
using NUnit.Framework;
using RajaSawit.Sim;

namespace RajaSawit.Tests
{
    public class SimulationTests
    {
        private static Simulation NewSim(int seed = 42, BalanceConfig cfg = null)
        {
            cfg = cfg ?? new BalanceConfig();
            return new Simulation(cfg, Simulation.NewGame(cfg, seed));
        }

        private static (int x, int y) FindClearedSpot(Simulation sim)
        {
            int n = sim.State.gridSize;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    if (sim.IsAreaAll(x, y, LandType.Cleared)) return (x, y);
            Assert.Fail("No cleared 3x3 spot on the starting map");
            return (0, 0);
        }

        private static (int x, int y) FindForestSpot(Simulation sim, int minDistanceFromEdge = 4)
        {
            int n = sim.State.gridSize;
            for (int y = minDistanceFromEdge; y < n - minDistanceFromEdge; y++)
                for (int x = minDistanceFromEdge; x < n - minDistanceFromEdge; x++)
                    if (sim.IsAreaAll(x, y, LandType.Forest)) return (x, y);
            Assert.Fail("No forest spot");
            return (0, 0);
        }

        [Test]
        public void NewGame_HasStarterPlotBuildingsAndMoney()
        {
            var sim = NewSim();
            Assert.AreEqual(1, sim.State.plots.Count);
            Assert.AreEqual(2, sim.State.buildings.Count);
            Assert.AreEqual(sim.Config.startingRupiah, sim.State.rupiah);
            Assert.AreEqual(1, sim.State.trucks.Count);
        }

        [Test]
        public void StarterPlot_GivesFirstTbsWithinAMinute()
        {
            var sim = NewSim();
            sim.Advance(60);
            Assert.Greater(sim.State.plots[0].readyKg, 0f);
        }

        [Test]
        public void PlantedPlot_GrowsThroughStagesThenProduces()
        {
            var sim = NewSim();
            var (x, y) = FindClearedSpot(sim);
            Assert.IsTrue(sim.Plant(x, y).Ok);
            Plot p = sim.State.PlotAt(x, y);
            Assert.AreEqual(PlotStage.Bibit, p.stage);

            sim.Advance(sim.Config.bibitTicks);
            Assert.AreEqual(PlotStage.Remaja, p.stage);

            sim.Advance(sim.Config.remajaTicks);
            Assert.AreEqual(PlotStage.Menghasilkan, p.stage);

            sim.Advance(sim.Config.harvestIntervalTicks);
            Assert.Greater(p.readyKg, 0f);
        }

        [Test]
        public void Plant_RespectsPlotCapAndMoney()
        {
            var cfg = new BalanceConfig { plotCapByLevel = new[] { 1, 2, 3, 4, 5, 6 } };
            var sim = NewSim(cfg: cfg);
            var (x, y) = FindClearedSpot(sim);
            Assert.AreEqual(CommandError.PlotCapReached, sim.Plant(x, y).Error);

            var cfg2 = new BalanceConfig { startingRupiah = 0 };
            var poor = NewSim(cfg: cfg2);
            var (x2, y2) = FindClearedSpot(poor);
            Assert.AreEqual(CommandError.NotEnoughMoney, poor.Plant(x2, y2).Error);
        }

        [Test]
        public void FullChain_HarvestTruckMillSell_EarnsMoney()
        {
            var sim = NewSim();
            sim.Advance(60);
            Assert.IsTrue(sim.Harvest(sim.State.plots[0].id).Ok);
            Assert.Greater(sim.State.CollectionKg, 0f);

            // Truck one way + mill time.
            sim.Advance(sim.Config.truckOneWayTicks + 200);
            Assert.Greater(sim.State.cpoKg, 0f);

            float expected = sim.Config.yieldPerHarvestKg * sim.Config.rendemenMin;
            Assert.GreaterOrEqual(sim.State.cpoKg, expected * 0.99f);

            long before = sim.State.rupiah;
            Assert.IsTrue(sim.SellAllCpo().Ok);
            Assert.Greater(sim.State.rupiah, before);
            Assert.AreEqual(0f, sim.State.cpoKg, 0.001f);
        }

        [Test]
        public void StaleTbs_GivesLessOil()
        {
            var sim = NewSim();
            Assert.AreEqual(1f, sim.Freshness(sim.Config.tbsFreshTicks));
            Assert.AreEqual(0.99f, sim.Freshness(sim.Config.tbsFreshTicks + SimTime.TicksPerRealHour), 0.0001f);
            Assert.AreEqual(0f, sim.Freshness(sim.Config.tbsFreshTicks + 200 * SimTime.TicksPerRealHour));
        }

        [Test]
        public void UnpickedFruit_BlocksNextBunch()
        {
            var sim = NewSim();
            sim.Advance(60);
            float first = sim.State.plots[0].readyKg;
            sim.Advance(sim.Config.harvestIntervalTicks * 3);
            Assert.AreEqual(first, sim.State.plots[0].readyKg);
        }

        [Test]
        public void BuyCertified_ClearsImmediatelyWithoutRisk()
        {
            var sim = NewSim();
            var (x, y) = FindForestSpot(sim);
            Assert.IsTrue(sim.ClearLand(x, y, ClearMethod.BuyCertified).Ok);
            Assert.IsTrue(sim.IsAreaAll(x, y, LandType.Cleared));
            Assert.AreEqual(0f, sim.State.asap);
            Assert.AreEqual(0f, sim.State.sorotan);
        }

        [Test]
        public void Mechanical_TakesTwoHoursAndAddsSmallSorotan()
        {
            var sim = NewSim();
            var (x, y) = FindForestSpot(sim);
            Assert.IsTrue(sim.ClearLand(x, y, ClearMethod.Mechanical).Ok);
            Assert.IsTrue(sim.IsAreaAll(x, y, LandType.Clearing));
            Assert.AreEqual(1f, sim.State.sorotan, 0.001f);
            sim.Advance(sim.Config.mechanicalTicks);
            Assert.IsTrue(sim.IsAreaAll(x, y, LandType.Cleared));
        }

        [Test]
        public void QuickBurn_RaisesAsapAndSorotan_ThenFireGoesOut()
        {
            var sim = NewSim(seed: 7);
            var (x, y) = FindForestSpot(sim);
            Assert.IsTrue(sim.ClearLand(x, y, ClearMethod.QuickBurn).Ok);
            Assert.GreaterOrEqual(sim.State.asap, sim.Config.burnAsap);
            Assert.GreaterOrEqual(sim.State.sorotan, sim.Config.burnSorotan);

            // A fire must die out instead of eating the whole map.
            for (int i = 0; i < 20 && Count(sim, LandType.Fire) > 0; i++)
                sim.Advance(sim.Config.burnTicks);
            Assert.AreEqual(0, Count(sim, LandType.Fire));
            Assert.Less(Count(sim, LandType.Cleared), sim.State.tiles.Length / 4);
        }

        [Test]
        public void Fire_CanSpreadIntoAPlotAndDestroyIt()
        {
            var cfg = new BalanceConfig { fireSpreadChancePerTick = 1f };
            var sim = NewSim(cfg: cfg);
            var (x, y) = FindClearedSpot(sim);
            Assert.IsTrue(sim.Plant(x, y).Ok);
            int plotId = sim.State.PlotAt(x, y).id;

            // Light the tile right next to the new plot.
            int fx = x - 1;
            Assert.AreNotEqual(LandType.Plot, sim.State.GetTile(fx, y));
            sim.State.SetTile(fx, y, LandType.Fire);
            sim.State.fireTicksLeft[sim.State.Index(fx, y)] = 5;

            var events = new List<SimEvent>();
            sim.Advance(3);
            sim.DrainEvents(events);
            Assert.IsNull(sim.State.FindPlot(plotId));
            Assert.IsTrue(events.Exists(e => e.Type == SimEventType.PlotBurned && e.PlotId == plotId));
        }

        [Test]
        public void Razia_TriggersAtSorotan100AndFines()
        {
            var sim = NewSim();
            sim.State.sorotan = 95f;
            long before = sim.State.rupiah;
            var (x, y) = FindForestSpot(sim);
            Assert.IsTrue(sim.ClearLand(x, y, ClearMethod.QuickBurn).Ok);

            Assert.AreEqual(1, sim.State.raziaCount);
            Assert.AreEqual(sim.Config.sorotanAfterRazia, sim.State.sorotan, 0.001f);
            Assert.Less(sim.State.rupiah, before - sim.Config.burnCost);
        }

        [Test]
        public void SameSeedSameActions_GiveSameResult()
        {
            var a = NewSim(seed: 123);
            var b = NewSim(seed: 123);
            foreach (var sim in new[] { a, b })
            {
                var (x, y) = FindForestSpot(sim);
                sim.ClearLand(x, y, ClearMethod.QuickBurn);
                sim.Advance(3 * SimTime.TicksPerGameDay);
            }
            Assert.AreEqual(a.State.rng.state, b.State.rng.state);
            Assert.AreEqual(a.State.cpoPricePerKg, b.State.cpoPricePerKg);
            Assert.AreEqual(a.State.asap, b.State.asap);
            CollectionAssert.AreEqual(a.State.tiles, b.State.tiles);
        }

        [Test]
        public void LevelUp_CostsMoreEachLevelAndRaisesCap()
        {
            var cfg = new BalanceConfig { startingRupiah = 1_000_000_000 };
            var sim = NewSim(cfg: cfg);
            long cost1 = cfg.LevelUpCost(1);
            long cost2 = cfg.LevelUpCost(2);
            Assert.Greater(cost2, cost1);
            Assert.IsTrue(sim.UpgradeLevel().Ok);
            Assert.AreEqual(2, sim.State.level);
            Assert.AreEqual(12, sim.PlotCap);
        }

        [Test]
        public void Economy_SevenDayIdleHarvester_StaysSane()
        {
            // A simple bot: plants every cleared spot it can afford, harvests every minute, sells when the tank is half full.
            var sim = NewSim(seed: 2026);
            long[] moneyByDay = new long[8];
            moneyByDay[0] = sim.State.rupiah;
            for (int t = 1; t <= 7 * SimTime.TicksPerGameDay; t++)
            {
                sim.Tick();
                if (t % 60 == 0)
                {
                    sim.HarvestAll();
                    TryPlant(sim);
                    if (sim.State.cpoKg > sim.Config.tankCapacityKg * 0.5f) sim.SellAllCpo();
                }
                if (t % SimTime.TicksPerGameDay == 0) moneyByDay[t / SimTime.TicksPerGameDay] = sim.State.rupiah;
                sim.ClearEvents();
            }

            for (int d = 0; d < moneyByDay.Length; d++)
                TestContext.WriteLine($"Hari {d}: Rp {moneyByDay[d]:N0}, petak {sim.State.plots.Count}");

            Assert.Greater(sim.State.totalRevenue, 0);
            Assert.Greater(moneyByDay[7], moneyByDay[0]);
        }

        private static void TryPlant(Simulation sim)
        {
            if (sim.State.plots.Count >= sim.PlotCap || sim.State.rupiah < sim.Config.seedCost) return;
            int n = sim.State.gridSize;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    if (sim.IsAreaAll(x, y, LandType.Cleared))
                    {
                        sim.Plant(x, y);
                        return;
                    }
        }

        private static int Count(Simulation sim, LandType type)
        {
            int c = 0;
            foreach (var t in sim.State.tiles) if (t == type) c++;
            return c;
        }
    }
}
