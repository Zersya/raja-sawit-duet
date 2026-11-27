using System;

namespace RajaSawit.Sim
{
    /// <summary>
    /// Every balance number in one place. Durations are in ticks (1 tick = 1 real second).
    /// Defaults follow the GDD and mockups; they are a first draft meant to be tuned in the
    /// BalanceConfig asset inside Unity.
    /// </summary>
    [Serializable]
    public class BalanceConfig
    {
        // Peta
        public int gridSize = 64;
        public int plotSize = 3;
        public int startClearedMin = 26;
        public int startClearedMax = 38;

        // Awal permainan
        public long startingRupiah = 25_000_000;
        public float startingCpoPrice = 12_000f;

        // Level 1-6: Petani Plasma, Juragan, Toke, Bos PT, Taipan, Raja Sawit
        public int[] plotCapByLevel = { 6, 12, 20, 32, 48, 64 };
        public long levelUpBaseCost = 50_000_000;
        public float levelCostMultiplier = 1.6f;

        // Tanam dan tumbuh
        public long seedCost = 500_000;
        public int bibitTicks = 30 * SimTime.TicksPerRealMinute;
        public int remajaTicks = 60 * SimTime.TicksPerRealMinute;

        // Panen: TBS siap tiap 20 menit
        public int harvestIntervalTicks = 20 * SimTime.TicksPerRealMinute;
        public float yieldPerHarvestKg = 600f;

        // Rawat
        public long fertilizerCost = 150_000;
        public int fertilizerDurationTicks = SimTime.TicksPerGameDay;
        public float fertilizerYieldBonus = 1.3f;
        public float pestChancePerDay = 0.1f;
        public float pestYieldFactor = 0.5f;
        public long pestTreatCost = 100_000;

        // TBS membusuk: -1% per jam setelah 2 jam
        public int tbsFreshTicks = 2 * SimTime.TicksPerRealHour;
        public float tbsDecayPerHour = 0.01f;

        // Angkut
        public int truckCount = 1;
        public float truckCapacityKg = 2_000f;
        public int truckOneWayTicks = 150;

        // Olah di PKS: rendemen 20-24%
        public float millKgPerTick = 10f;
        public float rendemenMin = 0.20f;
        public float rendemenMax = 0.24f;
        public float tankCapacityKg = 5_000f;

        // Pasar CPO
        public float cpoPriceMin = 8_000f;
        public float cpoPriceMax = 18_000f;
        public float cpoDailyVolatility = 0.08f;
        public float cpoMeanReversion = 0.1f;

        // Buka lahan (angka dari mockup layar Bakar)
        public long buyLandCost = 18_000_000;
        public long mechanicalCost = 6_000_000;
        public int mechanicalTicks = 2 * SimTime.TicksPerRealHour;
        public float mechanicalSorotan = 1f;
        public long burnCost = 200_000;
        public int burnTicks = 10 * SimTime.TicksPerRealMinute;
        public float burnAsap = 15f;
        public float burnSorotan = 8f;

        // Api menjalar per petak
        public float fireSpreadChancePerTick = 0.00025f;
        public float windwardMultiplier = 2f;
        public float leewardMultiplier = 0.5f;
        public float asapPerSpreadTile = 0.5f;
        public float sorotanPerSpreadTile = 0.3f;

        // Meter risiko 0-100
        public float asapDecayPerDay = 12f;
        public float sorotanDecayPerDay = 2f;
        public float heavySmokeThreshold = 60f;
        public float heavySmokeYieldFactor = 0.8f;
        public float heavySmokeSorotanPerDay = 3f;
        public float raziaFineRate = 0.2f;
        public float sorotanAfterRazia = 40f;

        // Progres offline: paling lama 8 jam dikejar
        public int maxOfflineTicks = 8 * SimTime.TicksPerRealHour;

        public int MaxLevel => plotCapByLevel.Length;

        public int PlotCap(int level)
        {
            int i = Math.Max(1, Math.Min(level, plotCapByLevel.Length)) - 1;
            return plotCapByLevel[i];
        }

        public long LevelUpCost(int currentLevel)
        {
            return (long)Math.Round(levelUpBaseCost * Math.Pow(levelCostMultiplier, currentLevel - 1));
        }
    }
}
