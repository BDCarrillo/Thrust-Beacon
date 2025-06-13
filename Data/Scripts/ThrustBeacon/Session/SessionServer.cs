using Digi.Example_NetworkProtobuf;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Utils;
using VRageMath;
using VRage.Game.ModAPI;

namespace ThrustBeacon
{
    public partial class Session : MySessionComponentBase
    {
        private void ServerUpdatePlayers()
        {
            PlayerList.Clear();
            if (MPActive)
                MyAPIGateway.Multiplayer.Players.GetPlayers(PlayerList);
            else
                PlayerList.Add(Session.Player); //SP workaround
        }

        private void ServerPowerShutdown()
        {
            foreach (var groupComp in powershutdownList.ToArray())
                groupComp.TogglePower();
        }

        private void ServerThrustShutdown()
        {
            foreach (var groupComp in thrustshutdownList.ToArray())
                groupComp.ToggleThrust();
        }

        private void ServerUpdateGroups()
        {
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
        }

        private void ServerSendLogs()
        {
            foreach (var readyLog in ReadyLogs)
                Networking.SendToPlayer(new PacketStatsSend(readyLog.Key), readyLog.Value);
            ReadyLogs.Clear();
        }

        private void ServerMainLoop()
        {
            var ss = ServerSettings.Instance;

            var tickMod = Tick % 100;
            foreach (var player in PlayerList)
            {
                if (player == null || player.IsBot || player.Character == null || (MPActive && player.SteamUserId == 0) || (player.IdentityId % 100 != tickMod) || (!ServerSettings.Instance.SendSignalDataToSuits && player.Controller?.ControlledEntity is IMyCharacter))
                    continue;

                var playerPos = player.Character.WorldAABB.Center;

                //Pull modifiers for current players grid (IE if it has increased detection range)
                var controlledGrid = (IMyCubeGrid)player.Controller?.ControlledEntity?.Entity?.Parent;
                var playerGridDetectionModSqr = 0f;
                var playerGridDetailMod = 0f;
                if (controlledGrid != null)
                {
                    GroupComp playerComp;
                    if (GroupDict.TryGetValue(controlledGrid.GetGridGroup(GridLinkTypeEnum.Mechanical), out playerComp))
                    {
                        //var playerComp = GroupDict[controlledGrid.GetGridGroup(GridLinkTypeEnum.Mechanical)];
                        playerGridDetectionModSqr = playerComp.groupDetectionRange * playerComp.groupDetectionRange;
                        if (playerComp.groupDetectionRange < 0)
                            playerGridDetectionModSqr *= -1;
                        playerGridDetailMod = playerComp.groupDetailMod;
                    }
                }

                var playerFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(player.IdentityId);
                var validSignalList = new List<SignalComp>();
                var tempSignalList = new List<SignalComp>();

                //For each player, iterate each grid
                foreach (var group in GroupDict.Values)
                {
                    var playerGrid = controlledGrid == null ? false : group.GridDict.ContainsKey(controlledGrid);
                    if ((!playerGrid && group.groupBroadcastDist < 2) || group.stealth || group.groupFuncCount == 0) continue;
                    var gridPos = group.groupSphere.Center;
                    var distToTargSqr = Vector3D.DistanceSquared(playerPos, gridPos);
                    if (!playerGrid && distToTargSqr > group.groupBroadcastDistSqr + playerGridDetectionModSqr) continue; //Distance check

                    //Check if occluded by a planet
                    if (ss.EnablePlanetOcclusion)
                    {
                        var planetOcclusion = false;
                        if (!playerGrid)
                        {
                            var dirRay = new RayD(playerPos, Vector3D.Normalize(gridPos - playerPos));//Pseudo ray from target to viewer
                            foreach (var planet in planetSpheres)
                            {
                                var hitDist = dirRay.Intersects(planet);
                                if (hitDist != null && hitDist * hitDist < distToTargSqr)//Check if ray hit planet sphere, and check if planet is beyond sig emitter
                                {
                                    var onPlanet = planet.Contains(gridPos) == ContainmentType.Contains;//Check if on planet
                                    if (!onPlanet || (onPlanet && Vector3D.DistanceSquared(planet.Center, gridPos) <= distToTargSqr))//Check if signal emitter or center of planet is closer, should catch cases where emitter is on the surface but not beyond horizon
                                    {
                                        planetOcclusion = true;
                                        break;
                                    }
                                }
                            }
                        }
                        if (planetOcclusion) continue;
                    }

                    //Faction/relation checking and signal data compilation
                    var masked = ss.EnableDataMasking && distToTargSqr > group.groupBroadcastDistSqr * (ss.DataMaskingRange + playerGridDetailMod) * (ss.DataMaskingRange + playerGridDetailMod);
                    var sameFaction = playerFaction != null && playerFaction.FactionId == group.groupFactionID;
                    var signalData = new SignalComp();
                    signalData.position = (Vector3I)gridPos;
                    signalData.range = playerGrid ? group.groupBroadcastDist : (int)Math.Sqrt(distToTargSqr);
                    signalData.faction = masked && !sameFaction ? "" : group.groupFaction;
                    signalData.entityID = playerGrid ? controlledGrid.EntityId : group.GridDict.FirstPair().Key.EntityId;
                    signalData.sizeEnum = group.groupSizeEnum;

                    if (playerGrid) //Own grid
                        signalData.relation = 4;
                    else if (!masked && !playerGrid && playerFaction != null && !sameFaction)//Not in player faction
                        signalData.relation = (byte)MyAPIGateway.Session.Factions.GetRelationBetweenFactions(playerFaction.FactionId, group.groupFactionID);
                    else if (sameFaction)//In player faction
                        signalData.relation = 3;
                    else if (!masked)//Factionless, presumed hostile
                        signalData.relation = 1;
                    else if (masked)//Outside detail range, mask data
                        signalData.relation = 2;

                    if (!useCombine || signalData.relation == 4 || signalData.range <= ss.CombineBeyond) //Add own grid or those within aggregate dist to final sig list
                        validSignalList.Add(signalData);
                    else
                        tempSignalList.Add(signalData); //Add others to list for aggregate processing
                }

                //Aggregate signals by proximity
                while (tempSignalList.Count > 0)
                {
                    var sig = tempSignalList[0];
                    tempSignalList.RemoveAtFast(0);
                    for (int i = 0; i < tempSignalList.Count; i++)
                    {
                        var checkSig = tempSignalList[i];
                        if (sig.faction == checkSig.faction && Vector3.DistanceSquared(sig.position, checkSig.position) <= combineDistSqr) //Within aggregation distance
                        {
                            sig.position = (sig.position + checkSig.position) / 2;//Re-average position
                            if (ss.CombineIncludeQuantity)
                                sig.quantity++;
                            if (ss.CombineIncrementSize && sig.sizeEnum < 5)
                                sig.sizeEnum++;
                            removalList.Add(i);
                        }
                    }
                    validSignalList.Add(sig);
                    tempSignalList.RemoveIndices(removalList);
                    removalList.Clear();
                }

                //If there's anything to send to the player, fire it off via the Networking or call the packet received method for SP
                if (validSignalList.Count > 0)
                {
                    var packet = new PacketSignals(validSignalList);
                    if (MPActive)
                        Networking.SendToPlayer(packet, player.SteamUserId);
                    else
                        packet.Received();
                }
            }
        }
    }
}
