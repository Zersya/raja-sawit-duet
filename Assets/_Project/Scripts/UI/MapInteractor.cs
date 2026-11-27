using System.Collections.Generic;
using RajaSawit.Core;
using RajaSawit.Sim;
using RajaSawit.View;
using UnityEngine;

namespace RajaSawit.UI
{
    /// <summary>
    /// Turns a tap on the map into a selection and the bottom sheet's actions:
    /// a plot (panen, pupuk, obat hama), empty land (tanam), or forest (beli, mekanis, bakar).
    /// </summary>
    public sealed class MapInteractor : MonoBehaviour
    {
        [SerializeField] private CameraController cameraController;
        [SerializeField] private IsoMapView map;
        [SerializeField] private HudView hud;

        private enum Kind { None, Plot, Cleared, Forest, Building, Busy }

        private GameSession session;
        private Kind kind;
        private int plotId;
        private Vector2Int origin;
        private Vector2Int tapped;
        private readonly List<SheetAction> actions = new List<SheetAction>(3);

        private void Start()
        {
            session = GameSession.Instance;
            if (session == null || cameraController == null || map == null || hud == null)
            {
                Debug.LogError("[RajaSawit] MapInteractor is missing a reference.");
                enabled = false;
                return;
            }
            cameraController.Tapped += OnTap;
            session.StateChanged += RefreshSheet;
        }

        private void OnDestroy()
        {
            if (cameraController != null) cameraController.Tapped -= OnTap;
            if (session != null) session.StateChanged -= RefreshSheet;
        }

        private void OnTap(Vector2 screen)
        {
            Vector3 world = cameraController.ScreenToWorld(screen);
            if (!map.TryWorldToCell(world, out Vector2Int cell))
            {
                Deselect();
                return;
            }
            Select(cell);
        }

        private void Select(Vector2Int cell)
        {
            Simulation sim = session.Sim;
            GameState s = sim.State;
            int size = sim.Config.plotSize;
            tapped = cell;

            Plot p = s.PlotAt(cell.x, cell.y);
            if (p != null)
            {
                kind = Kind.Plot;
                plotId = p.id;
                origin = new Vector2Int(p.x, p.y);
            }
            else
            {
                LandType t = s.GetTile(cell.x, cell.y);
                switch (t)
                {
                    case LandType.Cleared:
                        kind = Kind.Cleared;
                        origin = BestOrigin(sim, cell, LandType.Cleared);
                        break;
                    case LandType.Forest:
                        kind = Kind.Forest;
                        origin = BestOrigin(sim, cell, LandType.Forest);
                        break;
                    case LandType.Building:
                        kind = Kind.Building;
                        origin = cell;
                        break;
                    default:
                        kind = Kind.Busy;
                        origin = cell;
                        break;
                }
            }

            map.SetHighlight(origin.x, origin.y, kind == Kind.Building || kind == Kind.Busy ? 1 : size);
            RefreshSheet();
        }

        /// <summary>Finds a 3x3 area of one land type that covers the tapped tile, centered if possible.</summary>
        private static Vector2Int BestOrigin(Simulation sim, Vector2Int cell, LandType type)
        {
            int size = sim.Config.plotSize;
            int half = size / 2;
            var centered = new Vector2Int(cell.x - half, cell.y - half);
            if (sim.IsAreaAll(centered.x, centered.y, type)) return centered;
            for (int dy = 0; dy < size; dy++)
                for (int dx = 0; dx < size; dx++)
                {
                    var o = new Vector2Int(cell.x - dx, cell.y - dy);
                    if (sim.IsAreaAll(o.x, o.y, type)) return o;
                }
            return centered;
        }

        private void Deselect()
        {
            kind = Kind.None;
            map.ClearHighlight();
            hud.HideSheet();
        }

