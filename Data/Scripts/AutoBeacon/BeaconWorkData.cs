using ParallelTasks;
using Sandbox.Game.Entities;

namespace AutoBeacon
{
    internal class BeaconWorkData : WorkData
    {
        public readonly MyCubeGrid CubeGrid;
        public float BlockMass;
        public int QualifiedPCU;

        public BeaconWorkData(MyCubeGrid cubeGrid)
        {
            CubeGrid = cubeGrid;
        }
    }
}