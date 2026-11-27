using UnityEngine;
using UnityEngine.Tilemaps;

namespace RajaSawit.View
{
    /// <summary>
    /// Makes flat isometric diamond tiles at runtime, in the Raja Sawit palette,
    /// so the prototype runs before any real art exists. Replace with sprite atlases in Fase 2.
    /// </summary>
    public static class PlaceholderArt
    {
        public const int TileWidthPx = 64;
        public const int TileHeightPx = 32;

        public static readonly Color Forest = Hex(0x24572B);
        public static readonly Color ForestDark = Hex(0x1C4521);
        public static readonly Color Soil = Hex(0xB0703F);
        public static readonly Color Bibit = Hex(0xA9D46F);
        public static readonly Color Remaja = Hex(0x4E9A36);
        public static readonly Color Menghasilkan = Hex(0x2F7A32);
        public static readonly Color Building = Hex(0x8E857C);
        public static readonly Color Clearing = Hex(0xE3B23C);
        public static readonly Color Fire = Hex(0xD9462B);
        public static readonly Color Ember = Hex(0xF08A24);
        public static readonly Color Cream = Hex(0xFFF6E5);
        public static readonly Color Pest = Hex(0x8A2414);

        public static Tile MakeTile(Color fill, Color? dot = null, float edgeDarken = 0.18f)
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = MakeDiamond(fill, dot, edgeDarken);
            tile.colliderType = Tile.ColliderType.None;
            return tile;
        }

        public static Tile MakeOutlineTile(Color color)
        {
            var tex = NewTexture();
            var pixels = new Color32[TileWidthPx * TileHeightPx];
            for (int py = 0; py < TileHeightPx; py++)
                for (int px = 0; px < TileWidthPx; px++)
                {
                    float d = DiamondDistance(px, py);
                    pixels[py * TileWidthPx + px] = d <= 1f && d >= 0.82f ? (Color32)color : new Color32(0, 0, 0, 0);
                }
            tex.SetPixels32(pixels);
            tex.Apply();
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = Sprite.Create(tex, new Rect(0, 0, TileWidthPx, TileHeightPx), new Vector2(0.5f, 0.5f), TileWidthPx);
            tile.colliderType = Tile.ColliderType.None;
            return tile;
        }

        private static Sprite MakeDiamond(Color fill, Color? dot, float edgeDarken)
        {
            var tex = NewTexture();
            var pixels = new Color32[TileWidthPx * TileHeightPx];
            Color edge = Color.Lerp(fill, Color.black, edgeDarken);
            for (int py = 0; py < TileHeightPx; py++)
            {
                for (int px = 0; px < TileWidthPx; px++)
                {
                    float d = DiamondDistance(px, py);
                    Color c;
                    if (d > 1f) c = new Color(0, 0, 0, 0);
                    else if (d > 0.9f) c = edge;
                    else c = fill;

                    if (dot.HasValue && d <= 1f)
                    {
                        float dx = (px - (TileWidthPx - 1) * 0.5f) / 8f;
                        float dy = (py - (TileHeightPx - 1) * 0.5f) / 4f;
                        if (dx * dx + dy * dy <= 1f) c = dot.Value;
                    }
                    pixels[py * TileWidthPx + px] = c;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, TileWidthPx, TileHeightPx), new Vector2(0.5f, 0.5f), TileWidthPx);
        }

        private static Texture2D NewTexture()
        {
            return new Texture2D(TileWidthPx, TileHeightPx, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
        }

        /// <summary>0 at the center, 1 on the diamond's edge.</summary>
        private static float DiamondDistance(int px, int py)
        {
            float cx = (TileWidthPx - 1) * 0.5f;
            float cy = (TileHeightPx - 1) * 0.5f;
            return Mathf.Abs(px - cx) / (TileWidthPx * 0.5f) + Mathf.Abs(py - cy) / (TileHeightPx * 0.5f);
        }

        public static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }
    }
}
