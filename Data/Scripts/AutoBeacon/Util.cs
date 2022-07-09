using System;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.ModAPI;

namespace AutoBeacon
{
    public static class Util
    {
        public static bool IsValid(IMyEntity obj)
        {
            return obj != null && !obj.MarkedForClose;
        }

        public static bool IsClient =>
            MyAPIGateway.Multiplayer.MultiplayerActive && !MyAPIGateway.Multiplayer.IsServer;
    }
}