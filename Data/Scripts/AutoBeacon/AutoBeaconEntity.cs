using System;
using System.Collections.Generic;
using ParallelTasks;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Network;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Sync;
using VRage.Utils;
using VRageMath;

namespace AutoBeacon
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Beacon), false, "SmallBlockBeacon", "LargeBlockBeacon")]
    public class AutoBeaconEntityComponent : MyGameLogicComponent
    {
        private static readonly MyDefinitionId Electricity = MyResourceDistributorComponent.ElectricityId;
        private IMyBeacon beaconBlock;
        private IMyCubeGrid cubeGrid;
        private float radius;
        private float weatherModifier;
        private float decaySpeedMod;
        private int nextScanTick;
        private bool updateBeacon;
        private Task? scanTask;

        // Expected that this is never initialized, the game takes care of it
        private MySync<float, SyncDirection.FromServer> syncAutoRangeRadius;

        private MyResourceSinkComponent ResourceSink => (MyResourceSinkComponent)beaconBlock.ResourceSink;

        public override void OnAddedToContainer()
        {
            base.OnAddedToContainer();

            if (Util.IsClient)
            {
                return;
            }

            var config = AutoBeaconSessionComponent.Instance?.Config;
            if (config == null || !Util.IsValid(cubeGrid))
            {
                return;
            }

            syncAutoRangeRadius.Value = config.MinBeaconRadius;
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            beaconBlock = (IMyBeacon)Entity;
            cubeGrid = beaconBlock.CubeGrid;
            if (!MyAPIGateway.Session.IsServer && MyAPIGateway.Multiplayer.MultiplayerActive)
            {
                NeedsUpdate = MyEntityUpdateEnum.EACH_10TH_FRAME;
                syncAutoRangeRadius.ValueChanged += ClientOnRadiusChanged;
                return;
            }

            NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME | MyEntityUpdateEnum.BEFORE_NEXT_FRAME;

            AddHandlers(beaconBlock, cubeGrid);

            beaconBlock.Radius = syncAutoRangeRadius.Value;
            beaconBlock.HudText = CreateBeaconName(beaconBlock.CubeGrid, AutoBeaconSessionComponent.Instance?.Config);
            beaconBlock.Enabled = true;
        }

        public override void Close()
        {
            beaconBlock = null;
            cubeGrid = null;
        }

        public override void UpdateOnceBeforeFrame()
        {
            var sink = ResourceSink;
            if (sink == null)
            {
                return;
            }

            sink.SetMaxRequiredInputByType(Electricity, 0);
            sink.SetRequiredInputFuncByType(Electricity, () => 0);
            sink.Update();

            StartScan();
        }

        // Used to handle initial replication from server to client.
        public override void UpdateBeforeSimulation10()
        {
            if (!Util.IsClient)
            {
                return;
            }

            if (ResourceSink.MaxRequiredInputByType(Electricity) > 0)
            {
                ResourceSink.SetMaxRequiredInputByType(Electricity, 0);
                ResourceSink.SetRequiredInputFuncByType(Electricity, () => 0);
                ResourceSink.Update();
            }

            if (syncAutoRangeRadius.Value > 0)
            {
                beaconBlock.Radius = syncAutoRangeRadius.Value;
                beaconBlock.Enabled = true;
                MyGameLogic.ChangeUpdate(this, MyEntityUpdateEnum.NONE);
            }
            else
            {
                beaconBlock.Radius = 0;
                beaconBlock.Enabled = true;
            }
        }

        public override void UpdateBeforeSimulation100()
        {
            if (!Util.IsValid(cubeGrid))
            {
                return;
            }

            var config = AutoBeaconSessionComponent.Instance?.Config;
            if (config == null || !Util.IsValid(cubeGrid))
            {
                return;
            }

            if (decaySpeedMod > 0f)
            {
                decaySpeedMod = Math.Max(0f, decaySpeedMod - 1 / (config.CooldownSecs * 60 / 100));
            }

            var oldRadius = radius;
            if (!updateBeacon && MyAPIGateway.Session.GameplayFrameCounter < nextScanTick)
            {
                SetBeaconRadius(config, radius);
                if (Math.Abs(oldRadius - radius) < 1f)
                {
                    return;
                }

                var newHudText = CreateBeaconName(beaconBlock.CubeGrid, config);
                if (beaconBlock.HudText != newHudText)
                {
                    beaconBlock.HudText = newHudText;
                }

                return;
            }

            if (!beaconBlock.Enabled)
            {
                beaconBlock.Enabled = true;
            }

            weatherModifier = GetWeatherModifier(config, cubeGrid.PositionComp.GetPosition());

            var linearVelocity = Vector3.Zero;
            if (cubeGrid.Physics != null)
            {
                linearVelocity = cubeGrid.Physics.LinearVelocity;
            }

            // If we're already at 0 speed and 0 velocity and not decaying speed, and this was not triggered
            // by an update, skip the scan entirely. If the grid changes, we don't need to know until moving again.
            if (!updateBeacon && decaySpeedMod < 1f && Vector3.IsZero(linearVelocity, 1f) &&
                Math.Abs(beaconBlock.Radius - config.MinBeaconRadius) < 1f)
            {
                nextScanTick = MyAPIGateway.Session.GameplayFrameCounter + config.ForceRescanPeriodSecs * 60;
                return;
            }

            StartScan();
            updateBeacon = false;
        }

        private void StartScan()
        {
            var grid = cubeGrid as MyCubeGrid;
            // IsValid provides null check
            if (!Util.IsValid(grid))
            {
                return;
            }

            if (grid.Physics == null)
            {
                return;
            }

            if (scanTask != null)
            {
                return;
            }

            var workData = new BeaconWorkData(grid);
            scanTask = MyAPIGateway.Parallel.Start(ScanGridAction, ScanGridCallback, workData);
        }

        private void ScanGridAction(WorkData data)
        {
            var workData = (BeaconWorkData)data;
            var scanGrid = workData.CubeGrid;

            var blocks = new List<IMySlimBlock>(scanGrid.BlocksCount);
            var connectedGrids = scanGrid.GetConnectedGrids(GridLinkTypeEnum.Mechanical);
            foreach (IMyCubeGrid connectedGrid in connectedGrids)
            {
                if (!Util.IsValid(connectedGrid))
                {
                    continue;
                }

                blocks.Clear();
                connectedGrid.GetBlocks(blocks);
                ScanBlocks(blocks, workData);

                var length = (connectedGrid.Max - connectedGrid.Min).Length() * connectedGrid.GridSize;
                if (length > workData.MaxDimensions)
                {
                    workData.MaxDimensions = length;
                    workData.MaxCubeSize = connectedGrid.GridSizeEnum;
                }
            }
        }

        private static void ScanBlocks(List<IMySlimBlock> blocks, BeaconWorkData workData)
        {
            foreach (var block in blocks)
            {
                var definition = block?.BlockDefinition as MyCubeBlockDefinition;
                if (definition == null)
                {
                    continue;
                }

                workData.BlockMass += definition.Mass;

                if (definition.PCU <= 0)
                {
                    continue;
                }

                if (definition is MyShipToolDefinition ||
                    definition is MyMechanicalConnectionBlockBaseDefinition ||
                    definition is MyBeaconDefinition)
                {
                    continue;
                }

                // If it's not a tool or a mechanical connection block and it has PCU, count it as a weapon!
                workData.QualifiedPCU += definition.PCU;
            }
        }

        private void ScanGridCallback(WorkData data)
        {
            var workData = (BeaconWorkData)data;
            if (cubeGrid != workData.CubeGrid || !Util.IsValid(workData.CubeGrid) || !Util.IsValid(Entity))
            {
                return;
            }

            var config = AutoBeaconSessionComponent.Instance?.Config;
            if (config == null)
            {
                return;
            }

            nextScanTick = MyAPIGateway.Session.GameplayFrameCounter + config.ForceRescanPeriodSecs * 60;

            var oldRadius = radius;

            SetBeaconRadius(config, CalculateRadius(config,
                workData.QualifiedPCU, workData.BlockMass, workData.MaxDimensions, workData.MaxCubeSize));

            if (Math.Abs(oldRadius - radius) > 1f)
            {
                var newHudText = CreateBeaconName(beaconBlock.CubeGrid, config);
                if (beaconBlock.HudText != newHudText)
                {
                    beaconBlock.HudText = newHudText;
                }
            }

            if (!beaconBlock.Enabled)
            {
                beaconBlock.Enabled = true;
            }

            scanTask = null;
        }

        private void SetBeaconRadius(BeaconConfiguration config, float newRadius)
        {
            radius = newRadius;

            if (cubeGrid.Physics != null)
            {
                var speedMod = GetSpeedModifier(cubeGrid.Physics.LinearVelocity);
                decaySpeedMod = Math.Max(speedMod, decaySpeedMod);

                newRadius *= (float)Math.Round(decaySpeedMod, 1);
            }

            if (newRadius > 0)
            {
                newRadius *= weatherModifier;
            }

            newRadius = MathHelper.Clamp(newRadius, config.MinBeaconRadius, config.MaxBeaconRadius);

            // Don't bother updating it if it hasn't changed or is a very small change.
            if (Math.Abs(beaconBlock.Radius - newRadius) < 25f)
            {
                return;
            }

            beaconBlock.Radius = newRadius;
            syncAutoRangeRadius.Value = beaconBlock.Radius;
        }

        private string CreateBeaconName(IMyCubeGrid grid, BeaconConfiguration config)
        {
            return grid.CustomName.Contains(" Grid ")
                ? $"{config?.OverrideFallbackName ?? grid.CustomName} ({radius:0000})"
                : $"{grid.CustomName} ({radius:0000})";
        }

        private static float CalculateRadius(BeaconConfiguration config, int pcu, float blockMass, float dimensions,
            MyCubeSize cubeSize)
        {
            var pcuWeight = MathHelper.Clamp(pcu / config.MaxWeaponPCU, 0f, 1f) * config.WeaponPCUWeight;

            var dimensionsWeight =
                MathHelper.Clamp(
                    dimensions / (config.maxGridDimensionsLength * (cubeSize == MyCubeSize.Large ? 2.5 : 0.5)), 0f,
                    1f) * config.GridDimensionsWeight;

            var massWeight = MathHelper.Clamp(blockMass / config.MaxRangeBlockMass, 0f, 1f) * config.BlockMassWeight;

            var totalWeight = config.WeaponPCUWeight + config.GridDimensionsWeight + config.BlockMassWeight;
            var rangePercent = (pcuWeight + dimensionsWeight + massWeight) / totalWeight;

            var newRadius = Math.Floor(config.MaxBeaconRadius * rangePercent);

            if (cubeSize == MyCubeSize.Small)
            {
                newRadius *= config.SmallGridRangeFactor;
            }

            return (float)newRadius;
        }

        private static float GetWeatherModifier(BeaconConfiguration config, Vector3D position)
        {
            var weather = MyAPIGateway.Session.WeatherEffects.GetWeather(position);
            float weatherModifer;
            if (config.AffectingWeatherTypes.Dictionary.TryGetValue(weather, out weatherModifer))
            {
                return MathHelper.Clamp(
                    MyAPIGateway.Session.WeatherEffects.GetWeatherIntensity(position) / config.WeatherPeakPoint,
                    0, 1) * (1 - weatherModifer);
            }

            return 1f;
        }

        private static float GetSpeedModifier(Vector3 velocity)
        {
            if (Vector3.IsZero(velocity, 10f))
            {
                return 0;
            }

            return MathHelper.Clamp((int)(velocity.Length() / 10f) * 10f / 50f, 0, 1f);
        }

        private void BeaconOnEnabledChanged(IMyTerminalBlock terminalBlock)
        {
            var block = terminalBlock as IMyBeacon;
            // IsValid provides null check
            if (!Util.IsValid(block))
            {
                return;
            }

            if (block.Enabled)
            {
                return;
            }

            block.Enabled = true;
        }

        private void CubeGridOnGridMerge(IMyCubeGrid keep, IMyCubeGrid lost)
        {
            if (Util.IsValid(beaconBlock) && cubeGrid == lost)
            {
                RemoveHandlers(lost);
            }

            cubeGrid = keep;
            AddHandlers(keep);
            MarkForUpdate();
        }

        private void CubeGridOnGridSplit(IMyCubeGrid first, IMyCubeGrid second)
        {
            RemoveHandlers(first);
            RemoveHandlers(second);
            if (first == beaconBlock.SlimBlock.CubeGrid)
            {
                AddHandlers(first);
                cubeGrid = first;
            }
            else
            {
                AddHandlers(second);
                cubeGrid = second;
            }

            MarkForUpdate();
        }

        private void CubeGridOnIsStaticChanged(IMyCubeGrid grid, bool isStatic)
        {
            MarkForUpdate();
        }

        private void BeaconOnClosing(IMyEntity obj)
        {
            var closingBlock = obj as IMyBeacon;
            if (closingBlock != null && closingBlock == beaconBlock)
            {
                RemoveHandlers(beaconBlock, cubeGrid);
            }
        }

        private void ClientOnRadiusChanged(MySync<float, SyncDirection.FromServer> obj)
        {
            if (!Util.IsValid(beaconBlock))
            {
                return;
            }

            beaconBlock.Radius = obj.Value;
        }

        private void AddHandlers(IMyBeacon beacon, IMyCubeGrid grid)
        {
            AddHandlers(beacon);
            AddHandlers(grid);
        }

        private void AddHandlers(IMyBeacon beacon)
        {
            if (Util.IsValid(beacon))
            {
                beacon.EnabledChanged += BeaconOnEnabledChanged;
                beacon.OnClosing += BeaconOnClosing;
            }
        }

        private void AddHandlers(IMyCubeGrid grid)
        {
            if (Util.IsValid(grid))
            {
                grid.OnGridMerge += CubeGridOnGridMerge;
                grid.OnGridSplit += CubeGridOnGridSplit;
                grid.OnIsStaticChanged += CubeGridOnIsStaticChanged;
            }
        }

        private void RemoveHandlers(IMyBeacon beacon, IMyCubeGrid grid)
        {
            RemoveHandlers(beacon);
            RemoveHandlers(grid);
        }

        private void RemoveHandlers(IMyBeacon beacon)
        {
            if (Util.IsValid(beacon))
            {
                beacon.EnabledChanged -= BeaconOnEnabledChanged;
                beacon.OnMarkForClose -= BeaconOnClosing;
            }
        }

        private void RemoveHandlers(IMyCubeGrid grid)
        {
            if (Util.IsValid(grid))
            {
                grid.OnGridMerge -= CubeGridOnGridMerge;
                grid.OnGridSplit -= CubeGridOnGridSplit;
                grid.OnIsStaticChanged -= CubeGridOnIsStaticChanged;
            }
        }

        private void MarkForUpdate()
        {
            updateBeacon = true;
        }
    }
}