namespace RajaSawit.Sim
{
    public static class LevelTitles
    {
        private static readonly string[] Titles =
        {
            "Petani Plasma", "Juragan", "Toke", "Bos PT", "Taipan", "Raja Sawit",
        };

        public static string For(int level)
        {
            int i = level < 1 ? 0 : (level > Titles.Length ? Titles.Length - 1 : level - 1);
            return Titles[i];
        }
    }
}
