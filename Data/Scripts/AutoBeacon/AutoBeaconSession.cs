using System.Collections.Generic;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.Components;

namespace AutoBeacon
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class AutoBeaconSessionComponent : MySessionComponentBase
    {
        private static readonly List<string> DisableTerminalActionIds = new List<string>
        {
            "OnOff", "OnOff_On", "OnOff_Off", "IncreaseRadius", "DecreaseRadius"
        };

        public static AutoBeaconSessionComponent Instance { get; private set; }
        public BeaconConfiguration Config { get; private set; }

        public override void LoadData()
        {
            MyAPIGateway.TerminalControls.CustomActionGetter += HandleBeaconActions;
            MyAPIGateway.TerminalControls.CustomControlGetter += HandleBeaconControls;

            if (MyAPIGateway.Session.IsServer || !MyAPIGateway.Multiplayer.MultiplayerActive)
            {
                Config = BeaconConfiguration.LoadSettings();
            }

            Instance = this;
        }

        protected override void UnloadData()
        {
            MyAPIGateway.TerminalControls.CustomActionGetter -= HandleBeaconActions;
            MyAPIGateway.TerminalControls.CustomControlGetter -= HandleBeaconControls;
            Instance = null;
        }

        private void HandleBeaconControls(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            var beaconBlock = block as IMyBeacon;
            var logic = beaconBlock?.GameLogic.GetAs<AutoBeaconEntityComponent>();
            if (logic == null)
            {
                return;
            }

            List<IMyTerminalControl> controlList;
            MyAPIGateway.TerminalControls.GetControls<IMyBeacon>(out controlList);

            for (var index = 0; index < controls.Count; index++)
            {
                var control = controls[index];

                if (control == null)
                {
                    continue;
                }

                switch (control.Id)
                {
                    case "OnOff":
                        if (control is IMyTerminalControlOnOffSwitch)
                        {
                            control.Visible = terminalBlock => false;

                            // Hide the separator as well
                            if (index + 1 >= controls.Count)
                            {
                                continue;
                            }

                            var nextControl = controls[index + 1];
                            if (nextControl is IMyTerminalControlSeparator)
                            {
                                nextControl.Visible = terminalBlock => false;
                            }
                        }

                        break;
                    case "HudText":
                    case "Radius":
                        break;
                    default:
                        continue;
                }

                control.Enabled = terminalBlock => false;
            }
        }

        private void HandleBeaconActions(IMyTerminalBlock block, List<IMyTerminalAction> actions)
        {
            var beaconBlock = block as IMyBeacon;
            var logic = beaconBlock?.GameLogic.GetAs<AutoBeaconEntityComponent>();
            if (logic == null)
            {
                return;
            }

            actions.RemoveAll(a => DisableTerminalActionIds.Contains(a.Id));
        }
    }
}