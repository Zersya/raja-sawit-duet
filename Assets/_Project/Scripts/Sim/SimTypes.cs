using System;
using System.Collections.Generic;

namespace RajaSawit.Sim
{
    /// <summary>One tick = one real second = one game minute. One game day = 24 real minutes.</summary>
    public static class SimTime
    {
        public const int TicksPerGameHour = 60;
        public const int TicksPerGameDay = 1440;
        public const int TicksPerRealMinute = 60;
        public const int TicksPerRealHour = 3600;
    }

    public enum LandType
    {
        Forest = 0,
        Cleared = 1,
        Plot = 2,
        Building = 3,
        Clearing = 4,
        Fire = 5,
    }

    public enum PlotStage
    {
        Bibit = 0,
        Remaja = 1,
        Menghasilkan = 2,
    }

    public enum BuildingType
    {
        RumahJuragan = 0,
        Pks = 1,
        Gudang = 2,
    }

    public enum ClearMethod
    {
        /// <summary>Beli lahan bersertifikat: mahal, langsung jadi, tanpa risiko.</summary>
        BuyCertified = 0,
        /// <summary>Land clearing mekanis: sedang, butuh waktu, Sorotan kecil.</summary>
        Mechanical = 1,
        /// <summary>"Pembersihan cepat" alias bakar: murah, cepat, Asap dan Sorotan naik, api bisa menjalar.</summary>
        QuickBurn = 2,
    }

    public enum TruckPhase
    {
        Idle = 0,
        ToMill = 1,
        Returning = 2,
    }

    [Serializable]
    public class Plot
    {
        public int id;
        public int x;
        public int y;
        public PlotStage stage;
        public int stageTicks;
        public int harvestTimer;
        public float readyKg;
        public int fertilizedTicksLeft;
        public bool hasPest;
    }

    [Serializable]
    public class Building
    {
        public BuildingType type;
        public int x;
        public int y;
        public int width;
        public int height;
    }

    /// <summary>A heap of harvested fresh fruit bunches (TBS). Ages from the moment it is harvested.</summary>
    [Serializable]
    public class TbsBatch
    {
        public float kg;
        public int ageTicks;
        public float rendemen;
    }

    [Serializable]
    public class TruckState
    {
        public TruckPhase phase;
        public int ticksLeft;
        public List<TbsBatch> cargo = new List<TbsBatch>();
    }

    [Serializable]
    public class ClearingJob
    {
        public int x;
        public int y;
        public ClearMethod method;
        public int ticksLeft;
    }

    public enum CommandError
    {
        None = 0,
        NotEnoughMoney,
        PlotCapReached,
        InvalidArea,
        NotReady,
        NotFound,
        MaxLevel,
        NothingToSell,
    }

    public readonly struct CommandResult
    {
        public readonly CommandError Error;
        public bool Ok => Error == CommandError.None;

        private CommandResult(CommandError error) { Error = error; }

        public static readonly CommandResult Success = new CommandResult(CommandError.None);
        public static CommandResult Fail(CommandError error) => new CommandResult(error);
    }
}
