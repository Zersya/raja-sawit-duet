using System;
using RajaSawit.Sim;

namespace RajaSawit.UI
{
    /// <summary>Indonesian number words: Rp 850 rb, Rp 6 jt, Rp 1,2 M.</summary>
    public static class Format
    {
        public static string Rupiah(long value)
        {
            string sign = value < 0 ? "-" : "";
            double v = Math.Abs((double)value);
            if (v >= 1_000_000_000) return $"{sign}Rp {Short(v / 1_000_000_000)} M";
            if (v >= 1_000_000) return $"{sign}Rp {Short(v / 1_000_000)} jt";
            if (v >= 1_000) return $"{sign}Rp {Short(v / 1_000)} rb";
            return $"{sign}Rp {v:0}";
        }

        public static string Kg(float kg)
        {
            if (kg >= 1000f) return $"{Short(kg / 1000f)} t";
            return $"{kg:0} kg";
        }

        public static string Clock(GameState s)
        {
            int m = s.MinuteOfDay;
            return $"Hari {s.Day} · {m / 60:00}:{m % 60:00}";
        }

        public static string Duration(int ticks)
        {
            if (ticks >= SimTime.TicksPerRealHour) return $"{ticks / (float)SimTime.TicksPerRealHour:0.#} jam";
            if (ticks >= 60) return $"{ticks / 60} menit";
            return $"{ticks} detik";
        }

        public static string Stage(PlotStage stage)
        {
            switch (stage)
            {
                case PlotStage.Bibit: return "Bibit";
                case PlotStage.Remaja: return "Remaja";
                default: return "Menghasilkan";
            }
        }

        public static string Error(CommandError error)
        {
            switch (error)
            {
                case CommandError.NotEnoughMoney: return "Uangnya kurang, Bos.";
                case CommandError.PlotCapReached: return "Batas petak level ini penuh. Naik jabatan dulu.";
                case CommandError.InvalidArea: return "Lahannya tidak cocok untuk itu.";
                case CommandError.NotReady: return "Belum siap.";
                case CommandError.NotFound: return "Petaknya sudah tidak ada.";
                case CommandError.MaxLevel: return "Sudah jadi Raja Sawit.";
                case CommandError.NothingToSell: return "Tangki CPO kosong.";
                default: return "";
            }
        }

        // One decimal, Indonesian comma, no trailing ",0".
        private static string Short(double v)
        {
            string s = v >= 100 ? v.ToString("0") : v.ToString("0.#");
            return s.Replace('.', ',');
        }
    }
}
