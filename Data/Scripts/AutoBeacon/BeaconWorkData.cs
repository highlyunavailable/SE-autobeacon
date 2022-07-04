using ParallelTasks;
using Sandbox.Game.Entities;

namespace AutoBeacon
{
    internal class BeaconWorkData : WorkData
    {
        public readonly MyCubeGrid CubeGrid;
        public int QualifiedPCU;
        public float BlockMass;

        public BeaconWorkData(MyCubeGrid cubeGrid)
        {
            CubeGrid = cubeGrid;
        }
    }
}