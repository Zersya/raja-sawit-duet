using System;
using System.IO;
using RajaSawit.Sim;
using UnityEngine;

namespace RajaSawit.Core
{
    [Serializable]
    public class SaveEnvelope
    {
        public int formatVersion = 1;
        public long savedUtcTicks;
        public GameState state;
    }

    /// <summary>Local JSON save in persistentDataPath. Written to a temp file first so a crash never leaves half a save.</summary>
    public static class SaveService
    {
        private const string FileName = "rajasawit-save.json";

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public static void Save(GameState state)
        {
            var envelope = new SaveEnvelope { savedUtcTicks = DateTime.UtcNow.Ticks, state = state };
            string json = JsonUtility.ToJson(envelope);
            string tmp = FilePath + ".tmp";
            try
            {
                File.WriteAllText(tmp, json);
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[RajaSawit] Gagal menyimpan: {e.Message}");
            }
        }

        public static SaveEnvelope Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                var envelope = JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(FilePath));
                if (envelope == null || envelope.state == null) return null;
                return Migrate(envelope);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RajaSawit] Save rusak, mulai baru: {e.Message}");
                return null;
            }
        }

        public static void Delete()
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }

        /// <summary>Upgrade old saves here when GameState.CurrentVersion goes up.</summary>
        private static SaveEnvelope Migrate(SaveEnvelope envelope)
        {
            if (envelope.state.version > GameState.CurrentVersion) return null;
            envelope.state.version = GameState.CurrentVersion;
            return envelope;
        }
    }
}
