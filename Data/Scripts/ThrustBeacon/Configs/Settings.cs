using ProtoBuf;
using Draygo.API;
using Sandbox.ModAPI;
using System.IO;
using VRageMath;
using VRage.Utils;

namespace ThrustBeacon
{
    [ProtoContract]
    public class Settings
    {
        public static Settings Instance;
        public static readonly Settings Default = new Settings()
        {
            friendColor = Color.Green,
            enemyColor = Color.Red,
            neutralColor = Color.White,
            symbolWidth = 0.04f,
            offscreenWidth = 0.06f,
            fadeOutTime = 2d,
            stopDisplayTime = 8d,
            keepTime = 10d,
            textSize = 1f,
            textSizeOwn = 1f,
            signalDrawCoords = new Vector2D(-0.7, -0.625),
            newTime = 2d,
            hideDistance = 1000,
            hideWC = true,
        };

        [ProtoMember(1)]
        public Color friendColor { get; set; }

        [ProtoMember(2)]
        public float symbolWidth { get; set; }

        [ProtoMember(3)]
        public float offscreenWidth { get; set; }

        [ProtoMember(4)]
        public double fadeOutTime { get; set; }

        [ProtoMember(5)]
        public double stopDisplayTime { get; set; }

        [ProtoMember(6)]
        public float textSize { get; set; }

        [ProtoMember(7)]
        public float textSizeOwn { get; set; }

        [ProtoMember(8)]
        public Vector2D signalDrawCoords { get; set; }

        [ProtoMember(9)]
        public Color enemyColor { get; set; }

        [ProtoMember(10)]
        public Color neutralColor { get; set; }

        [ProtoMember(11)]
        public double keepTime { get; set; }

        [ProtoMember(12)]
        public double newTime { get; set; }
        
        [ProtoMember(13)]
        public int hideDistance { get; set; }

        [ProtoMember(14)]
        public bool hideWC { get; set; }



    }
    public partial class Session
    {
        private void InitConfig()
        {
            Settings s = Settings.Default;
            var Filename = "Config.cfg";
            if (MyAPIGateway.Utilities.FileExistsInLocalStorage(Filename, typeof(Settings)))
            {
                TextReader reader = MyAPIGateway.Utilities.ReadFileInLocalStorage(Filename, typeof(Settings));
                try
                {
                    string text = reader.ReadToEnd();
                    s = MyAPIGateway.Utilities.SerializeFromXML<Settings>(text);
                    Settings.Instance = s;
                }
                catch
                {
                    MyAPIGateway.Utilities.ShowMessage(ModName, "Error reading client config file, using defaults");
                    MyLog.Default.WriteLineAndConsole(ModName + "Error reading client config file, using defaults");
                    s = Settings.Default;
                    Save(s);
                }
                reader.Close();
            }
            else
            {
                firstLoad = true;
                s = Settings.Default;
                Save(s);
                MyLog.Default.WriteLineAndConsole(ModName + "Saved default client config");
            }
            fadeTimeTicks = (int)(s.fadeOutTime * 60);
            stopDisplayTimeTicks = (int)(s.stopDisplayTime * 60);
            keepTimeTicks = (int)(s.keepTime * 3600);
            MyLog.Default.WriteLineAndConsole(ModName + "Initialized client config");
        }

        public static void Save(Settings settings)
        {
            var Filename = "Config.cfg";
            TextWriter writer;
            writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(Filename, typeof(Settings));
            writer.Write(MyAPIGateway.Utilities.SerializeToXML(settings));
            writer.Close();
            Settings.Instance = settings;
        }
        HudAPIv2.MenuRootCategory SettingsMenu;
        HudAPIv2.MenuSubCategory MoveSignalDisplay, OwnTextSize, TextSize, SymbolSize;
        HudAPIv2.MenuItem MoveLeft, MoveRight, MoveUp, MoveDown, MoveReset, Reset, Blank, IncreaseOwn, DecreaseOwn, IncreaseText, DecreaseText, IncreaseSymbol, DecreaseSymbol, HideWC;
        HudAPIv2.MenuColorPickerInput FriendColor, EnemyColor, NeutralColor;
        HudAPIv2.MenuTextInput FadeTime, StopDisplayTime, KeepTime, HideDist;


