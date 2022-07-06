using ParallelTasks;
using Sandbox.Game.Entities;
using VRage.Game;

namespace AutoBeacon
{
    internal class BeaconWorkData : WorkData
    {
        public readonly MyCubeGrid CubeGrid;
        public float BlockMass;
        public MyCubeSize MaxCubeSize;
        public float MaxDimensions;
        public int QualifiedPCU;

        public BeaconWorkData(MyCubeGrid cubeGrid)
        {
            CubeGrid = cubeGrid;
        }
    }
}