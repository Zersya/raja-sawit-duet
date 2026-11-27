using RajaSawit.Sim;
using UnityEngine;

namespace RajaSawit.Data
{
    /// <summary>
    /// Holds every balance number so a designer can tune them in the Inspector.
    /// The simulation itself only sees the plain BalanceConfig inside.
    /// </summary>
    [CreateAssetMenu(fileName = "BalanceConfig", menuName = "Raja Sawit/Balance Config")]
    public sealed class BalanceConfigAsset : ScriptableObject
    {
        [SerializeField] private BalanceConfig balance = new BalanceConfig();

        public BalanceConfig Balance => balance;
    }
}
