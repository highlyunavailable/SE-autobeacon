using System;
using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.Components;
using VRage.Utils;

namespace AutoBeacon
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class AutoBeaconSessionComponent : MySessionComponentBase
    {
        // Must match the value of EntityBuilderSubTypeNames in the
        // MyEntityComponentDescriptor attribute on AutoBeaconEntityComponent
        private static readonly string[] BeaconSubtypes = { "SmallBlockBeacon", "LargeBlockBeacon" };

        private static readonly List<string> DisableTerminalActionIds = new List<string>
        {
            "OnOff", "OnOff_On", "OnOff_Off", "IncreaseRadius", "DecreaseRadius"
        };

        private bool initControls;

        public static AutoBeaconSessionComponent Instance { get; private set; }
        public BeaconConfiguration Config { get; private set; }

        public override void LoadData()
        {
            if (!MyAPIGateway.Multiplayer.MultiplayerActive || !MyAPIGateway.Utilities.IsDedicated)
            {
                MyAPIGateway.TerminalControls.CustomActionGetter += HandleBeaconActions;
                MyAPIGateway.TerminalControls.CustomControlGetter += HandleBeaconControls;
            }

            foreach (var definition in MyDefinitionManager.Static.GetDefinitionsOfType<MyBeaconDefinition>())
            {
                if (!BeaconSubtypes.Contains(definition.Id.SubtypeName))
                {
                    continue;
                }

                definition.BuildProgressModels = Array.Empty<MyCubeBlockDefinition.BuildProgressModel>();
            }

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

            if (initControls)
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

                if (control.Id == "OnOff")
                {
                    var func = control.Visible;
                    Func<IMyTerminalBlock, bool> newFunc = terminalBlock =>
                    {
                        var component = terminalBlock.GameLogic.GetAs<AutoBeaconEntityComponent>();
                        return func(terminalBlock) && (component == null || component.IgnoredBeacon);
                    };

                    control.Visible = newFunc;

                    // Hide the separator as well
                    if (index + 1 >= controls.Count)
                    {
                        continue;
                    }

                    var nextControl = controls[index + 1];
                    if (nextControl is IMyTerminalControlSeparator)
                    {
                        nextControl.Visible = newFunc;
                    }
                }
                else if (control.Id == "HudText" || control.Id == "Radius")
                {
                    var func = control.Enabled;
                    control.Enabled = terminalBlock =>
                    {
                        var component = terminalBlock.GameLogic.GetAs<AutoBeaconEntityComponent>();
                        return func(terminalBlock) && (component == null || component.IgnoredBeacon);
                    };
                }
            }

            initControls = true;
        }

        private void HandleBeaconActions(IMyTerminalBlock block, List<IMyTerminalAction> actions)
        {
            var beaconBlock = block as IMyBeacon;
            var logic = beaconBlock?.GameLogic.GetAs<AutoBeaconEntityComponent>();
            if (logic == null || logic.IgnoredBeacon)
            {
                return;
            }

            actions.RemoveAll(a => DisableTerminalActionIds.Contains(a.Id));
        }
    }
}