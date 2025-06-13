using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRageMath;

namespace ThrustBeacon
{
    internal class GroupComp
    {
        internal IMyGridGroupData iMyGroup;
        internal Dictionary<IMyCubeGrid, GridComp> GridDict = new Dictionary<IMyCubeGrid, GridComp>();
        internal float groupSignalRange;
        internal float groupDetectionRange;
        internal int groupBroadcastDist;
        internal long groupBroadcastDistSqr;
        internal BoundingSphereD groupSphere;
        internal int groupLastUpdate = 0;
        internal bool groupSpecialsDirty = false;
        internal int groupFuncCount = 0;
        internal byte groupSizeEnum;
        internal string groupFaction = "";
        internal long groupFactionID = 0;
        internal bool groupLogging = false;
        internal string groupLog = "";
        internal ulong groupLogRequestor = 0;
        internal float groupDetailMod = 0;
        internal bool stealth = false;
        internal void InitGrids()
        {
            List <IMyCubeGrid> tempGridList = new List<IMyCubeGrid>();
            iMyGroup.GetGrids(tempGridList);
            foreach(var startGrid in tempGridList)
            {
                var gridComp = new GridComp();
                gridComp.Init((MyCubeGrid)startGrid, iMyGroup);
                GridDict.Add(startGrid, gridComp);
            }            
        }

        internal void OnGridAdded(IMyGridGroupData AddedTo, IMyCubeGrid grid, IMyGridGroupData RemovedFrom)
        {
            if (RemovedFrom != null) //Existing comp, transfer over and update fields
            {
                var oldGroup = Session.GroupDict[RemovedFrom];
                var oldComp = oldGroup.GridDict[grid];
                //Update old
                oldGroup.GridDict.Remove(grid);
                oldGroup.groupFuncCount -= oldComp.funcCount;
                oldGroup.UpdateGroup();

                //Update new
                oldComp.group = AddedTo;
                GridDict.Add(grid, oldComp);
                groupFuncCount += oldComp.funcCount;
                UpdateGroup();
            }
            else //New comp
            {
                var gridComp = new GridComp();
                gridComp.Init((MyCubeGrid)grid, iMyGroup);
                GridDict.Add(grid, gridComp);               
            }
        }

        internal void OnGridRemoved(IMyGridGroupData RemovedFrom, IMyCubeGrid grid, IMyGridGroupData AddedTo)
        {
            if (AddedTo == null) //If it's being added to a group, the new group will handle the removal/update
            {
                GridComp gridComp;
                if (GridDict.TryGetValue(grid, out gridComp))
                {
                    groupFuncCount -= gridComp.funcCount;
                    gridComp.Clean();
                    GridDict.Remove(grid);
                }
            }
        }

