using Sandbox.ModAPI;
using VRage.ModAPI;

namespace AutoBeacon
{
    public static class Util
    {
        public static bool IsClient =>
            MyAPIGateway.Multiplayer.MultiplayerActive && !MyAPIGateway.Multiplayer.IsServer;

        public static bool IsValid(IMyEntity entity) => entity != null && !entity.MarkedForClose;
    }
}