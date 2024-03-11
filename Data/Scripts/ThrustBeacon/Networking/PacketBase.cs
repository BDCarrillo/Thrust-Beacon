﻿using ProtoBuf;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System.Collections.Generic;
using ThrustBeacon;
using VRage;
using VRage.Game.Entity;
using VRage.Utils;

namespace Digi.Example_NetworkProtobuf
{
    [ProtoInclude(1000, typeof(PacketSettings))]
    [ProtoInclude(2000, typeof(PacketSignals))]
    [ProtoInclude(3000, typeof(PacketStatsRequest))]
    [ProtoInclude(4000, typeof(PacketStatsSend))]
    [ProtoContract]
    public abstract class PacketBase
    {
        //[ProtoMember(1)]
        //public ulong SenderId;
        public PacketBase() { } // Empty constructor required for deserialization
        //public PacketBase()
        //{
        //    SenderId = MyAPIGateway.Multiplayer.MyId;
        //}
        public abstract bool Received();
    }

    [ProtoContract]
    public partial class PacketSignals : PacketBase
    {
        [ProtoMember(100)]
        public List<SignalComp> signalData;
        public PacketSignals() { } // Empty constructor required for deserialization
        public PacketSignals(List<SignalComp> signaldata)
        {
            signalData = signaldata;
        }
        public override bool Received()
        {
            //Clientside addition or update of signal data
            foreach (var signalRcvd in signalData)
            {
                if (Session.SignalList.ContainsKey(signalRcvd.entityID)) //Client already had this in their list, update data
                    Session.SignalList[signalRcvd.entityID] = new MyTuple<SignalComp, int>(signalRcvd, Session.Tick);
                else //This contact is new to the client
                    Session.SignalList.TryAdd(signalRcvd.entityID, new MyTuple<SignalComp, int>(signalRcvd, Session.Tick));
            }
            return false;
        }
    }

    [ProtoContract]
    public partial class PacketSettings : PacketBase
    {
        [ProtoMember(200)]
        public List<string> Labels;
        [ProtoMember(201)]
        public bool ClientBeacon;
        public PacketSettings() { } // Empty constructor required for deserialization
        public PacketSettings(List<string> labels, bool clientBeacon)
        {
            Labels = labels;
            ClientBeacon = clientBeacon;
        }
        public override bool Received()
        {
            Session.messageList = Labels;
            Session.clientUpdateBeacon = ClientBeacon;
            MyLog.Default.WriteLineAndConsole($"{Session.ModName}: Received server label list");
            Session.clientActionRegistered = false; //Reset for Seamless purposes
            Session.SignalList.Clear(); //Reset for Seamless purposes
            return false;
        }
    }

    [ProtoContract]
    public partial class PacketStatsRequest : PacketBase
    {
        [ProtoMember(300)]
        public ulong PlayerID;
        [ProtoMember(301)]
        public long EntityID;
        public PacketStatsRequest() { } // Empty constructor required for deserialization
        public PacketStatsRequest(ulong playerID, long entityID)
        {
            PlayerID = playerID;
            EntityID = entityID;
        }
        public override bool Received()
        {
            MyLog.Default.WriteLineAndConsole($"{Session.ModName}: Server received logging request");
            GroupComp logGroupComp = null;
            foreach(var group in Session.GroupDict)
            {
                foreach(var grid in group.Value.GridDict)
                {
                    if (grid.Key.EntityId == EntityID)
                    {
                        group.Value.groupLogging = true;
                        group.Value.groupLogRequestor = PlayerID;
                        logGroupComp = group.Value;
                        break;
                    }
                }
            }

            if(logGroupComp == null)
                MyLog.Default.WriteLineAndConsole($"{Session.ModName}: Server could not match grid entity ID");
            return false;
        }
    }
    [ProtoContract]
    public partial class PacketStatsSend : PacketBase
    {
        [ProtoMember(400)]
        public string Log;

        public PacketStatsSend() { } // Empty constructor required for deserialization
        public PacketStatsSend(string log)
        {
            Log = log;
        }
        public override bool Received()
        {
            MyAPIGateway.Utilities.ShowMissionScreen("Signal Report", "", "", Log, null, "Close");
            MyLog.Default.WriteLineAndConsole($"{Session.ModName}: Received log from server");
            return false;
        }
    }
}
