using CoreSystems.Api;
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
using Sandbox.Game;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRage;
using VRage.ModAPI;

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
                MyAPIGateway.Entities.GetEntities(entityHash, CheckPlanets);
                entityHash.Clear();
                var groupStartList = new List<IMyGridGroupData>();
                MyAPIGateway.GridGroups.GetGridGroups(GridLinkTypeEnum.Mechanical, groupStartList);
                foreach(var group in groupStartList)
                    GridGroupsOnOnGridGroupCreated(group);

                //Load NPC faction list
                Session.Factions.FactionCreated += FactionCreated;
                foreach(var faction in Session.Factions.Factions)
                    FactionCreated(faction.Key);
                //Hook connection event to send label list
                MyVisualScriptLogicProvider.PlayerConnected += PlayerConnected;
                MyEntities.OnEntityCreate += OnEntityCreate;
            }
            if(Client)
            {
                MyAPIGateway.Utilities.MessageEnteredSender += OnMessageEnteredSender;
            }

            //Init WC and register all defs on callback
            wcAPI = new WcApi();
            wcAPI.Load(RegisterWCDefs, true);
        }

        private bool CheckPlanets(IMyEntity entity)
        {
            if(entity is MyPlanet)
            {
                var planet = (MyPlanet)entity;
                
                var sphere = new BoundingSphereD(planet.PositionComp.WorldVolume.Center, planet.AverageRadius + planet.AtmosphereAltitude);
                planetSpheres.Add(sphere);
                MyLog.Default.WriteLine($"{ModName} Planet added {planet.Name} {planet.MinimumRadius} {planet.AverageRadius} {planet.MaximumRadius} {planet.AtmosphereRadius} {planet.AtmosphereAltitude}");
            }
            return false;
        }

        public override void LoadData()
        {
            MPActive = MyAPIGateway.Multiplayer.MultiplayerActive;
            Server = (MPActive && MyAPIGateway.Multiplayer.IsServer) || !MPActive;
            Client = (MPActive && !MyAPIGateway.Utilities.IsDedicated) || !MPActive;
            if (Client)
            {
                InitConfig();
                hudAPI = new HudAPIv2(InitMenu);
                viewDist = Math.Min(Session.SessionSettings.SyncDistance, Session.SessionSettings.ViewDistance);
            }
            if (Server)
            {
                dsAPI = new ShieldApi();
                dsAPI.Load();
                LoadSignalProducerConfigs(); //Blocks that generate signal (thrust, power)
                LoadBlockConfigs(); //Blocks that alter targeting
                InitServerConfig(); //Overall settings
                if (MyAPIGateway.Utilities.IsDedicated)
                    InitDefaults();
            }
        }

        public override void UpdateBeforeSimulation()
        {
            Tick++;
            if (Client)
                ClientTasks();

            if (Server)
            {
                if (Tick % 119 == 0 && PbActivate && !PbApiInited)
                    Api.PbInit();

                if (Tick % 300 == 0) 
                    ServerUpdatePlayers();
                if (Tick % 5 == 0)
                {
                    if (powershutdownList.Count > 0)
                        ServerPowerShutdown();
                    if (thrustshutdownList.Count > 0)
                        ServerThrustShutdown();
                }
                ServerUpdateGroups();
                if (ReadyLogs.Count > 0)
                    ServerSendLogs();
                ServerMainLoop();
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
                MyVisualScriptLogicProvider.PlayerConnected -= PlayerConnected;
                Session.Factions.FactionCreated -= FactionCreated;
            }
            if (Client)
            {
                Save(Settings.Instance);
                if (clientActionRegistered)
                {
                    clientActionRegistered = false;
                    Session.Player.Controller.ControlledEntityChanged -= GridChange;
                }
                if (primaryBeacon != null)
                    Beacon_OnClosing(primaryBeacon);
                MyAPIGateway.Utilities.MessageEnteredSender -= OnMessageEnteredSender;
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
