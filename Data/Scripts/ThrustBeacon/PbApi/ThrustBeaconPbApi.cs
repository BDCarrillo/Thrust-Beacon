using System;
using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using VRage;
using VRage.Game;
using VRageMath;

namespace ThrustBeacon.Data.Scripts.ThrustBeacon.PbApi
{
    /// <summary>
    /// https://github.com/BDCarrillo/Thrust-Beacon/blob/main/Data/Scripts/ThrustBeacon/PbApi/ThrustBeaconPbApi.cs
    /// </summary>
    public class ThrustBeaconPbApi
    {
        private Sandbox.ModAPI.Ingame.IMyTerminalBlock _self;
        private Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, int> _getThrustSignalBroadcastRange;

        public bool Activate(Sandbox.ModAPI.Ingame.IMyTerminalBlock pbBlock)
        {
            var dict = pbBlock.GetProperty("ThrustBeaconAPI")?.As<Dictionary<string, Delegate>>().GetValue(pbBlock);
            if (dict == null) throw new Exception("ThrustBeaconAPI failed to activate");
            _self = pbBlock;
            return ApiAssign(dict);
        }

        public bool ApiAssign(IReadOnlyDictionary<string, Delegate> delegates)
        {
            if (delegates == null)
                return false;

            AssignMethod(delegates, "GetThrustSignalBroadcastRange", ref _getThrustSignalBroadcastRange);
            return true;
        }

        private void AssignMethod<T>(IReadOnlyDictionary<string, Delegate> delegates, string name, ref T field) where T : class
        {
            if (delegates == null) {
                field = null;
                return;
            }

            Delegate del;
            if (!delegates.TryGetValue(name, out del))
                throw new Exception($"{GetType().Name} :: Couldn't find {name} delegate of type {typeof(T)}");

            field = del as T;
            if (field == null)
                throw new Exception(
                    $"{GetType().Name} :: Delegate {name} is not type {typeof(T)}, instead it's: {del.GetType()}");
        }

        public int GetThrustSignalBroadcastRange() => _getThrustSignalBroadcastRange?.Invoke(_self) ?? -1;
    }
}
