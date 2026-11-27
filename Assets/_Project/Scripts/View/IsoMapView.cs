using RajaSawit.Core;
using RajaSawit.Sim;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RajaSawit.View
{
    /// <summary>
    /// Draws the simulation's 64x64 land grid on an isometric Tilemap and keeps it in sync.
    /// Only tiles whose look changed are rewritten.
    /// </summary>
    [RequireComponent(typeof(Grid))]
    public sealed class IsoMapView : MonoBehaviour
    {
        [SerializeField] private Tilemap ground;
        [SerializeField] private Tilemap highlight;

        private enum Look
        {
            None = -1,
            Forest, ForestDark, Cleared, Building, Clearing, Fire, Ember,
            Bibit, Remaja, Menghasilkan, Ready, Pest,
            Count,
        }

        private Tile[] tiles;
        private Tile highlightTile;
        private int[] lastLook;
        private Grid grid;
        private GameSession session;

        public Grid Grid => grid;

        private void Awake()
        {
            grid = GetComponent<Grid>();
            grid.cellLayout = GridLayout.CellLayout.Isometric;
            grid.cellSize = new Vector3(1f, 0.5f, 1f);
            if (ground == null) ground = CreateLayer("Ground", 0);
            if (highlight == null) highlight = CreateLayer("Highlight", 10);
            BuildTiles();
        }

        private void Start()
        {
            session = GameSession.Instance;
            if (session == null)
            {
                Debug.LogError("[RajaSawit] IsoMapView needs a GameSession in the scene.");
                enabled = false;
                return;
            }
            session.StateChanged += Redraw;
            Redraw();
        }

        private void OnDestroy()
        {
            if (session != null) session.StateChanged -= Redraw;
        }

        private Tilemap CreateLayer(string layerName, int sortingOrder)
        {
            var go = new GameObject(layerName);
            go.transform.SetParent(transform, false);
            var map = go.AddComponent<Tilemap>();
            var renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;
            return map;
        }

        private void BuildTiles()
        {
            tiles = new Tile[(int)Look.Count];
            tiles[(int)Look.Forest] = PlaceholderArt.MakeTile(PlaceholderArt.Forest);
            tiles[(int)Look.ForestDark] = PlaceholderArt.MakeTile(PlaceholderArt.ForestDark);
            tiles[(int)Look.Cleared] = PlaceholderArt.MakeTile(PlaceholderArt.Soil);
            tiles[(int)Look.Building] = PlaceholderArt.MakeTile(PlaceholderArt.Building);
            tiles[(int)Look.Clearing] = PlaceholderArt.MakeTile(PlaceholderArt.Clearing);
            tiles[(int)Look.Fire] = PlaceholderArt.MakeTile(PlaceholderArt.Fire, PlaceholderArt.Ember);
            tiles[(int)Look.Ember] = PlaceholderArt.MakeTile(PlaceholderArt.Ember, PlaceholderArt.Fire);
            tiles[(int)Look.Bibit] = PlaceholderArt.MakeTile(PlaceholderArt.Bibit);
            tiles[(int)Look.Remaja] = PlaceholderArt.MakeTile(PlaceholderArt.Remaja);
            tiles[(int)Look.Menghasilkan] = PlaceholderArt.MakeTile(PlaceholderArt.Menghasilkan);
            tiles[(int)Look.Ready] = PlaceholderArt.MakeTile(PlaceholderArt.Menghasilkan, PlaceholderArt.Ember);
            tiles[(int)Look.Pest] = PlaceholderArt.MakeTile(PlaceholderArt.Menghasilkan, PlaceholderArt.Pest);
            highlightTile = PlaceholderArt.MakeOutlineTile(PlaceholderArt.Cream);
        }

        private void Redraw()
        {
            GameState s = session.Sim.State;
            int n = s.gridSize;
            if (lastLook == null || lastLook.Length != n * n)
            {
                lastLook = new int[n * n];
                for (int i = 0; i < lastLook.Length; i++) lastLook[i] = (int)Look.None;
            }

            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    int look = (int)LookFor(s, x, y);
                    int idx = y * n + x;
                    if (lastLook[idx] == look) continue;
                    lastLook[idx] = look;
                    ground.SetTile(new Vector3Int(x, y, 0), tiles[look]);
                }
            }
        }

        private static Look LookFor(GameState s, int x, int y)
        {
            switch (s.GetTile(x, y))
            {
                case LandType.Forest:
                    return ((x * 7 + y * 13) % 5 == 0) ? Look.ForestDark : Look.Forest;
                case LandType.Cleared: return Look.Cleared;
                case LandType.Building: return Look.Building;
                case LandType.Clearing: return Look.Clearing;
                case LandType.Fire:
                    return ((x + y + (int)(s.tick / 2)) % 3 == 0) ? Look.Ember : Look.Fire;
                case LandType.Plot:
                    Plot p = s.PlotAt(x, y);
                    if (p == null) return Look.Cleared;
                    bool center = x == p.x + s.PlotSizeHint / 2 && y == p.y + s.PlotSizeHint / 2;
                    if (center && p.hasPest) return Look.Pest;
                    if (center && p.readyKg > 0f) return Look.Ready;
                    switch (p.stage)
                    {
                        case PlotStage.Bibit: return Look.Bibit;
                        case PlotStage.Remaja: return Look.Remaja;
                        default: return Look.Menghasilkan;
                    }
            }
            return Look.Cleared;
        }

        public bool TryWorldToCell(Vector3 world, out Vector2Int cell)
        {
            Vector3Int c = grid.WorldToCell(new Vector3(world.x, world.y, 0f));
            cell = new Vector2Int(c.x, c.y);
            GameState s = session != null ? session.Sim.State : null;
            return s != null && s.InBounds(c.x, c.y);
        }

        public Vector3 CellCenterWorld(int x, int y) => grid.GetCellCenterWorld(new Vector3Int(x, y, 0));

        /// <summary>World-space bounds of the whole map, for clamping the camera.</summary>
        public Bounds MapBounds()
        {
            int n = session != null ? session.Sim.State.gridSize : 64;
            var b = new Bounds(CellCenterWorld(0, 0), Vector3.zero);
            b.Encapsulate(CellCenterWorld(n - 1, 0));
            b.Encapsulate(CellCenterWorld(0, n - 1));
            b.Encapsulate(CellCenterWorld(n - 1, n - 1));
            return b;
        }

        public void SetHighlight(int x, int y, int size)
        {
            highlight.ClearAllTiles();
            for (int yy = y; yy < y + size; yy++)
                for (int xx = x; xx < x + size; xx++)
                    highlight.SetTile(new Vector3Int(xx, yy, 0), highlightTile);
        }

        public void ClearHighlight() => highlight.ClearAllTiles();
    }
}
