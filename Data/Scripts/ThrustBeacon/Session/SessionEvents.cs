using Digi.Example_NetworkProtobuf;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;


namespace ThrustBeacon
{
    public partial class Session : MySessionComponentBase
    {
        private void RegisterWCDefs()
        {
            //Roll subtype IDs of all WC weapons into a hash set
            List<VRage.Game.MyDefinitionId> tempWeaponDefs = new List<VRage.Game.MyDefinitionId>();
            wcAPI.GetAllCoreWeapons(tempWeaponDefs);
            foreach (var def in tempWeaponDefs)
            {
                weaponSubtypeIDs.Add(def.SubtypeId);
            }

            MyLog.Default.WriteLineAndConsole($"{ModName}Registered {weaponSubtypeIDs.Count} weapon block types");
        }

        //Send newly connected clients server-specific data (label text)
        private void PlayerConnected(long playerId)
        {
            MyLog.Default.WriteLineAndConsole($"{ModName}Player connected " + playerId);
            var steamId = MyAPIGateway.Multiplayer.Players.TryGetSteamId(playerId);
            if (steamId != 0)
            {
                Networking.SendToPlayer(
                    new PacketSettings(messageList, ServerSettings.Instance.UpdateBeaconOnControlledGrid), steamId);
                MyLog.Default.WriteLineAndConsole($"{ModName}Sent settings to player " + steamId);
            }
            else
                MyLog.Default.WriteLineAndConsole($"{ModName}Failed to find steam ID for playerId " + playerId);
        }

        //Dump current signals when hopping out of a grid
        private void GridChange(VRage.Game.ModAPI.Interfaces.IMyControllableEntity previousEnt,
            VRage.Game.ModAPI.Interfaces.IMyControllableEntity newEnt)
        {
            if (newEnt is IMyCharacter)
            {
                SignalList.Clear();

                //Clear out old primary
                if (primaryBeacon != null)
                {
                    primaryBeacon.HudText = "";
                    primaryBeacon.Radius = 0;
                    Beacon_OnClosing(primaryBeacon);
                }
            }
            else if (newEnt is IMyCubeBlock && clientUpdateBeacon && !newEnt.Entity.MarkedForClose)
            {
                try
                {
                    var block = newEnt as IMyCubeBlock;
                    var entGrid = (MyCubeGrid)block.CubeGrid;
                    var group = entGrid.GetGridGroup(GridLinkTypeEnum.Mechanical);
                    var groupGridList = new List<IMyCubeGrid>();
                    group.GetGrids(groupGridList);
                    IMyBeacon lastBeacon = null;

                    foreach (var igrid in groupGridList)
                    {
                        var grid = (MyCubeGrid)igrid;
                        foreach (var fat in grid.GetFatBlocks())
                        {
                            if (!(fat is IMyBeacon))
                                continue;
                            var beacon = fat as IMyBeacon;
                            //There's already something tagged as primary
                            if (beacon.CustomName.Contains(primaryBeaconLabel))
                            {
                                if (primaryBeacon == null)
                                {
                                    primaryBeacon = beacon;
                                }
                                else //Multiple beacons marked as primary- clear out old (if player manually added [pri] tag or grid merge
                                {
                                    beacon.CustomName = beacon.CustomName.Replace(primaryBeaconLabel, string.Empty);
                                    beacon.HudText = "";
                                    beacon.Radius = 0;
                                }
                            }

                            lastBeacon = beacon;
                        }
                    }

                    //Update name, register actions, etc
                    if (primaryBeacon == null && lastBeacon != null)
                        primaryBeacon = lastBeacon;
                    if (primaryBeacon == null)
                        return;
                    if (!primaryBeacon.Enabled)
                        primaryBeacon.Enabled = true;
                    primaryBeacon.PropertiesChanged += Beacon_PropertiesChanged;
                    primaryBeacon.EnabledChanged += Beacon_EnabledChanged;
                    primaryBeacon.OnClosing += Beacon_OnClosing;
                    if (!primaryBeacon.CustomName.Contains(primaryBeaconLabel))
                        primaryBeacon.CustomName += primaryBeaconLabel;
                }
                catch (Exception e)
                {
                    MyLog.Default.WriteLineAndConsole(
                        $"{ModName} Error in selection of primary beacon - replication not ready {e.InnerException}");
                }
            }
        }

