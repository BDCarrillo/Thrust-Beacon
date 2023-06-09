﻿using CoreSystems.Api;
using Digi.Example_NetworkProtobuf;
using Draygo.API;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Text;
using VRage;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;

namespace ThrustBeacon
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public partial class Session : MySessionComponentBase
    {
        //Future options stuff?
        internal Color signalColor = Color.Yellow;
        internal float symbolWidth = 0.04f;
        internal float offscreenWidth = 0.1f;
        internal int fadeOutTime = 90;
        internal int maxContactAge = 500;
        internal float textSize = 1f;
        internal Vector2D signalDrawCoords = new Vector2D(-0.7, -0.625);

        public override void BeforeStart()
        {
            Networking.Register();
        }
        public override void LoadData()
        {
            MPActive = MyAPIGateway.Multiplayer.MultiplayerActive;
            Server = (MyAPIGateway.Multiplayer.MultiplayerActive && MyAPIGateway.Multiplayer.IsServer) || !MPActive; //TODO check if I jacked these up for actual application
            Client = (MyAPIGateway.Multiplayer.MultiplayerActive && !MyAPIGateway.Multiplayer.IsServer) || !MPActive;
            if (Server)
            {
                MyEntities.OnEntityCreate += OnEntityCreate;
                //TODO: Hook player joining for server and populate PlayerList, or just jam GetPlayers in Update?
            }
            if (Client)
            {
                hudAPI = new HudAPIv2();
                wcAPI = new WcApi();
                wcAPI.Load();
                viewDist = Math.Min(Session.SessionSettings.SyncDistance, Session.SessionSettings.ViewDistance);
            }
            if (!MPActive)
                PlayerList.Add(MyAPIGateway.Session.Player);

        }
        public override void UpdateBeforeSimulation()
        {
            if (Client && symbolHeight == 0)//TODO see if there's a better spot for this
            {
                aspectRatio = Session.Camera.ViewportSize.X / Session.Camera.ViewportSize.Y;
                symbolHeight = symbolWidth * aspectRatio;
                offscreenHeight = offscreenWidth * aspectRatio;
            }

            Tick++;
            if (Server && Tick % 60 == 0)
            {
                foreach (var gridComp in GridList)
                {
                    if (gridComp.thrustList.Count > 0)
                        gridComp.CalcThrust();//TODO: See if there's a better way to account for pulsing/blipping the gas
                }
                //Find player controlled entities in range and broadcast to them
                PlayerList.Clear();
                MyAPIGateway.Multiplayer.Players.GetPlayers(PlayerList);//Kinda gross...
                if (!MPActive) PlayerList.Add(Session.Player);
                foreach (var player in PlayerList)
                {
                    if (player.Character == null || (MPActive && player.SteamUserId == 0)) continue;
                    var playerPos = player.Character.WorldAABB.Center;
                    if (playerPos == Vector3D.Zero)
                    {
                        MyLog.Default.WriteLineAndConsole($"Player position error - Vector3D.Zero - player.SteamUserId{player.SteamUserId}");
                        continue;
                    }
                var controlledEnt = player.Controller?.ControlledEntity?.Entity?.Parent?.EntityId;

                var tempList = new List<SignalComp>();
                    foreach (var grid in GridList)
                    {
                        var playerGrid = grid.Grid.EntityId == controlledEnt;
                        var gridPos = grid.Grid.PositionComp.WorldAABB.Center;
                        var distToTargSqr = Vector3D.DistanceSquared(playerPos, gridPos);
                        if (playerGrid || distToTargSqr <= grid.broadcastDistSqr)
                        {
                            var signalData = new SignalComp();
                            signalData.position = (Vector3I)gridPos;
                            signalData.range = playerGrid ? grid.broadcastDist : (int)Math.Sqrt(distToTargSqr);
                            signalData.faction = grid.faction;
                            signalData.entityID = grid.Grid.EntityId;
                            signalData.sizeEnum = grid.sizeEnum;
                            tempList.Add(signalData);

                            if(!MPActive)
                            {
                                if (SignalList.ContainsKey(signalData.entityID))
                                {
                                    var updateTuple = new MyTuple<SignalComp, int>(signalData, Tick);
                                    SignalList[signalData.entityID] = updateTuple;
                                }
                                else
                                    SignalList.TryAdd(signalData.entityID, new MyTuple<SignalComp, int>(signalData, Tick));
                            }
                        }
                    }
                    if (MPActive && tempList.Count > 0)
                        Networking.SendToPlayer(new PacketBase(tempList), player.SteamUserId);
                }
                if (!_startBlocks.IsEmpty || !_startGrids.IsEmpty)
                    StartComps();
            }

            //Clientside list processing
            if (Client && Tick % 60 == 0)
            {
                entityIDList.Clear();
                var controlledEnt = MyAPIGateway.Session?.Player?.Controller?.ControlledEntity?.Entity?.Parent;
                if (controlledEnt != null && controlledEnt is MyCubeGrid)//WC Deconflict
                {
                    var myEnt = (MyEntity)controlledEnt;
                    wcAPI.GetSortedThreats(myEnt, threatList);
                    foreach (var item in threatList)
                    {
                        entityIDList.Add(item.Item1.EntityId);
                    }
                    wcAPI.GetObstructions(myEnt, obsList);
                    foreach (var item in obsList)
                    {
                        entityIDList.Add(item.EntityId);
                    }
                }
                foreach (var wcContact in entityIDList)
                {
                    if (SignalList.ContainsKey(wcContact))
                        SignalList.Remove(wcContact);
                }
                //temp sample points
                if (Tick % 600 == 0 && !SignalList.ContainsKey(0) && !SignalList.ContainsKey(1))
                {
                    var temp1 = new MyTuple<SignalComp, int>(new SignalComp() { faction = "Mover (won't fade for a real one)", range = 1234, position = new Vector3I(1000, 2000, 3000), entityID = 0, sizeEnum = 3 }, Tick);
                    var temp2 = new MyTuple<SignalComp, int>(new SignalComp() { faction = "Lost Signal", range = 4567000, position = new Vector3I(11000, 2000, 3000), entityID = 0, sizeEnum = 2 }, Tick);
                    SignalList.TryAdd(0, temp1);
                    SignalList.TryAdd(1, temp2);
                }

                if (SignalList.ContainsKey(2)) SignalList.Remove(2);
                var temp3 = new MyTuple<SignalComp, int>(new SignalComp() { faction = "Norm Update", range = 4567000, position = new Vector3I(101000, 2000, 3000), entityID = 0, sizeEnum = 4 }, Tick);
                SignalList.TryAdd(2, temp3);
                //temp moving point for positional update tests
                if (SignalList.ContainsKey(0)) SignalList[0].Item1.position += new Vector3I(100, 0, 0);
                //end of temp
            }


            if (Server && Tick % 5 == 0 && shutdownList.Count > 0)//5 tick interval to keep players from spamming keys to turn power back on
            {
                foreach (var gridComp in shutdownList.ToArray())
                    gridComp.TogglePower();
            }

        }

        public override void Draw()
        {
            if (Client && hudAPI.Heartbeat && SignalList.Count > 0)
            {
                var viewProjectionMat = Session.Camera.ViewMatrix * Session.Camera.ProjectionMatrix;
                var camPos = Session.Camera.Position;
                var playerEnt = MyAPIGateway.Session?.Player?.Controller?.ControlledEntity?.Entity?.Parent?.EntityId;

                foreach (var signal in SignalList.ToArray())
                {
                    var contact = signal.Value.Item1;
                    if (contact.entityID == playerEnt)
                    {
                        var dispRange = contact.range > 1000 ? (contact.range / 1000f).ToString("0.0") + " km" : contact.range + " m";
                        var info = new StringBuilder("Broadcast Dist: " + dispRange + "\n" + "Size: " + messageList[contact.sizeEnum]);
                        var Label = new HudAPIv2.HUDMessage(info, signalDrawCoords, null, 2, textSize, true, true);
                        //Label.InitialColor = signalColor;
                        Label.Visible = true;
                    }
                    else
                    {
                        var contactAge = Tick - signal.Value.Item2;
                        if (contactAge >= maxContactAge)
                        {
                            SignalList.Remove(signal.Key);
                            continue;
                        }
                        var adjColor = signalColor;
                        if (fadeOutTime > 0)
                        {
                            byte colorFade = (byte)(contactAge < fadeOutTime ? 0 : (contactAge - fadeOutTime) / 2);
                            adjColor.R = (byte)MathHelper.Clamp(signalColor.R - colorFade, 0, 255);
                            adjColor.G = (byte)MathHelper.Clamp(signalColor.G - colorFade, 0, 255);
                            adjColor.B = (byte)MathHelper.Clamp(signalColor.B - colorFade, 0, 255);
                        }

                        var adjustedPos = camPos + Vector3D.Normalize((Vector3D)contact.position - camPos) * viewDist;
                        var screenCoords = Vector3D.Transform(adjustedPos, viewProjectionMat);
                        var offScreen = screenCoords.X > 1 || screenCoords.X < -1 || screenCoords.Y > 1 || screenCoords.Y < -1 || screenCoords.Z > 1;
                        if (!offScreen)
                        {
                            var symbolPosition = new Vector2D(screenCoords.X, screenCoords.Y);
                            var labelPosition = new Vector2D(screenCoords.X + (symbolHeight * 0.4), screenCoords.Y + (symbolHeight * 0.5));
                            var dispRange = contact.range > 1000 ? contact.range / 1000 + " km" : contact.range + " m";
                            var info = new StringBuilder(contact.faction + messageList[contact.sizeEnum] + "\n" + dispRange);
                            var Label = new HudAPIv2.HUDMessage(info, labelPosition, new Vector2D(0, -0.001), 2, textSize, true, true);
                            Label.InitialColor = adjColor;
                            Label.Visible = true;
                            var symbolObj = new HudAPIv2.BillBoardHUDMessage(symbolList[contact.sizeEnum], symbolPosition, adjColor, Width: symbolWidth, Height: symbolHeight, TimeToLive: 2, HideHud: true, Shadowing: true);
                        }
                        else
                        {
                            if (screenCoords.Z > 1)//Camera is between player and target
                                screenCoords *= -1;
                            var vectorToPt = new Vector2D(screenCoords.X, screenCoords.Y);
                            vectorToPt.Normalize();
                            vectorToPt *= offscreenSquish;
                            var vectorToPt2 = vectorToPt * 0.9;//TODO fix this offset (variable space on left vs top edge) or fix by replacing arrow with one that is large enough & offset already

                            var rotation = (float)Math.Atan2(screenCoords.X, screenCoords.Y);
                            var symbolObj = new HudAPIv2.BillBoardHUDMessage(symbolOffscreenArrow, vectorToPt, adjColor, Width: offscreenWidth, Height: offscreenHeight, TimeToLive: 2, Rotation: rotation, HideHud: true, Shadowing: true);
                            var symbolObj2 = new HudAPIv2.BillBoardHUDMessage(symbolList[contact.sizeEnum], vectorToPt, adjColor, Width: symbolWidth, Height: symbolHeight, TimeToLive: 2, HideHud: true, Shadowing: true);
                        }
                    }
                }
            }
        }

        protected override void UnloadData()
        {
            if (Server)
            {
                MyEntities.OnEntityCreate -= OnEntityCreate;
                Clean();
            }
            if (wcAPI != null)
                wcAPI.Unload();
            if (hudAPI != null)
                hudAPI.Unload();
            Networking?.Unregister();
            Networking = null;
            PlayerList.Clear();
            GridList.Clear();
            SignalList.Clear();
            threatList.Clear();
            obsList.Clear();
            shutdownList.Clear();
        }
    }
}
