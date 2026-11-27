namespace RajaSawit.Sim
{
    public enum SimEventType
    {
        NewDay,
        PlotPlanted,
        PlotStageChanged,
        PlotReady,
        PlotHarvested,
        PlotBurned,
        PestAppeared,
        PestTreated,
        Fertilized,
        TruckDeparted,
        TruckArrivedAtMill,
        TankFull,
        CpoSold,
        ClearingStarted,
        LandCleared,
        FireStarted,
        FireSpread,
        FireOut,
        Razia,
        LevelUp,
    }

    /// <summary>What the simulation tells the view. Small and allocation free.</summary>
    public readonly struct SimEvent
    {
        public readonly SimEventType Type;
        public readonly int PlotId;
        public readonly int X;
        public readonly int Y;
        public readonly float Amount;

        public SimEvent(SimEventType type, int plotId = 0, int x = 0, int y = 0, float amount = 0f)
        {
            Type = type;
            PlotId = plotId;
            X = x;
            Y = y;
            Amount = amount;
        }

        public override string ToString() => $"{Type} plot={PlotId} ({X},{Y}) amount={Amount}";
    }
}