        private void RefreshSheet()
        {
            if (kind == Kind.None) return;
            Simulation sim = session.Sim;
            BalanceConfig c = sim.Config;
            actions.Clear();

            switch (kind)
            {
                case Kind.Plot:
                {
                    Plot p = sim.State.FindPlot(plotId);
                    if (p == null) { Deselect(); return; }
                    string status = p.stage == PlotStage.Menghasilkan
                        ? (p.readyKg > 0f ? $"TBS siap {Format.Kg(p.readyKg)}" : $"Panen berikut {Format.Duration(c.harvestIntervalTicks - p.harvestTimer)}")
                        : $"Tumbuh {Format.Duration((p.stage == PlotStage.Bibit ? c.bibitTicks : c.remajaTicks) - p.stageTicks)} lagi";
                    string extras = (p.hasPest ? "  · Hama!" : "") + (p.fertilizedTicksLeft > 0 ? "  · Dipupuk" : "");
                    actions.Add(new SheetAction("Panen", () => hud.ShowResult(session.Run(s => s.Harvest(plotId))),
                        HudView.Green, HudView.Cream, p.readyKg > 0f));
                    actions.Add(new SheetAction($"Pupuk\n{Format.Rupiah(c.fertilizerCost)}", () => hud.ShowResult(session.Run(s => s.Fertilize(plotId))),
                        HudView.Gold, HudView.Ink, p.fertilizedTicksLeft <= 0));
                    actions.Add(new SheetAction($"Obat hama\n{Format.Rupiah(c.pestTreatCost)}", () => hud.ShowResult(session.Run(s => s.TreatPest(plotId))),
                        HudView.Blue, HudView.Cream, p.hasPest));
                    hud.ShowSheet($"Petak #{p.id} · {Format.Stage(p.stage)}", status + extras, actions);
                    break;
                }

                case Kind.Cleared:
                {
                    bool fits = sim.IsAreaAll(origin.x, origin.y, LandType.Cleared);
                    string body = fits
                        ? $"Lahan 3x3 siap. Petak {sim.State.plots.Count}/{sim.PlotCap}."
                        : "Butuh 3x3 tanah kosong. Coba ketuk bagian lain.";
                    actions.Add(new SheetAction($"Tanam bibit\n{Format.Rupiah(c.seedCost)}", () =>
                    {
                        CommandResult r = session.Run(s => s.Plant(origin.x, origin.y));
                        hud.ShowResult(r, "Bibit ditanam.");
                        if (r.Ok) Select(tapped);
                    }, HudView.Green, HudView.Cream, fits));
                    hud.ShowSheet("Tanah kosong", body, actions);
                    break;
                }

                case Kind.Forest:
                {
                    bool fits = sim.IsAreaAll(origin.x, origin.y, LandType.Forest);
                    string body = fits
                        ? $"Hutan 3x3. Bakar: Asap +{c.burnAsap:0}, Sorotan +{c.burnSorotan:0}, api bisa menjalar."
                        : "Butuh 3x3 hutan utuh. Coba ketuk bagian lain.";
                    actions.Add(new SheetAction($"Beli lahan\n{Format.Rupiah(c.buyLandCost)}",
                        () => Clear(ClearMethod.BuyCertified), HudView.Green, HudView.Cream, fits));
                    actions.Add(new SheetAction($"Mekanis {Format.Duration(c.mechanicalTicks)}\n{Format.Rupiah(c.mechanicalCost)}",
                        () => Clear(ClearMethod.Mechanical), HudView.Sand, HudView.Ink, fits));
                    actions.Add(new SheetAction($"Bakar\n{Format.Rupiah(c.burnCost)}",
                        () => Clear(ClearMethod.QuickBurn), HudView.Red, HudView.Cream, fits));
                    hud.ShowSheet("Buka lahan baru", body, actions);
                    break;
                }

                case Kind.Building:
                {
                    GameState s = sim.State;
                    Building b = FindBuilding(s, origin);
                    string title = b == null ? "Bangunan" : (b.type == BuildingType.Pks ? "Pabrik Kelapa Sawit" : "Rumah Juragan");
                    string body = b != null && b.type == BuildingType.Pks
                        ? $"Antre di ramp {Format.Kg(s.RampKg)} TBS. Tangki {Format.Kg(s.cpoKg)} dari {Format.Kg(c.tankCapacityKg)}."
                        : $"{LevelTitles.For(s.level)}. Total omzet {Format.Rupiah(s.totalRevenue)}. Razia {s.raziaCount} kali.";
                    hud.ShowSheet(title, body, actions);
                    break;
                }

                case Kind.Busy:
                {
                    LandType t = sim.State.GetTile(origin.x, origin.y);
                    if (t == LandType.Fire) hud.ShowSheet("Api!", "Lahan sedang terbakar. Angin menentukan arah api.", actions);
                    else if (t == LandType.Clearing) hud.ShowSheet("Alat berat bekerja", "Land clearing mekanis sedang berjalan.", actions);
                    else if (t == LandType.Cleared || t == LandType.Forest) Select(tapped);
                    else Deselect();
                    break;
                }
            }
        }

        private void Clear(ClearMethod method)
        {
            CommandResult r = session.Run(s => s.ClearLand(origin.x, origin.y, method));
            hud.ShowResult(r);
            if (r.Ok) Select(tapped);
        }

        private static Building FindBuilding(GameState s, Vector2Int cell)
        {
            foreach (Building b in s.buildings)
                if (cell.x >= b.x && cell.y >= b.y && cell.x < b.x + b.width && cell.y < b.y + b.height) return b;
            return null;
        }
    }
}