        private void InitMenu()
        {
            SettingsMenu = new HudAPIv2.MenuRootCategory("Thrust Signal", HudAPIv2.MenuRootCategory.MenuFlag.PlayerMenu, "Thrust Signal Settings");
            FriendColor = new HudAPIv2.MenuColorPickerInput("Select Friendly Signal Color >>", SettingsMenu, Settings.Instance.friendColor, "Select Color", ColorFriend);
            EnemyColor = new HudAPIv2.MenuColorPickerInput("Select Enemy Signal Color >>", SettingsMenu, Settings.Instance.enemyColor, "Select Color", ColorEnemy);
            NeutralColor = new HudAPIv2.MenuColorPickerInput("Select Neutral Signal Color >>", SettingsMenu, Settings.Instance.neutralColor, "Select Color", ColorNeutral);

            SymbolSize = new HudAPIv2.MenuSubCategory("Adjust Symbol Size >>", SettingsMenu, "Adjust Symbol Size");
                IncreaseSymbol = new HudAPIv2.MenuItem("Increase symbol size", SymbolSize, IncreaseSymSz);
                DecreaseSymbol = new HudAPIv2.MenuItem("Decrease symbol size", SymbolSize, DecreaseSymSz);

            TextSize = new HudAPIv2.MenuSubCategory("Adjust Label Text Size >>", SettingsMenu, "Adjust Label Text Size");
                IncreaseText = new HudAPIv2.MenuItem("Increase text size", TextSize, IncreaseTextSz);
                DecreaseText = new HudAPIv2.MenuItem("Decrease text size", TextSize, DecreaseTextSz);

            OwnTextSize = new HudAPIv2.MenuSubCategory("Adjust Broadcast Info Size >>", SettingsMenu, "Adjust Broadcast Info Size");
                IncreaseOwn = new HudAPIv2.MenuItem("Increase text size", OwnTextSize, IncreaseOwnSz);
                DecreaseOwn = new HudAPIv2.MenuItem("Decrease text size", OwnTextSize, DecreaseOwnSz);

            MoveSignalDisplay = new HudAPIv2.MenuSubCategory("Move Broadcast Info Location >>", SettingsMenu, "Broadcast Info Location");
                MoveLeft = new HudAPIv2.MenuItem("Move Left", MoveSignalDisplay, LeftMove);
                MoveRight = new HudAPIv2.MenuItem("Move Right", MoveSignalDisplay, RightMove);
                MoveUp = new HudAPIv2.MenuItem("Move Up", MoveSignalDisplay, UpMove);
                MoveDown = new HudAPIv2.MenuItem("Move Down", MoveSignalDisplay, DownMove);
                MoveReset = new HudAPIv2.MenuItem("Reset Position", MoveSignalDisplay, ResetMove);
            Blank = new HudAPIv2.MenuItem("- - - - - - - - - - -", SettingsMenu, null);
            HideDist = new HudAPIv2.MenuTextInput("Hide signals within " + Settings.Instance.hideDistance + "m", SettingsMenu, "Hide signals closer than distance provided below (in meters)", HideDistChange);
            HideWC = new HudAPIv2.MenuItem("Suppress signals if detected by WC: " + Settings.Instance.hideWC, SettingsMenu, HideWCChange);

            FadeTime = new HudAPIv2.MenuTextInput("Fade out after " + Settings.Instance.fadeOutTime + " seconds", SettingsMenu, "Time to wait before fading out contact (in seconds)", FadeTimeAdj);
            StopDisplayTime = new HudAPIv2.MenuTextInput("Stop displaying after " + Settings.Instance.stopDisplayTime + " seconds", SettingsMenu, "Stop drawing contacts that have not updated (in seconds)", StopTimeAdj);
            KeepTime = new HudAPIv2.MenuTextInput("Purge signal record after " + Settings.Instance.keepTime + " minutes", SettingsMenu, "Purge signal record without updates (in minutes)", KeepTimeAdj);

            Blank = new HudAPIv2.MenuItem("- - - - - - - - - - -", SettingsMenu, null);

            Reset = new HudAPIv2.MenuItem("Reset all Settings", SettingsMenu, ResetSettings);
        }

