using ProtoBuf;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using VRage.Utils;

namespace ThrustBeacon
{
    [ProtoContract]
    public class ServerSettings
    {
        public static ServerSettings Instance;
        public static readonly ServerSettings Default = new ServerSettings()
        {
            IncludePowerInSignal = true,
            DefaultPowerDivisor = 120000, //Default power divisor if no other value is specified.  Yields 2.5km of signal per LG large reactor
            IncludeThrustInSignal = true,
            DefaultThrustDivisor = 600, //Default thrust divisor if no other value is specified
            LargeGridCooldownRate = 0.95f, //Previous signal is multiplied by this per 59 tick cycle (unless freshly calc'd value is > than old value)
            SmallGridCooldownRate = 0.85f, //Same as above
            ShutdownPowerOverMaxSignal = true,
            MaxSignalforPowerShutdown = 500000,
            ShutdownThrustersOverMaxSignal = true,
            MaxSignalforThrusterShutdown = 500000,
            IncludeWeaponHeatInSignal = true,
            DefaultWeaponHeatDivisor = 1, //Explore further and find a good starting point for this value
            SendSignalDataToSuits = false, //If false, characters outside of grids will not get beacon updates
            IncludeShieldHPInSignal = true,
            DefaultShieldHPDivisor = 50,
            UpdateBeaconOnControlledGrid = false,
            EnableDataMasking = false,
            DataMaskingRange = 0.75f,
            CombineRange = 5000,
            CombineBeyond = 75000,
            CombineIncrementSize = true,
            CombineIncludeQuantity = false,
            EnablePlanetOcclusion = true,
            EnableBroadcastMirroringForNPCs = false,
            EnableBroadcastMirroring = false,
            IncludeMassInSignal = false,
            DefaultMassDivisor = 20, 
        };
        [ProtoMember(1)]
        public bool IncludePowerInSignal { get; set; } = true;
        [ProtoMember(2)]
        public int DefaultPowerDivisor { get; set; } = 120000;
        [ProtoMember(3)]
        public bool IncludeThrustInSignal { get; set; } = true;
        [ProtoMember(4)]
        public int DefaultThrustDivisor { get; set; } = 600;
        [ProtoMember(5)]
        public float LargeGridCooldownRate { get; set; } = 0.95f;
        [ProtoMember(6)]
        public float SmallGridCooldownRate { get; set; } = 0.85f;
        [ProtoMember(7)]
        public bool ShutdownPowerOverMaxSignal { get; set; } = true;
        [ProtoMember(8)]
        public double MaxSignalforPowerShutdown { get; set; } = 500000;
        [ProtoMember(9)]
        public bool ShutdownThrustersOverMaxSignal { get; set; } = true;
        [ProtoMember(10)]
        public double MaxSignalforThrusterShutdown { get; set; } = 500000;
        [ProtoMember(11)]
        public bool IncludeWeaponHeatInSignal { get; set; } = true;
        [ProtoMember(12)]
        public double DefaultWeaponHeatDivisor { get; set; } = 1;
        [ProtoMember(13)]
        public bool SendSignalDataToSuits { get; set; } = false;
        [ProtoMember(14)]
        public bool IncludeShieldHPInSignal { get; set; } = true;
        [ProtoMember(15)]
        public int DefaultShieldHPDivisor { get; set; } = 50;
        [ProtoMember(16)]
        public int Distance1 { get; set; } = 2500;
        [ProtoMember(17)]
        public int Distance2 { get; set; } = 100000;
        [ProtoMember(18)]
        public int Distance3 { get; set; } = 200000;
        [ProtoMember(19)]
        public int Distance4 { get; set; } = 300000;
        [ProtoMember(20)]
        public int Distance5 { get; set; } = 400000;
        [ProtoMember(21)]
        public string Label1 { get; set; } = "Idle Sig";
        [ProtoMember(22)]
        public string Label2 { get; set; } = "Small Sig";
        [ProtoMember(23)]
        public string Label3 { get; set; } = "Medium Sig";
        [ProtoMember(24)]
        public string Label4 { get; set; } = "Large Sig";
        [ProtoMember(25)]
        public string Label5 { get; set; } = "Huge Sig";
        [ProtoMember(26)]
        public string Label6 { get; set; } = "Massive Sig";
        [ProtoMember(27)]
        public string LabelShutdown { get; set; } = "OVERHEAT - SHUTDOWN";
        [ProtoMember(28)]
        public bool UpdateBeaconOnControlledGrid { get; set; } = false;
        [ProtoMember(29)]
        public bool SuppressShutdownForNPCs { get; set; } = false;
        [ProtoMember(30)]
        public bool SuppressSignalForNPCs { get; set; } = false;
        [ProtoMember(31)]
        public bool EnableDataMasking { get; set; } = false;
        [ProtoMember(32)]
        public float DataMaskingRange { get; set; } = 0.75f;
        [ProtoMember(33)]
        public int CombineRange { get; set; } = 5000;
        [ProtoMember(34)]
        public int CombineBeyond { get; set; } = 75000;
        [ProtoMember(35)]
        public bool CombineIncrementSize { get; set; } = true;
        [ProtoMember(36)]
        public bool CombineIncludeQuantity { get; set; } = false;
        [ProtoMember(37)]
        public bool EnablePlanetOcclusion { get; set; } = true;
        [ProtoMember(38)]
        public bool EnableBroadcastMirroringForNPCs { get; set; } = false;
        [ProtoMember(39)]
        public bool EnableBroadcastMirroring { get; set; } = false;
        [ProtoMember(40)]
        public bool IncludeMassInSignal { get; set; } = false;
        [ProtoMember(41)]
        public int DefaultMassDivisor { get; set; } = 20;
    }
    public partial class Session
    {
        private void InitServerConfig()
        {
            ServerSettings s = ServerSettings.Default;
            var Filename = "ServerConfig.cfg";
            var localFileExists = MyAPIGateway.Utilities.FileExistsInWorldStorage(Filename, typeof(ServerSettings));
            if(localFileExists)
            {
                TextReader reader = null;
                try
                {
                    reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(Filename, typeof(ServerSettings));
                    string text = reader.ReadToEnd();
                    reader.Close();
                    s = MyAPIGateway.Utilities.SerializeFromXML<ServerSettings>(text);
                    ServerSettings.Instance = s;
                    MyLog.Default.WriteLineAndConsole(ModName + "Loaded server config");
                    SaveServer(ServerSettings.Instance);
                }
                catch (Exception e)
                {
                    if (reader != null) reader.Close();
                    MyLog.Default.WriteLineAndConsole(ModName + "Server config read error, writing default file - " + e.InnerException);
                    s = ServerSettings.Default;
                    SaveServer(s);
                }
            }
            else
            {
                s = ServerSettings.Default;
                SaveServer(s);
            }
        }
        public void SaveServer(ServerSettings settings)
        {
            //SP writes to local variables, handled by packets w/ networking in MP
            messageList = new List<string>() {settings.Label1, settings.Label2, settings.Label3, settings.Label4, settings.Label5, settings.Label6, settings.LabelShutdown};
            clientUpdateBeacon = settings.UpdateBeaconOnControlledGrid;
            combineDistSqr = settings.CombineBeyond * settings.CombineBeyond;
            useCombine = settings.CombineBeyond > 0 && settings.CombineRange > 0;
            var Filename = "ServerConfig.cfg";
            TextWriter writer;
            writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(Filename, typeof(ServerSettings));
            writer.Write(MyAPIGateway.Utilities.SerializeToXML(settings));
            writer.Close();
            ServerSettings.Instance = settings;
            MyLog.Default.WriteLineAndConsole(ModName + "Saved server config");
        }
    }
}

