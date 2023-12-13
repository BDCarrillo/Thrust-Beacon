﻿using CoreSystems.Api;
using DefenseShields;
using Digi.Example_NetworkProtobuf;
using Draygo.API;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using VRage;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace ThrustBeacon
{
    public partial class Session : MySessionComponentBase
    {
        internal static int Tick;
        internal bool Client;
        internal bool Server;
        internal bool MPActive;
        internal static HudAPIv2 hudAPI;
        internal static WcApi wcAPI;
        internal static ShieldApi dsAPI;
        public Networking Networking = new Networking(1212); //TODO: Pick a new number based on mod ID
        internal MyStringId symbol = MyStringId.GetOrCompute("FrameSignal");
        internal MyStringId symbolOffscreenArrow = MyStringId.GetOrCompute("ArrowOffset");
        internal MyStringId symbolOffscreen = MyStringId.GetOrCompute("Arrow");
        internal List<MyStringId> symbolList = new List<MyStringId>(){MyStringId.GetOrCompute("IdleSignal"), MyStringId.GetOrCompute("SmallSignal"), MyStringId.GetOrCompute("MediumSignal"),
        MyStringId.GetOrCompute("LargeSignal"), MyStringId.GetOrCompute("HugeSignal"), MyStringId.GetOrCompute("MassiveSignal"), MyStringId.GetOrCompute("MassiveSignal")}; //TODO unique symbol for overheat/shutdown?
        internal List<string> messageList = new List<string>() {"Idle Sig", "Small Sig", "Medium Sig", "Large Sig", "Huge Sig", "Massive Sig", "OVERHEAT - SHUTDOWN"};
        internal static float symbolHeight = 0f;//Leave this as zero, monitor aspect ratio is figured in later
        internal float aspectRatio = 0f;//Leave this as zero, monitor aspect ratio is figured in later
        internal Vector2D offscreenSquish = new Vector2D(0.9, 0.7);//Pulls X in a little, flattens Y to not overlap hotbar
        internal int viewDist = 0;
        internal static float offscreenHeight = 0f;
        internal static readonly List<MyStringHash> weaponSubtypeIDs = new List<MyStringHash>();
        internal static readonly Dictionary<string, int> SignalProducer = new Dictionary<string, int>();
        internal static readonly Dictionary<MyStringHash, BlockConfig> BlockConfigs = new Dictionary<MyStringHash, BlockConfig>();
        internal List<IMyPlayer> PlayerList = new List<IMyPlayer>();
        internal static ConcurrentDictionary<long, MyTuple<SignalComp, int>> SignalList = new ConcurrentDictionary<long, MyTuple<SignalComp, int>>();
        internal static List<long> entityIDList = new List<long>();
        internal static List<GroupComp> thrustshutdownList = new List<GroupComp>();
        internal static List<GroupComp> powershutdownList = new List<GroupComp>();
        internal static Dictionary<IMyGridGroupData, GroupComp> GroupDict = new Dictionary<IMyGridGroupData, GroupComp>();
        internal static int fadeTimeTicks = 0;
        internal static int stopDisplayTimeTicks = 0;
        internal static int keepTimeTicks = 0;
        internal bool clientActionRegistered = false;
        Random rand = new Random();
        internal string ModName = "[Thrust Beacon]"; //Since I may change the name, this is used in logging

        private void Clean()
        {
            BlockConfigs.Clear();
            SignalProducer.Clear();
            weaponSubtypeIDs.Clear();
            GroupDict.Clear();
        }
    }
}