        private void Beacon_OnClosing(IMyEntity ent)
        {
            //Deregister actions for the primary beacon
            var beacon = ent as IMyBeacon;
            beacon.EnabledChanged -= Beacon_EnabledChanged;
            beacon.PropertiesChanged -= Beacon_PropertiesChanged;
            beacon.OnClosing -= Beacon_OnClosing;
            primaryBeacon = null;
        }

        private void Beacon_PropertiesChanged(IMyTerminalBlock block)
        {
            //Primary beacon anti-tamper
            var beacon = block as IMyBeacon;
            if (beacon.HudText != messageList[clientLastBeaconSizeEnum] ||
                Math.Abs(beacon.Radius - clientLastBeaconDist) > 0.0001)
            {
                beacon.Radius = clientLastBeaconDist;
                beacon.HudText = messageList[clientLastBeaconSizeEnum];
            }
        }

        private void Beacon_EnabledChanged(IMyTerminalBlock block)
        {
            //Primary beacon anti-tamper
            var beacon = block as IMyBeacon;
            if (!beacon.Enabled)
                beacon.Enabled = true;
        }

        private void GridGroupsOnOnGridGroupCreated(IMyGridGroupData group)
        {
            if (group.LinkType != GridLinkTypeEnum.Mechanical)
                return;
            GroupComp gComp = new GroupComp();
            gComp.iMyGroup = group;
            GroupDict.Add(group, gComp);
            gComp.InitGrids();

            group.OnGridAdded += gComp.OnGridAdded;
            group.OnGridRemoved += gComp.OnGridRemoved;
        }

        private void GridGroupsOnOnGridGroupDestroyed(IMyGridGroupData group)
        {
            if (group.LinkType != GridLinkTypeEnum.Mechanical)
                return;
            GroupComp gComp;
            if (GroupDict.TryGetValue(group, out gComp))
            {
                group.OnGridAdded -= gComp.OnGridAdded;
                group.OnGridRemoved -= gComp.OnGridRemoved;
                gComp.Clean();
                GroupDict.Remove(group);
            }
        }

        private void FactionCreated(long obj)
        {
            IMyFaction faction;
            if (Session.Factions.Factions.TryGetValue(obj, out faction) &&
                (!faction.AcceptHumans || faction.IsEveryoneNpc()))
            {
                if (!npcFactions.Contains(faction.FactionId))
                    npcFactions.Add(faction.FactionId);
            }
        }

        private void OnMessageEnteredSender(ulong sender, string messageText, ref bool sendToOthers)
        {
            messageText.ToLower();

            if (messageText == "/beacon stats")
            {
                sendToOthers = false;
                if (lastLogRequestTick + 300 < Tick)
                {
                    var controlledEnt = Session.Player?.Controller?.ControlledEntity;
                    var controlledBlock = controlledEnt as IMyCubeBlock;
                    if (controlledBlock == null)
                    {
                        MyAPIGateway.Utilities.ShowNotification("Must be seated in a grid for signal stats");
                        return;
                    }

                    lastLogRequestTick = Tick;
                    Networking.SendToServer(new PacketStatsRequest(Session.Player.SteamUserId,
                        controlledBlock.CubeGrid.EntityId));
                }
                else
                    MyAPIGateway.Utilities.ShowNotification("Please wait at least 5 seconds between log requests");

            }

            return;
        }

        internal void OnEntityCreate(MyEntity entity)
        {
            if (!PbApiInited && entity is IMyProgrammableBlock) PbActivate = true;
        }
    }
}
