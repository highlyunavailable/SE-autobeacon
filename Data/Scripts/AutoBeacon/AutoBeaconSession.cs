using System;
using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.Components;

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

        public static AutoBeaconSessionComponent Instance { get; private set; }
        public BeaconConfiguration Config { get; private set; }

        public override void LoadData()
        {
            if (!Util.IsDedicatedServer)
            {
                MyAPIGateway.TerminalControls.CustomActionGetter += HandleBeaconActions;
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
            Instance = null;
        }

        public override void BeforeStart()
        {
            if (Util.IsDedicatedServer)
            {
                return;
            }

            List<IMyTerminalControl> controls;
            MyAPIGateway.TerminalControls.GetControls<IMyBeacon>(out controls);

            for (var index = 0; index < controls.Count; index++)
            {
                var control = controls[index];

                if (control == null)
                {
                    continue;
                }

                if (control.Id == "OnOff")
                {
                    control.Enabled = DisableIfAutoBeacon(control.Enabled);
                    control.Visible = DisableIfAutoBeacon(control.Visible);

                    // Hide the separator as well
                    if (index + 1 >= controls.Count)
                    {
                        continue;
                    }

                    var nextControl = controls[index + 1];
                    if (nextControl is IMyTerminalControlSeparator)
                    {
                        nextControl.Visible = DisableIfAutoBeacon(nextControl.Visible);
                    }
                }
                else if (control.Id == "HudText" || control.Id == "Radius")
                {
                    control.Enabled = DisableIfAutoBeacon(control.Enabled);
                }
            }

            List<IMyTerminalAction> actions;
            MyAPIGateway.TerminalControls.GetActions<IMyBeacon>(out actions);

            foreach (var action in actions)
            {
                if (DisableTerminalActionIds.Contains(action.Id))
                {
                    action.Enabled = DisableIfAutoBeacon(action.Enabled);
                }
            }
        }

        private Func<IMyTerminalBlock, bool> DisableIfAutoBeacon(Func<IMyTerminalBlock, bool> wrapFunc)
        {
            return terminalBlock =>
                wrapFunc(terminalBlock) &&
                terminalBlock.GameLogic.GetAs<AutoBeaconEntityComponent>()?.IgnoredBeacon != false;
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