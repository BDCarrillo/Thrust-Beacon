using Draygo.API;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game.Components;
using VRageMath;

namespace ThrustBeacon
{
    public partial class Session : MySessionComponentBase
    {      
        //Main clientside loop for visuals
        public override void Draw()
        {
            if (Client && clientActionRegistered && hudAPI.Heartbeat && SignalList.Count > 0 && MyAPIGateway.Session.Config.HudState != 0 && !MyAPIGateway.Gui.IsCursorVisible)
            {
                var s = Settings.Instance;
                var viewProjectionMat = Session.Camera.ViewMatrix * Session.Camera.ProjectionMatrix;
                var camPos = Session.Camera.Position;

                //Draw main signal list
                foreach (var signal in SignalList)
                {
                    var contact = signal.Value.Item1;

                    //WC Deconflict
                    if(s.hideWC && entityIDList.Contains(contact.entityID))
                        continue;

                    //Signal for own occupied grid
                    if (contact.entityID == controlledGridParent || contact.relation == 4)
                    {
                        var dispRange = contact.range > 1000 ? (contact.range / 1000f).ToString("0.#") + " km" : contact.range + " m";
                        //Coloration and flashing for warning
                        var warnColor = "";
                        if (contact.sizeEnum == 6 && (Tick + 15) % 60 <= 20)
                            warnColor = "<color=255, 0, 0>";
                        else if (contact.sizeEnum == 5 && (Tick + 15) % 60 <= 20)
                            warnColor = "<color=255, 255, 0>";
                        var info = new StringBuilder($"Broadcast Dist: " + dispRange + "\n" + "Size: " + warnColor + messageList[contact.sizeEnum]);
                        ownShipLabel.Message = info;

                        //Optional beacon updates
                        if(clientUpdateBeacon && Tick % 29 == 0 && primaryBeacon != null)
                        {
                            primaryBeacon.Radius = contact.range;
                            primaryBeacon.HudText = messageList[contact.sizeEnum];
                            clientLastBeaconDist = contact.range;
                            clientLastBeaconSizeEnum = contact.sizeEnum;
                        }
                    }
                    //Other grid signals received from server
                    else
                    {
                        var contactAge = Tick - signal.Value.Item2;
                        if (contactAge >= stopDisplayTimeTicks)
                        {
                            if (contactAge >= keepTimeTicks)
                                SignalList.Remove(signal.Key);
                            continue;
                        }
                        float distance = Vector3.Distance(contact.position, camPos);
                        if (distance < s.hideDistance) continue;

                        //Color and fade over time effect
                        var baseColor = contact.relation == 1 ? s.enemyColor : contact.relation == 3 ? s.friendColor : s.neutralColor;
                        var adjColor = baseColor;
                        if (fadeTimeTicks > 0)
                        {
                            byte colorFade = (byte)(contactAge < fadeTimeTicks ? 0 : (contactAge - fadeTimeTicks) / 2);
                            adjColor.R = (byte)MathHelper.Clamp(baseColor.R - colorFade, 0, 255);
                            adjColor.G = (byte)MathHelper.Clamp(baseColor.G - colorFade, 0, 255);
                            adjColor.B = (byte)MathHelper.Clamp(baseColor.B - colorFade, 0, 255);
                        }

                        var adjustedPos = camPos + Vector3D.Normalize((Vector3D)contact.position - camPos) * viewDist;
                        var screenCoords = Vector3D.Transform(adjustedPos, viewProjectionMat);
                        var offScreen = screenCoords.X > 1 || screenCoords.X < -1 || screenCoords.Y > 1 || screenCoords.Y < -1 || screenCoords.Z > 1;

                        //Draw symbol on grid with text
                        if (!offScreen)
                        {
                            var symbolPosition = new Vector2D(screenCoords.X, screenCoords.Y);
                            var labelPosition = new Vector2D(screenCoords.X + (s.symbolWidth * 0.25), screenCoords.Y + (symbolHeight * 0.4));
                            var dispRange = distance > 1000 ? (distance / 1000).ToString("0.#") + " km" : distance.ToString("0.#") + " m";
                            var qty = contact.quantity > 1 ? contact.quantity + "x " : "";
                            var info = new StringBuilder(qty + contact.faction + messageList[contact.sizeEnum] + "\n" + dispRange);
                            var Label = new HudAPIv2.HUDMessage(info, labelPosition, new Vector2D(0, -0.001), 2, s.textSize, true, true);
                            Label.InitialColor = adjColor;
                            Label.Visible = true;
                            var symbolObj = new HudAPIv2.BillBoardHUDMessage(symbolList[contact.sizeEnum], symbolPosition, adjColor, Width: s.symbolWidth, Height: symbolHeight, TimeToLive: 2, HideHud: true, Shadowing: true);
                        }
                        //Draw off screen indicators and arrows
                        else
                        {
                            if (screenCoords.Z > 1)//Camera is between player and target
                                screenCoords *= -1;
                            var vectorToPt = new Vector2D(screenCoords.X, screenCoords.Y);
                            vectorToPt.Normalize();
                            vectorToPt *= offscreenSquish; //This flattens the Y axis so symbols don't overlap the hotbar and brings the X closer in
                            var rotation = (float)Math.Atan2(screenCoords.X, screenCoords.Y);
                            var symbolObj = new HudAPIv2.BillBoardHUDMessage(symbolOffscreenArrow, vectorToPt, adjColor, Width: s.offscreenWidth * 0.75f, Height: offscreenHeight, TimeToLive: 2, Rotation: rotation, HideHud: true, Shadowing: true);
                            var symbolObj2 = new HudAPIv2.BillBoardHUDMessage(symbolList[contact.sizeEnum], vectorToPt, adjColor, Width: s.symbolWidth, Height: symbolHeight, TimeToLive: 2, HideHud: true, Shadowing: true);
                        }
                    }
                }
            }
        }
    }
}
