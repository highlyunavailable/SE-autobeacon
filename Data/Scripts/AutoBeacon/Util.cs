using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace AutoBeacon
{
    public static class Util
    {
        public static bool IsClient =>
            MyAPIGateway.Multiplayer.MultiplayerActive && !MyAPIGateway.Multiplayer.IsServer;

        public static bool IsValid(IMyEntity entity) => entity != null && !entity.MarkedForClose;

        public static bool IsNpcOwned(IMyCubeBlock obj)
        {
            if (!IsValid(obj))
            {
                return false;
            }

            if (obj.OwnerId == 0)
            {
                return false;
            }

            var faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(obj.OwnerId);

            if (faction == null)
            {
                return false;
            }

            return faction.Tag.Length > 3 || faction.IsEveryoneNpc();
        }
    }
}