        internal void UpdateGroup()
        {
            var ss = ServerSettings.Instance;
            groupSignalRange = 0;
            groupDetectionRange = 0;
            groupBroadcastDist = 0;
            groupBroadcastDistSqr = 0;
            groupDetailMod = 0f;

            var tempFactionDict = new Dictionary<long, int>();
            var tempGroupSphere = new BoundingSphereD(Vector3D.Zero, double.MinValue);
            foreach(var gridComp in GridDict.Values)
            {
                if (groupLogging)
                    gridComp.gridLogging = true;
                gridComp.CalcSignal();
                //Recalc group sphere
                tempGroupSphere.Include(gridComp.Grid.PositionComp.WorldVolume);

                //Figure faction weighting, by functional block count
                if (!tempFactionDict.ContainsKey(gridComp.factionID))
                    tempFactionDict.Add(gridComp.factionID, 0);
                tempFactionDict[gridComp.factionID] += gridComp.funcCount;

                //Tally signal
                groupBroadcastDist += gridComp.broadcastDist;
                groupSignalRange += gridComp.signalRange;
                groupDetectionRange += gridComp.detectionRange;
                groupDetailMod += gridComp.detailRange;

                //Stealth mod check
                if (((uint)gridComp.Grid.Flags & 0x20000000) > 0) //Stealth flag from Ash's mod
                    stealth = true;

                if (groupLogging && gridComp.gridLog.Length > 0)
                {
                    groupLog += gridComp.gridLog + "\n \n";
                    gridComp.gridLog = "";
                }
            }
            groupBroadcastDistSqr = (long)groupBroadcastDist * groupBroadcastDist;
            groupSphere = tempGroupSphere;
            groupLastUpdate = Session.Tick;
            groupSpecialsDirty = false; //Since they should be caight by CalcSignal

            //Faction ID and string from the grid with the most functional blocks in the group
            var tempCount = 0;
            groupFactionID = 0;
            foreach (var grid in tempFactionDict)
            {
                if (grid.Value > tempCount)
                {
                    tempCount = grid.Value;
                    groupFactionID = grid.Key;
                }
            }
            var faction = MyAPIGateway.Session.Factions.TryGetFactionById(groupFactionID);
            groupFaction = faction == null ? "" : faction.Tag + ".";

            //Update size enum
            if (groupBroadcastDist < ss.Distance1)//Idle
            {
                groupSizeEnum = 0;
            }
            else if (groupBroadcastDist < ss.Distance2)//Small
            {
                groupSizeEnum = 1;
            }
            else if (groupBroadcastDist < ss.Distance3)//Medium
            {
                groupSizeEnum = 2;
            }
            else if (groupBroadcastDist < ss.Distance4)//Large
            {
                groupSizeEnum = 3;
            }
            else if (groupBroadcastDist < ss.Distance5)//Huge
            {
                groupSizeEnum = 4;
            }
            else//Massive
            {
                groupSizeEnum = 5;
            }

            //Shutdown condition checks
            var npcFaction = Session.npcFactions.Contains(groupFactionID);
            if (!npcFaction || (!ss.SuppressShutdownForNPCs && npcFaction))
            {
                if (ss.ShutdownPowerOverMaxSignal)
                {
                    if (groupBroadcastDist >= ss.MaxSignalforPowerShutdown && !Session.powershutdownList.Contains(this))
                    {
                        Session.powershutdownList.Add(this);
                    }
                    else if (groupBroadcastDist < ss.MaxSignalforPowerShutdown && Session.powershutdownList.Contains(this))
                    {
                        Session.powershutdownList.Remove(this);
                        TogglePowerOn();
                    }
                }
                if (ss.ShutdownThrustersOverMaxSignal)
                {
                    if (groupBroadcastDist >= ss.MaxSignalforThrusterShutdown && !Session.thrustshutdownList.Contains(this))
                    {
                        Session.thrustshutdownList.Add(this);
                    }
                    else if (groupBroadcastDist < ss.MaxSignalforThrusterShutdown && Session.thrustshutdownList.Contains(this))
                    {
                        Session.thrustshutdownList.Remove(this);
                        ToggleThrustOn();
                    }
                }
            }

            if(Session.powershutdownList.Contains(this) || Session.thrustshutdownList.Contains(this))
                groupSizeEnum = 6;

            if (groupLogging)
            {
                groupLogging = false;
                Session.ReadyLogs.Add($"Group total broadcast: {groupBroadcastDist}m  {Session.messageList[groupSizeEnum]}\n \n" + groupLog, groupLogRequestor);
                groupLog = "";
            }
        }

        internal void TogglePower()
        {
            foreach (var gridComp in GridDict.Values)
                gridComp.TogglePower();
        }

        internal void ToggleThrust()
        {
            foreach (var gridComp in GridDict.Values)
                gridComp.ToggleThrust();
        }
        internal void TogglePowerOn()
        {
            foreach (var gridComp in GridDict.Values)
                gridComp.TogglePowerOn();
        }

        internal void ToggleThrustOn()
        {
            foreach (var gridComp in GridDict.Values)
                gridComp.ToggleThrustOn();
        }

        internal void Clean()
        {
            GridDict.Clear();
        }
    }
}
