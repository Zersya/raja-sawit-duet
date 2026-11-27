using System;
using System.Collections.Generic;

namespace RajaSawit.Sim
{
    /// <summary>The whole save. Plain serializable fields only, so Unity's JsonUtility can write it.</summary>
    [Serializable]
    public class GameState
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public long tick;
        public int level = 1;

        // Sumber daya
        public long rupiah;
        public float cpoKg;
        public float cpoPricePerKg;
        public int pengaruh;
        public long totalRevenue;

        // Meter risiko 0-100
        public float asap;
        public float sorotan;
        public int raziaCount;

        // Angin hari ini: 0 utara, 1 timur, 2 selatan, 3 barat
        public int windDirection;

        // Peta
        public int gridSize;
        public LandType[] tiles;
        public int[] fireTicksLeft;

        public List<Plot> plots = new List<Plot>();
        public int nextPlotId = 1;
        public List<Building> buildings = new List<Building>();
        public List<ClearingJob> clearingJobs = new List<ClearingJob>();

        // Rantai TBS: kebun -> truk -> ramp PKS -> tangki CPO
        public List<TbsBatch> collection = new List<TbsBatch>();
        public List<TruckState> trucks = new List<TruckState>();
        public List<TbsBatch> ramp = new List<TbsBatch>();

        public SimRandom rng = new SimRandom();

        public int Day => (int)(tick / SimTime.TicksPerGameDay) + 1;
        public int MinuteOfDay => (int)(tick % SimTime.TicksPerGameDay);

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < gridSize && y < gridSize;
        public int Index(int x, int y) => y * gridSize + x;
        public LandType GetTile(int x, int y) => tiles[Index(x, y)];
        public void SetTile(int x, int y, LandType type) => tiles[Index(x, y)] = type;

        public float CollectionKg => SumKg(collection);
        public float RampKg => SumKg(ramp);

        public Plot FindPlot(int id)
        {
            for (int i = 0; i < plots.Count; i++)
                if (plots[i].id == id) return plots[i];
            return null;
        }

        public Plot PlotAt(int x, int y)
        {
            for (int i = 0; i < plots.Count; i++)
            {
                Plot p = plots[i];
                if (x >= p.x && y >= p.y && x < p.x + PlotSizeHint && y < p.y + PlotSizeHint) return p;
            }
            return null;
        }

        /// <summary>Set by Simulation so PlotAt works without a config reference.</summary>
        [NonSerialized] public int PlotSizeHint = 3;

        private static float SumKg(List<TbsBatch> list)
        {
            float total = 0f;
            for (int i = 0; i < list.Count; i++) total += list[i].kg;
            return total;
        }
    }
}
