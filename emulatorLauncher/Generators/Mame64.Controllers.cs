﻿using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml.Linq;
using EmulatorLauncher.Common.Lightguns;
using EmulatorLauncher.Common.Joysticks;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    partial class Mame64Generator
    {
        private bool ConfigureMameControllers(string path, bool hbmame)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return false;

            if (Controllers.Count == 0)
                return false;

            else if (Controllers.Count == 1)
            {
                var c1 = Controllers.FirstOrDefault(c => c.PlayerIndex == 1);

                if (c1.IsKeyboard)
                    return false;
            }
            
            // Delete existing default file if any
            string defaultCtrl = Path.Combine(path, "default.cfg");
            if (File.Exists(defaultCtrl))
                File.Delete(defaultCtrl);

            string inputConfig = Path.Combine(path, "retrobat_auto.cfg");
            if (File.Exists(inputConfig))
                File.Delete(inputConfig);

            Dictionary<Controller, int> hybridController = new Dictionary<Controller, int>();
            var mameconfig = new XElement("mameconfig", new XAttribute("version", "10"));
            var system = new XElement("system", new XAttribute("name", "default"));
            var input = new XElement("input");

            var mameControllers = this.Controllers.Where(c => !c.IsKeyboard).OrderBy(i => i.PlayerIndex).ToList();

            if (SystemConfig["mame_joystick_driver"] == "xinput")
                mameControllers = this.Controllers.Where(c => c.IsXInputDevice && !c.IsKeyboard).OrderBy(i => i.PlayerIndex).ToList();

            else if (!SystemConfig.isOptSet("mame_joystick_driver") || SystemConfig["mame_joystick_driver"] == "winhybrid")
            {
                int i = 1;
                var nonXinputControllers = mameControllers.Where(c => !c.IsXInputDevice).OrderBy(o => o.DirectInput.DeviceIndex).ToList();
                if (nonXinputControllers.Count > 0)
                {
                    foreach (var controller in nonXinputControllers)
                    {
                        hybridController.Add(controller, i);
                        i++;
                    }
                }

                var usbControllers = mameControllers.Where(c => !nonXinputControllers.Contains(c) && c.DevicePath.StartsWith("USB")).OrderBy(o => o.DirectInput.DeviceIndex).ToList();
                if (usbControllers.Count > 0)
                {
                    foreach (var controller in usbControllers)
                    {
                        hybridController.Add(controller, i);
                        i++;
                    }
                }

                var otherXinputControllers = mameControllers.Where(c => !nonXinputControllers.Contains(c) && !usbControllers.Contains(c)).OrderBy(o => o.DirectInput.DeviceIndex).ToList();
                if (otherXinputControllers.Count > 0)
                {
                    foreach (var controller in otherXinputControllers)
                    {
                        hybridController.Add(controller, i);
                        i++;
                    }
                }
            }

            foreach (var controller in mameControllers)
            {
                int i = controller.PlayerIndex;
                int cIndex = mameControllers.OrderBy(c => c.DirectInput.DeviceIndex).ToList().IndexOf(controller) + 1;
                string joy = "JOYCODE_" + cIndex + "_";
                bool dpadonly = SystemConfig.isOptSet("mame_dpadandstick") && SystemConfig.getOptBoolean("mame_dpadandstick");
                bool isXinput = controller.IsXInputDevice && SystemConfig["mame_joystick_driver"] != "dinput";
                bool xinputCtrl = controller.IsXInputDevice;
                string guid = (controller.Guid.ToString()).Substring(0, 27) + "00000";
                SdlToDirectInput ctrlr = null;
                string gamecontrollerDB = Path.Combine(AppConfig.GetFullPath("tools"), "gamecontrollerdb.txt");

                if (SystemConfig["mame_joystick_driver"] == "xinput")
                {
                    int xIndex = mameControllers.OrderBy(c => c.DeviceIndex).ToList().IndexOf(controller) + 1;
                    joy = "JOYCODE_" + xIndex + "_";
                    input.Add(new XElement("mapdevice", new XAttribute("device", "XInput Player " + xIndex), new XAttribute("controller", "JOYCODE_" + xIndex)));
                }

                if ((!SystemConfig.isOptSet("mame_joystick_driver") || SystemConfig["mame_joystick_driver"] == "winhybrid") && hybridController.Count > 0)
                {
                    var hIndex = hybridController[controller];
                    joy = "JOYCODE_" + hIndex + "_";
                }

                // Get dinput mapping information
                if (!isXinput)
                {
                    if (!File.Exists(gamecontrollerDB))
                    {
                        SimpleLogger.Instance.Info("[INFO] gamecontrollerdb.txt file not found in tools folder. Controller mapping will not be available.");
                        gamecontrollerDB = null;
                    }
                    if (gamecontrollerDB != null)
                    {
                        SimpleLogger.Instance.Info("[INFO] Player "+ i + " . Fetching gamecontrollerdb.txt file with guid : " + guid);

                        try { ctrlr = GameControllerDBParser.ParseByGuid(gamecontrollerDB, guid); }
                        catch { }

                        if (ctrlr == null || ctrlr.ButtonMappings == null)
                            SimpleLogger.Instance.Info("[INFO] Player "+ i + ". No controller mapping found in gamecontrollerdb.txt file for guid : " + guid);
                        else
                            SimpleLogger.Instance.Info("[INFO] Player " + i + ": controller mapping found in gamecontrollerDB file.");
                    }
                }

                // Guns
                int gunCount = RawLightgun.GetUsableLightGunCount();
                var guns = RawLightgun.GetRawLightguns();
                _multigun = false;

                if (gunCount > 1 && guns.Length > 1)
                    _multigun = true;

                string mouseIndex1 = "1";
                string mouseIndex2 = "2";

                if (gunCount > 0 && guns.Length > 0)
                {
                    SimpleLogger.Instance.Info("[GUNS] Found " + guns.Length + " gun(s).");
                    mouseIndex1 = (guns[0].Index + 1).ToString();
                    SimpleLogger.Instance.Info("[GUNS] Gun 1 index : " + mouseIndex1);

                    if (_multigun)
                    {
                        mouseIndex2 = (guns[1].Index + 1).ToString();
                        SimpleLogger.Instance.Info("[GUNS] Gun 2 index : " + mouseIndex2);
                    }
                }

                if (SystemConfig.isOptSet("mame_gun1") && !string.IsNullOrEmpty(SystemConfig["mame_gun1"]))
                {
                    mouseIndex1 = SystemConfig["mame_gun1"];
                    SimpleLogger.Instance.Info("[GUNS] Overwriting Gun 1 index : " + mouseIndex1);
                }
                if (SystemConfig.isOptSet("mame_gun2") && !string.IsNullOrEmpty(SystemConfig["mame_gun2"]))
                {
                    mouseIndex2 = SystemConfig["mame_gun2"];
                    SimpleLogger.Instance.Info("[GUNS] Overwriting Gun 2 index : " + mouseIndex2);
                }

                // define mapping for xInput case
                var mapping = hbmame? hbxInputMapping : xInputMapping;

                #region player1
                // Add UI mapping for player 1 to control MAME UI + Service menu
                if (i == 1)
                {
                    #region player 1 xinput
                    if (isXinput)
                    {
                        if (hbmame)
                        {
                            input.Add(new XElement
                                ("port", new XAttribute("type", "UI_CONFIGURE"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " " + joy + mapping["south"] + " OR KEYCODE_TAB")));
                        }
                        else
                        {
                            input.Add(new XElement
                                ("port", new XAttribute("type", "UI_MENU"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " " + joy + mapping["south"] + " OR KEYCODE_TAB")));
                        }

                        input.Add(new XElement
                            ("port", new XAttribute("type", "UI_SELECT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["south"] + " OR KEYCODE_ENTER")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "UI_BACK"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["east"] + " OR KEYCODE_ESC")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "UI_CANCEL"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " " + joy + mapping["start"] + " OR KEYCODE_ESC")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "UI_UP"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["up"] + " OR " + joy + mapping["lsup"] + " OR KEYCODE_UP")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "UI_DOWN"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["down"] + " OR " + joy + mapping["lsdown"] + " OR KEYCODE_DOWN")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "UI_LEFT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["left"] + " OR " + joy + mapping["lsleft"] + " OR KEYCODE_LEFT")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "UI_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["right"] + " OR " + joy + mapping["lsright"] + " OR KEYCODE_RIGHT")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "UI_PAUSE"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " " + joy + mapping["east"] + " OR KEYCODE_P")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "UI_REWIND_SINGLE"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " " + joy + mapping["left"] + " OR KEYCODE_TILDE KEYCODE_LSHIFT")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "UI_FAST_FORWARD"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " " + joy + mapping["right"] + " OR KEYCODE_INSERT")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "UI_SAVE_STATE"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " " + joy + mapping["west"] + " OR KEYCODE_F7 KEYCODE_LSHIFT")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "UI_LOAD_STATE"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " " + joy + mapping["north"] + " OR KEYCODE_F7")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "SERVICE"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["l3"] + " " + joy + mapping["r3"] + " OR KEYCODE_F2")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "SERVICE1"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["l3"] + " " + joy + mapping["r3"] + " OR KEYCODE_9")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "TILT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " " + joy + mapping["r1"] + " OR KEYCODE_T")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "TILT1"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " " + joy + mapping["r1"] + " OR KEYCODE_T")));

                        // Standard joystick buttons and directions
                        if (dpadonly)
                        {
                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_JOYSTICK_UP"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["up"] + " OR KEYCODE_UP")));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_JOYSTICK_DOWN"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["down"] + " OR KEYCODE_DOWN")));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_JOYSTICK_LEFT"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["left"] + " OR KEYCODE_LEFT")));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_JOYSTICK_RIGHT"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["right"] + " OR KEYCODE_RIGHT")));
                        }
                        else
                        {
                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_JOYSTICK_UP"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["up"] + " OR " + joy + mapping["lsup"] + " OR KEYCODE_UP")));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_JOYSTICK_DOWN"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["down"] + " OR " + joy + mapping["lsdown"] + " OR KEYCODE_DOWN")));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_JOYSTICK_LEFT"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["left"] + " OR " + joy + mapping["lsleft"] + " OR KEYCODE_LEFT")));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_JOYSTICK_RIGHT"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["right"] + " OR " + joy + mapping["lsright"] + " OR KEYCODE_RIGHT")));
                        }

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_UP"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["rsup"] + " OR KEYCODE_I")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_DOWN"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["rsdown"] + " OR KEYCODE_K")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_LEFT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["rsleft"] + " OR KEYCODE_J")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["rsright"] + " OR KEYCODE_L")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_UP"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["lsup"] + " OR KEYCODE_E")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_DOWN"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["lsdown"] + " OR KEYCODE_D")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_LEFT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["lsleft"] + " OR KEYCODE_S")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["lsright"] + " OR KEYCODE_F")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON1"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["south"] + " OR KEYCODE_LCONTROL OR MOUSECODE_" + mouseIndex1 + "_BUTTON1 OR GUNCODE_" + mouseIndex1 + "_BUTTON1")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON2"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["east"] + " OR KEYCODE_LALT OR MOUSECODE_" + mouseIndex1 + "_BUTTON3 OR GUNCODE_" + mouseIndex1 + "_BUTTON2")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON3"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["west"] + " OR KEYCODE_SPACE OR MOUSECODE_" + mouseIndex1 + "_BUTTON2")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON4"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["north"] + " OR KEYCODE_LSHIFT")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON5"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["l1"] + " OR KEYCODE_Z")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON6"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["r1"] + " OR KEYCODE_X")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON7"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["l3"] + " OR KEYCODE_C")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON8"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["r3"] + " OR KEYCODE_V")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_START"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["start"] + " OR KEYCODE_1")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_SELECT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " OR KEYCODE_5")));

                        // Pedals and other devices
                        if (hbmame)
                        {
                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_PEDAL"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["r2"]),
                                    new XElement("newseq", new XAttribute("type", "increment"), joy + mapping["south"] + " KEYCODE_LCONTROL")));


                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_PEDAL2"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["l2"]),
                                    new XElement("newseq", new XAttribute("type", "increment"), joy + mapping["east"] + " OR KEYCODE_LALT")));
                        }
                        else
                        {
                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_PEDAL"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["r2"] + "_NEG"),
                                    new XElement("newseq", new XAttribute("type", "increment"), joy + mapping["south"] + " KEYCODE_LCONTROL")));


                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_PEDAL2"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["l2"] + "_NEG"),
                                    new XElement("newseq", new XAttribute("type", "increment"), joy + mapping["east"] + " OR KEYCODE_LALT")));
                        }

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_PEDAL3"),
                                new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_SPACE")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_PADDLE"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_x"] + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS"),
                                new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_LEFT")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_PADDLE_V"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_y"] + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS"),
                                new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_DOWN"),
                                new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_UP")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_POSITIONAL"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_x"] + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS"),
                                new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_LEFT")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_POSITIONAL_V"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_y"] + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS"),
                                new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_DOWN"),
                                new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_UP")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_DIAL"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_x"] + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS"),
                                new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_LEFT")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_DIAL_V"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_y"] + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS"),
                                new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_DOWN"),
                                new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_UP")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_TRACKBALL_X"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_x"] + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS"),
                                new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_LEFT")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_TRACKBALL_Y"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_y"] + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS"),
                                new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_DOWN"),
                                new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_UP")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_AD_STICK_X"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_x"] + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS"),
                                new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_LEFT")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_AD_STICK_Y"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_y"] + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS"),
                                new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_DOWN"),
                                new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_UP")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_AD_STICK_Z"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["rs_y"]),
                                new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_Z"),
                                new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_A")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_LIGHTGUN_X"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_x"] + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS OR GUNCODE_" + mouseIndex1 + "_XAXIS"),
                                new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_LEFT")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_LIGHTGUN_Y"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_y"] + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS OR GUNCODE_" + mouseIndex1 + "_YAXIS"),
                                new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_DOWN"),
                                new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_UP")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_MOUSE_X"),
                                new XElement("newseq", new XAttribute("type", "standard"), "MOUSECODE_" + mouseIndex1 + "_XAXIS"),
                                new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_LEFT")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_MOUSE_Y"),
                                new XElement("newseq", new XAttribute("type", "standard"), "MOUSECODE_" + mouseIndex1 + "_YAXIS"),
                                new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_DOWN"),
                                new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_UP")));

                        // Start & coin
                        input.Add(new XElement
                            ("port", new XAttribute("type", "START" + i),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["start"] + " OR KEYCODE_1")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "COIN" + i),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " OR KEYCODE_5")));
                    }
                    #endregion

                    #region dinput player 1
                    else
                    {
                        if (hbmame)
                        {
                            input.Add(new XElement
                                ("port", new XAttribute("type", "UI_CONFIGURE"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "a", xinputCtrl) + " OR KEYCODE_TAB")));
                        }
                        else
                        {
                            input.Add(new XElement
                                ("port", new XAttribute("type", "UI_MENU"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "a", xinputCtrl) + " OR KEYCODE_TAB")));
                        }

                        input.Add(new XElement
                            ("port", new XAttribute("type", "UI_SELECT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "a", xinputCtrl) + " OR KEYCODE_ENTER")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "UI_BACK"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "b", xinputCtrl) + " OR KEYCODE_ESC")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "UI_CANCEL"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "start", xinputCtrl) + " OR KEYCODE_ESC")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "UI_UP"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpup", xinputCtrl) + " OR " + joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, -1) + " OR KEYCODE_UP")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "UI_DOWN"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpdown", xinputCtrl) + " OR " + joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 1) + " OR KEYCODE_DOWN")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "UI_LEFT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpleft", xinputCtrl) + " OR " + joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, -1) + " OR KEYCODE_LEFT")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "UI_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpright", xinputCtrl) + " OR " + joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 1) + " OR KEYCODE_RIGHT")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "UI_PAUSE"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "b", xinputCtrl) + " OR KEYCODE_P")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "UI_REWIND_SINGLE"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "dpleft", xinputCtrl) + " OR KEYCODE_TILDE KEYCODE_LSHIFT")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "UI_FAST_FORWARD"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "dpright", xinputCtrl) + " OR KEYCODE_INSERT")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "UI_SAVE_STATE"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "x", xinputCtrl) + " OR KEYCODE_F7 KEYCODE_LSHIFT")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "UI_LOAD_STATE"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "y", xinputCtrl) + " OR KEYCODE_F7")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "SERVICE"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftstick", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "rightstick", xinputCtrl) + " OR KEYCODE_F2")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "SERVICE1"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftstick", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "rightstick", xinputCtrl) + " OR KEYCODE_9")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "TILT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "rightshoulder", xinputCtrl) + " OR KEYCODE_T")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "TILT1"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "rightshoulder", xinputCtrl) + " OR KEYCODE_T")));

                        // Standard joystick buttons and directions
                        if (dpadonly)
                        {
                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_JOYSTICK_UP"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpup", xinputCtrl) + " OR KEYCODE_UP")));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_JOYSTICK_DOWN"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpdown", xinputCtrl) + " OR KEYCODE_DOWN")));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_JOYSTICK_LEFT"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpleft", xinputCtrl) + " OR KEYCODE_LEFT")));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_JOYSTICK_RIGHT"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpright", xinputCtrl) + " OR KEYCODE_RIGHT")));
                        }
                        else
                        {
                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_JOYSTICK_UP"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpup", xinputCtrl) + " OR " + joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, -1) + " OR KEYCODE_UP")));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_JOYSTICK_DOWN"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpdown", xinputCtrl) + " OR " + joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 1) + " OR KEYCODE_DOWN")));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_JOYSTICK_LEFT"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpleft", xinputCtrl) + " OR " + joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, -1) + " OR KEYCODE_LEFT")));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_JOYSTICK_RIGHT"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpright", xinputCtrl) + " OR " + joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 1) + " OR KEYCODE_RIGHT")));
                        }

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_UP"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "righty", xinputCtrl, -1) + " OR KEYCODE_I")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_DOWN"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "righty", xinputCtrl, 1) + " OR KEYCODE_K")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_LEFT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "rightx", xinputCtrl, -1) + " OR KEYCODE_J")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "rightx", xinputCtrl, 1) + " OR KEYCODE_L")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_UP"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, -1) + " OR KEYCODE_E")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_DOWN"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 1) + " OR KEYCODE_D")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_LEFT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, -1) + " OR KEYCODE_S")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 1) + " OR KEYCODE_F")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON1"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "a", xinputCtrl) + " OR KEYCODE_LCONTROL OR MOUSECODE_" + mouseIndex1 + "_BUTTON1 OR GUNCODE_" + mouseIndex1 + "_BUTTON1")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON2"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "b", xinputCtrl) + " OR KEYCODE_LALT OR MOUSECODE_" + mouseIndex1 + "_BUTTON3 OR GUNCODE_" + mouseIndex1 + "_BUTTON2")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON3"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "x", xinputCtrl) + " OR KEYCODE_SPACE OR MOUSECODE_" + mouseIndex1 + "_BUTTON2")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON4"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "y", xinputCtrl) + " OR KEYCODE_LSHIFT")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON5"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftshoulder", xinputCtrl) + " OR KEYCODE_Z")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON6"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "rightshoulder", xinputCtrl) + " OR KEYCODE_X")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON7"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftstick", xinputCtrl) + " OR KEYCODE_C")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON8"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "rightstick", xinputCtrl) + " OR KEYCODE_V")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_START"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "start", xinputCtrl) + " OR KEYCODE_1")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_SELECT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " OR KEYCODE_5")));

                        // Pedals and other devices
                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_PEDAL"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "righttrigger", xinputCtrl, 1)),
                                new XElement("newseq", new XAttribute("type", "increment"), joy + GetDinputMapping(ctrlr, "a", xinputCtrl) + " KEYCODE_LCONTROL")));


                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_PEDAL2"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefttrigger", xinputCtrl, 1)),
                                new XElement("newseq", new XAttribute("type", "increment"), joy + GetDinputMapping(ctrlr, "b", xinputCtrl) + " OR KEYCODE_LALT")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_PEDAL3"),
                                new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_SPACE")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_PADDLE"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS"),
                                new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_LEFT")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_PADDLE_V"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS"),
                                new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_DOWN"),
                                new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_UP")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_POSITIONAL"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS"),
                                new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_LEFT")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_POSITIONAL_V"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS"),
                                new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_DOWN"),
                                new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_UP")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_DIAL"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS"),
                                new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_LEFT")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_DIAL_V"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS"),
                                new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_DOWN"),
                                new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_UP")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_TRACKBALL_X"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS"),
                                new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_LEFT")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_TRACKBALL_Y"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS"),
                                new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_DOWN"),
                                new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_UP")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_AD_STICK_X"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS"),
                                new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_LEFT")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_AD_STICK_Y"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS"),
                                new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_DOWN"),
                                new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_UP")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_AD_STICK_Z"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "righty", xinputCtrl, 0)),
                                new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_Z"),
                                new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_A")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_LIGHTGUN_X"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS OR GUNCODE_" + mouseIndex1 + "_XAXIS"),
                                new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_LEFT")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_LIGHTGUN_Y"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS OR GUNCODE_" + mouseIndex1 + "_YAXIS"),
                                new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_DOWN"),
                                new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_UP")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_MOUSE_X"),
                                new XElement("newseq", new XAttribute("type", "standard"), "MOUSECODE_" + mouseIndex1 + "_XAXIS"),
                                new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_LEFT")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_MOUSE_Y"),
                                new XElement("newseq", new XAttribute("type", "standard"), "MOUSECODE_" + mouseIndex1 + "_YAXIS"),
                                new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_DOWN"),
                                new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_UP")));

                        // Start & coin
                        input.Add(new XElement
                            ("port", new XAttribute("type", "START" + i),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "start", xinputCtrl) + " OR KEYCODE_1")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "COIN" + i),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " OR KEYCODE_5")));
                    }
                    #endregion
                }
                #endregion

                #region other players
                // Max 8 players for mame
                else if (i <= 8)
                {
                    #region other players xinput
                    if (isXinput)
                    {
                        if (dpadonly)
                        {
                            input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICK_UP"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["up"])));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_JOYSTICK_DOWN"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["down"])));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_JOYSTICK_LEFT"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["left"])));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_JOYSTICK_RIGHT"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["right"])));
                        }
                        else
                        {
                            input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICK_UP"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["up"] + " OR " + joy + mapping["lsup"])));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_JOYSTICK_DOWN"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["down"] + " OR " + joy + mapping["lsdown"])));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_JOYSTICK_LEFT"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["left"] + " OR " + joy + mapping["lsleft"])));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_JOYSTICK_RIGHT"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["right"] + " OR " + joy + mapping["lsright"])));
                        }

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_UP"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["rsup"])));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_DOWN"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["rsdown"])));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_LEFT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["rsleft"])));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["rsright"])));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_UP"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["lsup"])));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_DOWN"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["lsdown"])));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_LEFT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["lsleft"])));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["lsright"])));

                        // Case of 2 guns only for now, cannot test more than 2 guns so stop here
                        if ((_multigun && (i == 2)) || (SystemConfig.isOptSet("mame_multimouse") && SystemConfig.getOptBoolean("mame_multimouse") && (i == 2)))
                        {
                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_BUTTON1"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["south"] + " OR MOUSECODE_" + mouseIndex2 + "_BUTTON1 OR GUNCODE_" + mouseIndex2 + "_BUTTON1")));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_BUTTON2"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["east"] + " OR MOUSECODE_" + mouseIndex2 + "_BUTTON3 OR GUNCODE_" + mouseIndex2 + "_BUTTON2")));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_BUTTON3"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["west"] + " OR MOUSECODE_" + mouseIndex2 + "_BUTTON2")));
                        }
                        else
                        {
                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_BUTTON1"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["south"])));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_BUTTON2"),
                                     new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["east"])));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_BUTTON3"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["west"])));
                        }

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON4"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["north"])));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON5"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["l1"])));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON6"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["r1"])));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON7"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["l3"])));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON8"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["r3"])));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_START"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["start"])));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_SELECT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"])));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "START" + i),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["start"])));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "COIN" + i),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"])));

                        // Pedals and other devices
                        if (hbmame)
                        {
                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_PEDAL"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["r2"]),
                                    new XElement("newseq", new XAttribute("type", "increment"), joy + mapping["south"])));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_PEDAL2"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["l2"]),
                                    new XElement("newseq", new XAttribute("type", "increment"), joy + mapping["east"])));
                        }
                        else
                        {
                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_PEDAL"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["r2"] + "_NEG"),
                                    new XElement("newseq", new XAttribute("type", "increment"), joy + mapping["south"])));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_PEDAL2"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["l2"] + "_NEG"),
                                    new XElement("newseq", new XAttribute("type", "increment"), joy + mapping["east"])));
                        }

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_PEDAL3"),
                                new XElement("newseq", new XAttribute("type", "increment"), joy + mapping["south"])));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_PADDLE"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_x"])));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_PADDLE_V"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_y"])));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_DIAL"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_x"])));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_DIAL_V"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_y"])));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_TRACKBALL_X"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_x"])));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_TRACKBALL_Y"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_y"])));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_AD_STICK_X"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_x"])));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_AD_STICK_Y"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_y"])));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_AD_STICK_Z"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["rs_y"])));

                        if ((_multigun && (i == 2)) || (SystemConfig.isOptSet("mame_multimouse") && SystemConfig.getOptBoolean("mame_multimouse") && (i == 2)))
                        {
                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_LIGHTGUN_X"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_x"] + " OR MOUSECODE_" + mouseIndex2 + "_XAXIS OR GUNCODE_" + mouseIndex2 + "_XAXIS")));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_LIGHTGUN_Y"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_y"] + " OR MOUSECODE_" + mouseIndex2 + "_YAXIS OR GUNCODE_" + mouseIndex2 + "_YAXIS")));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_MOUSE_X"),
                                    new XElement("newseq", new XAttribute("type", "standard"), "MOUSECODE_" + mouseIndex2 + "_XAXIS")));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_MOUSE_Y"),
                                    new XElement("newseq", new XAttribute("type", "standard"), "MOUSECODE_" + mouseIndex2 + "_YAXIS")));
                        }
                        else
                        {
                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_LIGHTGUN_X"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_x"])));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_LIGHTGUN_Y"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_y"])));
                        }
                    }
                    #endregion

                    #region dinput other players
                    else
                    {
                        if (dpadonly)
                        {
                            input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICK_UP"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpup", xinputCtrl))));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_JOYSTICK_DOWN"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpdown", xinputCtrl))));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_JOYSTICK_LEFT"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpleft", xinputCtrl))));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_JOYSTICK_RIGHT"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpright", xinputCtrl))));
                        }
                        else
                        {
                            input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICK_UP"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpup", xinputCtrl) + " OR " + joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, -1))));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_JOYSTICK_DOWN"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpdown", xinputCtrl) + " OR " + joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 1))));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_JOYSTICK_LEFT"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpleft", xinputCtrl) + " OR " + joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, -1))));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_JOYSTICK_RIGHT"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpright", xinputCtrl) + " OR " + joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 1))));
                        }

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_UP"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "righty", xinputCtrl, -1))));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_DOWN"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "righty", xinputCtrl, 1))));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_LEFT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "rightx", xinputCtrl, -1))));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "rightx", xinputCtrl, 1))));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_UP"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, -1))));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_DOWN"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 1))));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_LEFT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, -1))));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 1))));

                        // Case of 2 guns only for now, cannot test more than 2 guns so stop here
                        if ((_multigun && (i == 2)) || (SystemConfig.isOptSet("mame_multimouse") && SystemConfig.getOptBoolean("mame_multimouse") && (i == 2)))
                        {
                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_BUTTON1"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "a", xinputCtrl) + " OR MOUSECODE_" + mouseIndex2 + "_BUTTON1 OR GUNCODE_" + mouseIndex2 + "_BUTTON1")));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_BUTTON2"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "b", xinputCtrl) + " OR MOUSECODE_" + mouseIndex2 + "_BUTTON3 OR GUNCODE_" + mouseIndex2 + "_BUTTON2")));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_BUTTON3"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "x", xinputCtrl) + " OR MOUSECODE_" + mouseIndex2 + "_BUTTON2")));
                        }
                        else
                        {
                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_BUTTON1"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "a", xinputCtrl))));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_BUTTON2"),
                                     new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "b", xinputCtrl))));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_BUTTON3"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "x", xinputCtrl))));
                        }

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON4"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "y", xinputCtrl))));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON5"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftshoulder", xinputCtrl))));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON6"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "rightshoulder", xinputCtrl))));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON7"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftstick", xinputCtrl))));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON8"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "rightstick", xinputCtrl))));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_START"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "start", xinputCtrl))));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_SELECT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "back", xinputCtrl))));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "START" + i),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "start", xinputCtrl))));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "COIN" + i),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "back", xinputCtrl))));

                        // Pedals and other devices
                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_PEDAL"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "righttrigger", xinputCtrl, 1)),
                                new XElement("newseq", new XAttribute("type", "increment"), joy + GetDinputMapping(ctrlr, "a", xinputCtrl))));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_PEDAL2"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefttrigger", xinputCtrl, 1)),
                                new XElement("newseq", new XAttribute("type", "increment"), joy + GetDinputMapping(ctrlr, "b", xinputCtrl))));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_PEDAL3"),
                                new XElement("newseq", new XAttribute("type", "increment"), joy + GetDinputMapping(ctrlr, "a", xinputCtrl))));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_PADDLE"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0))));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_PADDLE_V"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0))));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_DIAL"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0))));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_DIAL_V"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0))));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_TRACKBALL_X"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0))));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_TRACKBALL_Y"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0))));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_AD_STICK_X"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0))));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_AD_STICK_Y"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0))));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_AD_STICK_Z"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "righty", xinputCtrl, 0))));

                        if ((_multigun && (i == 2)) || (SystemConfig.isOptSet("mame_multimouse") && SystemConfig.getOptBoolean("mame_multimouse") && (i == 2)))
                        {
                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_LIGHTGUN_X"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex2 + "_XAXIS OR GUNCODE_" + mouseIndex2 + "_XAXIS")));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_LIGHTGUN_Y"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex2 + "_YAXIS OR GUNCODE_" + mouseIndex2 + "_YAXIS")));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_MOUSE_X"),
                                    new XElement("newseq", new XAttribute("type", "standard"), "MOUSECODE_" + mouseIndex2 + "_XAXIS")));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_MOUSE_Y"),
                                    new XElement("newseq", new XAttribute("type", "standard"), "MOUSECODE_" + mouseIndex2 + "_YAXIS")));
                        }
                        else
                        {
                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_LIGHTGUN_X"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0))));

                            input.Add(new XElement
                                ("port", new XAttribute("type", "P" + i + "_LIGHTGUN_Y"),
                                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0))));
                        }
                    }
                    #endregion
                }
            }
            #endregion

            XDocument xdoc = new XDocument
                (
                    new XDeclaration("1.0", null, null)
                );
            
            xdoc.Add(mameconfig);
            mameconfig.Add(system);
            system.Add(input);

            xdoc.Save(inputConfig);

            if (!File.Exists(inputConfig))
                return false;

            return true;
        }

        private string GetDinputMapping(SdlToDirectInput c, string buttonkey, bool isXinput, int axisDirection = 0)
        {
            if (c == null)
                return "";

            if (c.ButtonMappings == null)
            {
                SimpleLogger.Instance.Info("[INFO] No mapping found for the controller.");
                return "";
            }

            if (!c.ButtonMappings.ContainsKey(buttonkey))
            {
                SimpleLogger.Instance.Info("[INFO] No mapping found for " + buttonkey + " in gamecontrollerdb file");
                return "";
            }

            string button = c.ButtonMappings[buttonkey];

            // For xInput : specific treatment of axis
            if (isXinput && button == "a5")
                button = "a2";
            if (isXinput && button == "a2")
                axisDirection = 1;

            if (button.StartsWith("b"))
            {
                int buttonID = (button.Substring(1).ToInteger()) + 1;
                return "BUTTON" + buttonID;
            }

            else if (button.StartsWith("h"))
            {
                int hatID = button.Substring(3).ToInteger();

                switch (hatID)
                {
                    case 1:
                        return "HAT1UP";
                    case 2:
                        return "HAT1RIGHT";
                    case 4:
                        return "HAT1DOWN";
                    case 8:
                        return "HAT1LEFT";
                }
            }

            else if (button.StartsWith("a") || button.StartsWith("-a") || button.StartsWith("+a"))
            {
                int axisID = button.Substring(1).ToInteger();

                if (button.StartsWith("-a"))
                {
                    axisID = button.Substring(2).ToInteger();
                    axisDirection = -1;
                }
                if (button.StartsWith("+a"))
                {
                    axisID = button.Substring(2).ToInteger();
                    axisDirection = 1;
                }

                switch (axisID)
                {
                    case 0:
                        if (axisDirection == 1) return "XAXIS_RIGHT_SWITCH";
                        else if (axisDirection == -1) return "XAXIS_LEFT_SWITCH";
                        else return "XAXIS";
                    case 1:
                        if (axisDirection == 1) return "YAXIS_DOWN_SWITCH";
                        else if (axisDirection == -1) return "YAXIS_UP_SWITCH";
                        else return "YAXIS";
                    case 2:
                        if (axisDirection == 1) return "ZAXIS_POS_SWITCH";
                        else if (axisDirection == -1) return "ZAXIS_NEG_SWITCH";
                        else return "ZAXIS";
                    case 3:
                        if (axisDirection == 1) return "RXAXIS_POS_SWITCH";
                        else if (axisDirection == -1) return "RXAXIS_NEG_SWITCH";
                        else return "RXAXIS";
                    case 4:
                        if (axisDirection == 1) return "RYAXIS_POS_SWITCH";
                        else if (axisDirection == -1) return "RYAXIS_NEG_SWITCH";
                        else return "RYAXIS";
                    case 5:
                        if (axisDirection == 1) return "RZAXIS_POS_SWITCH";
                        else if (axisDirection == -1) return "RZAXIS_NEG_SWITCH";
                        else return "RZAXIS";
                }
            }

            return "";
        }

        #region Xinput mapping dictionnaries
        static Dictionary<string, string> xInputMapping = new Dictionary<string, string>()
        {
            { "l3",             "BUTTON7"},
            { "r3",             "BUTTON8"},
            { "l2",             "SLIDER1" },
            { "r2",             "SLIDER2"},
            { "north",          "BUTTON4" },
            { "south",          "BUTTON1" },
            { "west",           "BUTTON3" },
            { "east",           "BUTTON2" },
            { "start",          "START" },
            { "select",         "SELECT" },
            { "l1",             "BUTTON5" },
            { "r1",             "BUTTON6" },
            { "up",             "HAT1UP" },
            { "down",           "HAT1DOWN" },
            { "left",           "HAT1LEFT" },
            { "right",          "HAT1RIGHT" },
            { "lsup",           "YAXIS_UP_SWITCH" },
            { "lsdown",         "YAXIS_DOWN_SWITCH" },
            { "lsleft",         "XAXIS_LEFT_SWITCH" },
            { "lsright",        "XAXIS_RIGHT_SWITCH"},
            { "rsup",           "RZAXIS_NEG_SWITCH" },
            { "rsdown",         "RZAXIS_POS_SWITCH" },
            { "rsleft",         "ZAXIS_NEG_SWITCH" },
            { "rsright",        "ZAXIS_POS_SWITCH"},
            { "ls_x",           "XAXIS" },
            { "ls_y",           "YAXIS" },
            { "rs_x",           "ZAXIS" },
            { "rs_y",           "RZAXIS"}
        };

        static Dictionary<string, string> hbxInputMapping = new Dictionary<string, string>()
        {
            { "l3",             "BUTTON9"},
            { "r3",             "BUTTON10"},
            { "l2",             "RZAXIS_NEG_SWITCH" },  //differs
            { "r2",             "ZAXIS_NEG_SWITCH"},    //differs
            { "north",          "BUTTON4" },
            { "south",          "BUTTON1" },
            { "west",           "BUTTON3" },
            { "east",           "BUTTON2" },
            { "start",          "BUTTON7" },    //differs
            { "select",         "BUTTON8" },    //differs
            { "l1",             "BUTTON5" },
            { "r1",             "BUTTON6" },
            { "up",             "DPADUP" },     //differs
            { "down",           "DPADDOWN" },   //differs
            { "left",           "DPADLEFT" },   //differs
            { "right",          "DPADRIGHT" },  //differs
            { "lsup",           "YAXIS_UP_SWITCH" },
            { "lsdown",         "YAXIS_DOWN_SWITCH" },
            { "lsleft",         "XAXIS_LEFT_SWITCH" },
            { "lsright",        "XAXIS_RIGHT_SWITCH"},
            { "rsup",           "RYAXIS_NEG_SWITCH" },  //differs
            { "rsdown",         "RYAXIS_POS_SWITCH" },  //differs
            { "rsleft",         "RXAXIS_NEG_SWITCH" },  //differs
            { "rsright",        "RXAXIS_POS_SWITCH"},   //differs
            { "ls_x",           "XAXIS" },
            { "ls_y",           "YAXIS" },
            { "rs_x",           "ZAXIS" },
            { "rs_y",           "RZAXIS"}
        };
        #endregion
    }
}
