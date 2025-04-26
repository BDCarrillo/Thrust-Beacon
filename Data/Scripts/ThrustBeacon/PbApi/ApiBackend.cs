using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace ThrustBeacon.Data.Scripts.ThrustBeacon.PbApi
{
    public class ApiBackend
    {
        private readonly Session _session;
        internal Dictionary<string, Delegate> PbApiMethods;

        internal ApiBackend(Session session)
        {
            _session = session;
        }

        internal void PbInit()
        {
            PbApiMethods = new Dictionary<string, Delegate>
            {
                ["GetThrustSignalBroadcastRange"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, int>(PbGetThrustSignalBroadcastRange),
            };
            var pb = MyAPIGateway.TerminalControls.CreateProperty<Dictionary<string, Delegate>, IMyTerminalBlock>("ThrustBeaconAPI");
            pb.Getter = b => PbApiMethods;
            MyAPIGateway.TerminalControls.AddControl<IMyProgrammableBlock>(pb);
            _session.PbApiInited = true;
        }
        
        private int PbGetThrustSignalBroadcastRange(object arg1)
        {
            var block = arg1 as IMyTerminalBlock;
            var cubeGrid = (IMyCubeGrid)(block?.Parent ?? block);
            GroupComp groupComp;
            if (block != null && Session.GroupDict.TryGetValue(cubeGrid.GetGridGroup(GridLinkTypeEnum.Mechanical), out groupComp))
                return GetThrustSignalBroadcastRange(groupComp);
            return -1;
        }
        
        private int GetThrustSignalBroadcastRange(GroupComp groupComp)
        {
            return groupComp.groupBroadcastDist;
        }
    }
}