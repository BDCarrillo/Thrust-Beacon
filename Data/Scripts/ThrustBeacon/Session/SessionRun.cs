using CoreSystems.Api;
using Digi.Example_NetworkProtobuf;
using Draygo.API;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NexusSyncMod;
using VRage.Game;
using VRage.Game.Components;
using VRage.Utils;
using VRageMath;

namespace ThrustBeacon
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public partial class Session : MySessionComponentBase
    {
        internal static int Tick;
        internal bool IsServer;
        internal bool IsClient;
        internal bool IsDedicated;
        internal bool DedicatedServer;
        internal bool MpActive;
        internal bool MpServer;
        internal bool IsHost;
        HudAPIv2 hudAPI;
        WcApi wcAPI;
        public Networking Networking = new Networking(1337); //TODO: Pick a new number based on mod ID
        public NexusAPI NexusApi = new NexusAPI(7331);
        internal MyStringId symbol = MyStringId.GetOrCompute("FrameSignal");
        internal float symbolWidth = 0.02f;
        internal float symbolHeight = 0f;//Leave this as zero, monitor aspect ratio is figured in later
        internal Vector4 color = Color.Red.ToVector4();




        public override void BeforeStart()
        {
            Networking.Register();
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(7331, NexusMessageHandler);

        }

        public override void LoadData()
        {
            IsServer = MyAPIGateway.Multiplayer.MultiplayerActive && MyAPIGateway.Session.IsServer;
            DedicatedServer = MyAPIGateway.Utilities.IsDedicated;
            MpActive = MyAPIGateway.Multiplayer.MultiplayerActive;
            IsClient = !IsServer && !DedicatedServer;
            IsHost = IsServer && !DedicatedServer && MpActive;
            MpServer = IsHost || DedicatedServer || !MpActive;
            if (!IsClient)
            {
                MyEntities.OnEntityCreate += OnEntityCreate;
                //TODO: Hook player joining for server and populate PlayerList?

            }
            else
            {
                hudAPI = new HudAPIv2();
                wcAPI = new WcApi();
                wcAPI.Load();
            }

        }
        public override void UpdateBeforeSimulation()
        {
            if (symbolHeight == 0)
            {
                var aspectRatio = Session.Camera.ViewportSize.X / Session.Camera.ViewportSize.Y;
                symbolHeight = symbolWidth * aspectRatio;
            }

            Tick++;
            if (Tick % 60 == 0 && !IsClient)
            {
                var signalList = new List<SignalComp>();
                foreach (var gridComp in GridList)
                {
                    if (gridComp.thrustList.Count > 0)
                        gridComp.CalcThrust();
                    if (NexusAPI.IsRunningNexus())
                    {
                        // Generate signal list for cross nexus server replication

                        // Optimizations for cross instance broadcasting
                        // Check if grid is owned by NPC and skip if true
                        var owner = gridComp.Grid.BigOwners.FirstOrDefault();
                        if(owner == 0) continue;
                        if (MyAPIGateway.Players.TryGetSteamId(owner) == 0) continue;
                        //Cull small ranges with practically zero chance of being seen
                        if (gridComp.broadcastDist <= 50) continue; 

                        var gridPos = gridComp.Grid.PositionComp.WorldAABB.Center;

                        // Cull grids whose broadcast radius wont cross instance boundaries
                        var sector = NexusAPI.GetSectors().FirstOrDefault(s => s.ServerID == NexusAPI.GetThisServer().ServerID);
                        if(sector == null) continue;
                        if(Vector3D.DistanceSquared(sector.Center, gridPos) + gridComp.broadcastDistSqr < sector.Radius * sector.Radius) continue; 

                        var signalData = new SignalComp
                        {
                            position = gridPos,
                            range = gridComp.broadcastDist,
                            message = gridComp.broadcastMsg,
                            entityID = gridComp.Grid.EntityId
                        };
                        signalList.Add(signalData);
                    }
                }

                if (NexusAPI.IsRunningNexus() && signalList.Count > 0)
                {
                    var message = MyAPIGateway.Utilities.SerializeToBinary(signalList);
                    NexusApi.SendMessageToAllServers(message);
                    signalList.Clear();
                }

                //Find player controlled entities in range and broadcast to them
                foreach (var player in PlayerList)
                {
                    var playerPos = player.GetPosition();
                    var playerSteamID = player.SteamUserId;
                    if (playerPos == null || playerPos == Vector3D.Zero || playerSteamID == 0) continue;
                    var tempList = new List<SignalComp>();
                    foreach (var grid in GridList)
                    {
                        if (grid.broadcastDist <= 50) continue; //Cull short ranges with practically zero chance of being seen
                        var gridPos = grid.Grid.PositionComp.WorldAABB.Center;
                        if (Vector3D.DistanceSquared(playerPos, gridPos) <= grid.broadcastDistSqr)
                        {
                            var signalData = new SignalComp
                            {
                                position = gridPos,
                                range = (int)(Vector3D.Distance(playerPos, gridPos)),
                                message = grid.broadcastMsg,
                                entityID = grid.Grid.EntityId
                            };
                            tempList.Add(signalData);
                        }
                    }
                    // Check received list for signal data to send to player
                    if (NexusAPI.IsRunningNexus() && ReceivedSignalList.Count > 0)
                    {
                        foreach (var signal in ReceivedSignalList)
                        {
                            if (Vector3D.DistanceSquared(playerPos, signal.position) <= signal.range * signal.range)
                            {
                                var newSignal = new SignalComp
                                {
                                    position = signal.position,
                                    range = (int)Vector3D.Distance(playerPos, signal.position),
                                    message = signal.message,
                                    entityID = signal.entityID,
                                };
                                tempList.Add(newSignal);
                            }
                        }
                    }

                    if (tempList.Count > 0)
                    {
                        // Send to players within the server
                        Networking.SendToPlayer(new PacketBase(tempList), playerSteamID);
                        ReceivedSignalList.Clear();
                    }
                }
                if (!_startBlocks.IsEmpty || !_startGrids.IsEmpty)
                    StartComps();              
            }
            else if (Tick % 60 == 0 && IsClient)
            {
                //TODO: Client side list filtering to deconflict items in WC range
                //Add desired signals to DrawList

                //temp force feeding without filtering and sample points
                DrawList.Clear();
                foreach (var temp in SignalList)
                    DrawList.Add(temp);
                var temp1 = new SignalComp() { message = "Test1", range = 1234, position = new Vector3D(1000,2000,3000), entityID = 0 };
                var temp2 = new SignalComp() { message = "Test2", range = 4567000, position = new Vector3D(11000, 2000, 3000), entityID = 0 };
                DrawList.Add(temp1);
                DrawList.Add(temp2);
                SignalList.Clear();
            }
        }

        public override void Draw()
        {
            if (IsClient && hudAPI.Heartbeat)
            {
                foreach (var signal in DrawList)
                {
                    var varPos = signal.position;
                    var screenCoords = Session.Camera.WorldToScreen(ref varPos);
                    if (screenCoords.Z >= 1) continue; //TODO: Signal is off screen

                    var symbolPosition = new Vector2D(screenCoords.X, screenCoords.Y);
                    var labelPosition = new Vector2D(screenCoords.X + (symbolHeight * 0.4), screenCoords.Y + (symbolHeight * 0.5));
                    var dispRange = signal.range > 1000 ? signal.range / 1000 + " km" : signal.range + " m";
                    var info = new StringBuilder(signal.message + "\n" + dispRange);
                    var Label = new HudAPIv2.HUDMessage(info, labelPosition, new Vector2D(0,-0.001), 2, 1, true, true);
                    Label.InitialColor = Color.Red;
                    Label.Visible = true;
                    var symbolObj = new HudAPIv2.BillBoardHUDMessage(symbol, symbolPosition, Color.Red, Width: symbolWidth, Height: symbolHeight, TimeToLive: 2);
               
                }
            }
        }

        // Received message from another server instance add to list of signals to be checked
        private void NexusMessageHandler(ushort handlerId, byte[] message, ulong steamId, bool isServer)
        {
            // Process messages from other servers in parallel
            MyAPIGateway.Parallel.Start(() =>
            {
                var signalList = MyAPIGateway.Utilities.SerializeFromBinary<PacketBase>(message);
                foreach (var signal in signalList.signalData)
                {
                    ReceivedSignalList.Add(signal);
                }

                //foreach (var player in PlayerList)
                //{
                //    var playerPos = player.GetPosition();
                //    var playerSteamID = player.SteamUserId;
                //    if (playerPos == null || playerPos == Vector3D.Zero || playerSteamID == 0) continue;
                //    var tempList = new List<SignalComp>();
                //    foreach (var signal in signalList.signalData)
                //    {
                //        if (Vector3D.DistanceSquared(playerPos, signal.position) < signal.range * signal.range)
                //        {
                //            tempList.Add(signal);
                //        }

                //        if (tempList.Count > 0)
                //        {
                //            // Send to players within the server
                //            Networking.SendToPlayer(new PacketBase(tempList), playerSteamID);
                //        }
                //    }
                //}
            });
        }

        protected override void UnloadData()
        {
            if (MpServer)
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
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(7331, NexusMessageHandler);
            PlayerList.Clear();
            GridList.Clear();
            SignalList.Clear();
            DrawList.Clear();
        }
    }
}
