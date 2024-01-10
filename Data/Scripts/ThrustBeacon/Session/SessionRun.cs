﻿using CoreSystems.Api;
using Digi.Example_NetworkProtobuf;
using Draygo.API;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Utils;
using VRageMath;
using VRage.Game.ModAPI;
using DefenseShields;

namespace ThrustBeacon
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public partial class Session : MySessionComponentBase
    {
        public override void BeforeStart()
        {
            Networking.Register();
            if (Server)
            {
                //Register group actions and init existing groups
                MyAPIGateway.GridGroups.OnGridGroupCreated += GridGroupsOnOnGridGroupCreated;
                MyAPIGateway.GridGroups.OnGridGroupDestroyed += GridGroupsOnOnGridGroupDestroyed;
                var groupStartList = new List<IMyGridGroupData>();
                MyAPIGateway.GridGroups.GetGridGroups(GridLinkTypeEnum.Mechanical, groupStartList);
                foreach(var group in groupStartList)
                    GridGroupsOnOnGridGroupCreated(group);
            }
        }
        public override void LoadData()
        {
            MPActive = MyAPIGateway.Multiplayer.MultiplayerActive;
            Server = (MPActive && MyAPIGateway.Multiplayer.IsServer) || !MPActive;
            Client = (MPActive && !MyAPIGateway.Multiplayer.IsServer) || !MPActive;
            if (Client)
            {
                InitConfig();
                hudAPI = new HudAPIv2(InitMenu);
                viewDist = Math.Min(Session.SessionSettings.SyncDistance, Session.SessionSettings.ViewDistance);
            }
            wcAPI = new WcApi();
            wcAPI.Load();
            if (Server)
            {
                dsAPI = new ShieldApi();
                dsAPI.Load();
                LoadSignalProducerConfigs(); //Blocks that generate signal (thrust, power)
                LoadBlockConfigs(); //Blocks that alter targeting
                InitServerConfig(); //Overall settings

                //Roll subtype IDs of all WC weapons into a hash set
                List<VRage.Game.MyDefinitionId> tempWeaponDefs = new List<VRage.Game.MyDefinitionId>();               
                if(wcAPI != null) 
                    wcAPI.GetAllCoreWeapons(tempWeaponDefs);
                foreach (var def in tempWeaponDefs)
                {
                    weaponSubtypeIDs.Add(def.SubtypeId);
                    MyLog.Default.WriteLineAndConsole(ModName + $"Registered {weaponSubtypeIDs.Count} weapon block types");
                }

            }
        }

        //Dump current signals when hopping out of a grid
        private void GridChange(VRage.Game.ModAPI.Interfaces.IMyControllableEntity previousEnt, VRage.Game.ModAPI.Interfaces.IMyControllableEntity newEnt)
        {
            if (newEnt is IMyCharacter)
            {
                SignalList.Clear();
            }
        }

        public override void UpdateBeforeSimulation()
        {
            //Register client action of changing entity
            if (Client && !clientActionRegistered && Session?.Player?.Controller != null)
            {
                clientActionRegistered = true;
                Session.Player.Controller.ControlledEntityChanged += GridChange;
                MyLog.Default.WriteLineAndConsole(ModName + "Registered client ControlledEntityChanged action");
            }

            //Calc draw ratio figures based on resolution
            if (Client && symbolHeight == 0)
            {
                aspectRatio = Session.Camera.ViewportSize.X / Session.Camera.ViewportSize.Y;
                symbolHeight = Settings.Instance.symbolWidth * aspectRatio;
                offscreenHeight = Settings.Instance.offscreenWidth * aspectRatio;
            }

            //Time keeps on ticking
            Tick++;

            //Server timed updates
            #region ServerUpdates
            if (Server && Tick % 300 == 0)
            {
                //Find player controlled entities in range and broadcast to them
                PlayerList.Clear();
                if (MPActive)
                    MyAPIGateway.Multiplayer.Players.GetPlayers(PlayerList);
                else
                    PlayerList.Add(Session.Player); //SP workaround
            }
            #endregion

            //Server main loop
            #region ServerLoop
            if (Server)
            {
                //Update grid comps to recalc signals on a background thread.  Rand element to make blipping the gas to avoid detection harder
                foreach (var group in GroupDict.Values)
                {
                    //Skip grid comps without fat blocks
                    if (group.groupFuncCount == 0)
                        continue;
                    //Recalc a grid on a rolling random frequency with a max age of 59 ticks
                    //Using 236 in the rand to give an approx 1 in 4 chance of an early update, but no faster than every 15 ticks
                    if (Tick - group.groupLastUpdate - 15 > rand.Next(236) || group.groupSpecialsDirty || Tick - group.groupLastUpdate > 59)
                        MyAPIGateway.Parallel.StartBackground(group.UpdateGroup);
                }

                //Update players if the last 2 digits of their identity ID = tick % 100 to spread out network updates.  If 100 ticks is too long, div by 2
                var tickMod = Tick % 100;
                foreach (var player in PlayerList)
                {
                    if (player == null || player.IsBot || player.Character == null || (MPActive && player.SteamUserId == 0) || (player.IdentityId % 100 != tickMod) || (!ServerSettings.Instance.SendSignalDataToSuits && player.Controller.ControlledEntity is IMyCharacter))
                    {
                        continue;
                    }

                    var playerPos = player.Character.WorldAABB.Center;
                    if (playerPos == Vector3D.Zero)
                    {
                        MyLog.Default.WriteLineAndConsole(ModName + $"Player position error - Vector3D.Zero - player.Name: {player.DisplayName} - player.SteamUserId: {player.SteamUserId}");
                        continue;
                    }

                    //Pull modifiers for current players grid (IE if it has increased detection range)
                    var controlledGrid = (IMyCubeGrid)player.Controller?.ControlledEntity?.Entity?.Parent;
                    var playerGridDetectionModSqr = 0f;
                    if (controlledGrid != null)
                    {
                        var playerComp = GroupDict[controlledGrid.GetGridGroup(GridLinkTypeEnum.Mechanical)];
                            playerGridDetectionModSqr = playerComp.groupDetectionRange * playerComp.groupDetectionRange;
                            if (playerComp.groupDetectionRange < 0)
                                playerGridDetectionModSqr *= -1;
                    }

                    var playerFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(player.IdentityId);
                    var validSignalList = new List<SignalComp>();

                    //For each player, iterate each grid
                    foreach (var group in GroupDict.Values)
                    {
                        var stealth = false;//((uint)grid.Grid.Flags & 0x20000000) > 0; //Stealth flag from Ash's mod
                        var playerGrid = controlledGrid == null ?  false : group.GridDict.ContainsKey(controlledGrid);


                        if ((!playerGrid && group.groupBroadcastDist < 2) || stealth || group.groupFuncCount == 0) continue;
                        var gridPos = group.groupSphere.Center;
                        var distToTargSqr = Vector3D.DistanceSquared(playerPos, gridPos);

                        //Check if current grid is in detection range of the player
                        if (playerGrid || distToTargSqr <= group.groupBroadcastDistSqr + playerGridDetectionModSqr)
                        {
                            var signalData = new SignalComp();
                            signalData.position = (Vector3I)gridPos;
                            signalData.range = playerGrid ? group.groupBroadcastDist : (int)Math.Sqrt(distToTargSqr);
                            signalData.faction = group.groupFaction;
                            signalData.entityID = playerGrid ? controlledGrid.EntityId : group.GetHashCode();
                            signalData.sizeEnum = group.groupSizeEnum;
                            if (!playerGrid && playerFaction != null)
                            {
                                var relation = MyAPIGateway.Session.Factions.GetRelationBetweenFactions(playerFaction.FactionId, group.groupFactionID);
                                signalData.relation = (byte)relation;
                            }
                            else
                                signalData.relation = 1;
                            validSignalList.Add(signalData);
                        }
                    }
                    //If there's anything to send to the player, fire it off via the Networking or call the packet received method for SP
                    if(validSignalList.Count>0)
                    {
                        var packet = new PacketBase(validSignalList);
                        if (MPActive)
                            Networking.SendToPlayer(packet, player.SteamUserId);
                        else
                            packet.Received();
                    }
                }
            }
            #endregion

            //Shutdown list updates in 5 tick interval to keep players from spamming keys to turn power back on.
            //Alternative is to register actions when the grid is in the shut down list, then de-register when removed.
            if (Server && Tick % 5 == 0 && powershutdownList.Count > 0)
            {
                foreach (var groupComp in powershutdownList.ToArray())
                    groupComp.TogglePower();
            }
            if (Server && Tick % 5 == 0 && thrustshutdownList.Count > 0)
            {
                foreach (var groupComp in thrustshutdownList.ToArray())
                    groupComp.ToggleThrust();
            }

        }

        protected override void UnloadData()
        {
            if (Server)
            {
                Clean();
                if (dsAPI != null)
                    dsAPI.Unload();
                try //Because this throws a NRE in keen code if you alt-F4
                {
                    MyAPIGateway.GridGroups.OnGridGroupCreated -= GridGroupsOnOnGridGroupCreated;
                    MyAPIGateway.GridGroups.OnGridGroupDestroyed -= GridGroupsOnOnGridGroupDestroyed;
                }
                catch { }
                
            }
            if(Client)
            {
                Save(Settings.Instance);                
                if(clientActionRegistered)
                    Session.Player.Controller.ControlledEntityChanged -= GridChange;
            }
            if (wcAPI != null)
                wcAPI.Unload();
            if (hudAPI != null)
                hudAPI.Unload();
            Networking?.Unregister();
            Networking = null;
            PlayerList.Clear();
            SignalList.Clear();
            powershutdownList.Clear();
            thrustshutdownList.Clear();
        }
    }
}
