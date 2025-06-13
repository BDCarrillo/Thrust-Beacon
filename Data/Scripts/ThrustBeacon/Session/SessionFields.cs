using CoreSystems.Api;
using DefenseShields;
using Digi.Example_NetworkProtobuf;
using Draygo.API;
using Sandbox.ModAPI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using VRage;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace ThrustBeacon
{
    public partial class Session : MySessionComponentBase
    {
        //Generic
        internal static int Tick;
        internal bool Client;
        internal bool Server;
        internal bool MPActive;
        internal static HudAPIv2 hudAPI;
        internal static WcApi wcAPI;
        internal static ShieldApi dsAPI;
        public static Networking Networking = new Networking(1212);
        public static Random rand = new Random();
        internal static string ModName = "[Thrust Beacon]";
        internal static List<string> messageList = new List<string>() { "Idle Sig", "Small Sig", "Medium Sig", "Large Sig", "Huge Sig", "Massive Sig", "OVERHEAT - SHUTDOWN" };

        //Client specific
        internal MyStringId symbol = MyStringId.GetOrCompute("FrameSignal");
        internal MyStringId symbolOffscreenArrow = MyStringId.GetOrCompute("ArrowOffset");
        internal MyStringId symbolOffscreen = MyStringId.GetOrCompute("Arrow");
        internal List<MyStringId> symbolList = new List<MyStringId>(){MyStringId.GetOrCompute("IdleSignal"), MyStringId.GetOrCompute("SmallSignal"), MyStringId.GetOrCompute("MediumSignal"),
        MyStringId.GetOrCompute("LargeSignal"), MyStringId.GetOrCompute("HugeSignal"), MyStringId.GetOrCompute("MassiveSignal"), MyStringId.GetOrCompute("MassiveSignal")}; //TODO unique symbol for overheat/shutdown?
        internal static float symbolHeight = 0f;//Leave this as zero, monitor aspect ratio is figured in later
        internal float aspectRatio = 0f;//Leave this as zero, monitor aspect ratio is figured in later
        internal Vector2D offscreenSquish = new Vector2D(0.9, 0.7);//Pulls X in a little, flattens Y to not overlap hotbar
        internal int viewDist = 0;
        internal static float offscreenHeight = 0f;
        internal static ConcurrentDictionary<long, MyTuple<SignalComp, int>> SignalList = new ConcurrentDictionary<long, MyTuple<SignalComp, int>>();
        internal static int fadeTimeTicks = 0;
        internal static int stopDisplayTimeTicks = 0;
        internal static int keepTimeTicks = 0;
        internal static bool clientActionRegistered = false;
        internal string primaryBeaconLabel = "[PRI]";
        internal IMyBeacon primaryBeacon;
        internal int clientLastBeaconDist = 0;
        internal int clientLastBeaconSizeEnum = 0;
        internal static bool clientUpdateBeacon = false;
        internal static bool logging = true;
        internal static List<long> entityIDList = new List<long>();
        internal int lastLogRequestTick = 0;
        internal static bool firstLoad = false;
        internal long controlledGridParent = 0;
        internal static HudAPIv2.HUDMessage ownShipLabel = null;

        //Server specific
        internal static readonly List<MyStringHash> weaponSubtypeIDs = new List<MyStringHash>();
        internal static readonly Dictionary<string, int> SignalProducer = new Dictionary<string, int>();
        internal static readonly Dictionary<MyStringHash, BlockConfig> BlockConfigs = new Dictionary<MyStringHash, BlockConfig>();
        internal List<IMyPlayer> PlayerList = new List<IMyPlayer>();
        internal static List<GroupComp> thrustshutdownList = new List<GroupComp>();
        internal static List<GroupComp> powershutdownList = new List<GroupComp>();
        internal static Dictionary<IMyGridGroupData, GroupComp> GroupDict = new Dictionary<IMyGridGroupData, GroupComp>();
        internal static Dictionary<string, ulong> ReadyLogs = new Dictionary<string, ulong>();
        internal static List<long> npcFactions = new List<long>();
        internal static bool serverDefaults = false;
        internal List<BoundingSphereD> planetSpheres = new List<BoundingSphereD>();
        internal HashSet<IMyEntity> entityHash = new HashSet<IMyEntity>();
        internal long combineDistSqr;
        internal bool useCombine;
        internal ApiBackend Api;
        internal bool PbApiInited;
        internal bool PbActivate;
        internal Dictionary<IMyTerminalBlock, int> PbDict = new Dictionary<IMyTerminalBlock, int>();
        internal List<int> removalList = new List<int>();


        private void Clean()
        {
            SignalProducer.Clear();
            BlockConfigs.Clear();
            GroupDict.Clear();
            ReadyLogs.Clear();
            planetSpheres.Clear();
            PbDict.Clear();
        }
    }
}