        private void HideWCChange()
        {
            Settings.Instance.hideWC = !Settings.Instance.hideWC;
            HideWC.Text = "Suppress signals if detected by WC: " + Settings.Instance.hideWC;
        }
        private void HideDistChange(string obj)
        {
            int getter;
            if (!int.TryParse(obj, out getter))
                return;
            Settings.Instance.hideDistance = getter;
            HideDist.Text = "Hide signals within " + Settings.Instance.hideDistance + "m";
        }
        private void IncreaseSymSz()
        {
            Settings.Instance.symbolWidth += 0.0025f;
            symbolHeight = Settings.Instance.symbolWidth * aspectRatio;
            Settings.Instance.offscreenWidth = Settings.Instance.symbolWidth + 0.02f;
            offscreenHeight = Settings.Instance.offscreenWidth * aspectRatio;
        }
        private void DecreaseSymSz()
        {
            Settings.Instance.symbolWidth -= 0.0025f;
            symbolHeight = Settings.Instance.symbolWidth * aspectRatio;
            Settings.Instance.offscreenWidth = Settings.Instance.symbolWidth + 0.02f;
            offscreenHeight = Settings.Instance.offscreenWidth * aspectRatio;
        }
        private void IncreaseTextSz()
        {
            Settings.Instance.textSize += 0.025f;
        }
        private void DecreaseTextSz()
        {
            Settings.Instance.textSize -= 0.025f;
        }
        private void IncreaseOwnSz()
        {
            Settings.Instance.textSizeOwn += 0.025f;
            ownShipLabel.Scale = Settings.Instance.textSizeOwn;
        }
        private void DecreaseOwnSz()
        {
            Settings.Instance.textSizeOwn -= 0.025f;
            ownShipLabel.Scale = Settings.Instance.textSizeOwn;
        }
        private void LeftMove()
        {
            Settings.Instance.signalDrawCoords += new Vector2D(-0.005, 0);
            ownShipLabel.Origin = Settings.Instance.signalDrawCoords;
        }
        private void RightMove()
        {
            Settings.Instance.signalDrawCoords += new Vector2D(0.005, 0);
            ownShipLabel.Origin = Settings.Instance.signalDrawCoords;
        }
        private void UpMove()
        {
            Settings.Instance.signalDrawCoords += new Vector2D(0, 0.005 * aspectRatio);
            ownShipLabel.Origin = Settings.Instance.signalDrawCoords;
        }
        private void DownMove()
        {
            Settings.Instance.signalDrawCoords += new Vector2D(0, -0.005 * aspectRatio);
            ownShipLabel.Origin = Settings.Instance.signalDrawCoords;
        }
        private void ResetMove()
        {
            Settings.Instance.signalDrawCoords = new Vector2D(-0.7, -0.625);
            ownShipLabel.Origin = Settings.Instance.signalDrawCoords;
        }
        private void ColorFriend(Color obj)
        {
            Settings.Instance.friendColor = obj;
            FriendColor.InitialColor = obj;
        }
        private void ColorNeutral(Color obj)
        {
            Settings.Instance.neutralColor = obj;
            NeutralColor.InitialColor = obj;
        }
        private void ColorEnemy(Color obj)
        {
            Settings.Instance.enemyColor = obj;
            EnemyColor.InitialColor = obj;
        }
        private void FadeTimeAdj(string obj)
        {
            double getter;
            if (!double.TryParse(obj, out getter))
                return;
            Settings.Instance.fadeOutTime = getter;
            LabelUpdate();
            fadeTimeTicks = (int)(getter * 60);
        }
        private void StopTimeAdj(string obj)
        {
            double getter;
            if (!double.TryParse(obj, out getter))
                return;
            Settings.Instance.fadeOutTime = getter;
            LabelUpdate();
            stopDisplayTimeTicks = (int)(getter * 60);
        }
        private void KeepTimeAdj(string obj)
        {
            double getter;
            if (!double.TryParse(obj, out getter))
                return;
            Settings.Instance.keepTime = getter;
            LabelUpdate();
            keepTimeTicks = (int)(getter * 3600);
        }
        private void ResetSettings()
        {
            MyAPIGateway.Utilities.ShowNotification("Options reset to default");
            Settings.Instance = Settings.Default;
            Save(Settings.Instance);
            LabelUpdate();
        }
        private void LabelUpdate()
        {
            KeepTime.Text = "Purge signal record after " + Settings.Instance.keepTime + " minutes";
            StopDisplayTime.Text = "Stop displaying after " + Settings.Instance.stopDisplayTime + " seconds";
            FadeTime.Text = "Fade out after " + Settings.Instance.fadeOutTime + " seconds";
            HideDist.Text = "Hide signals within " + Settings.Instance.hideDistance + "m";
        }
    }
}

