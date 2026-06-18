using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.Utils.Collections;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Guild (Yeats).</summary>
public class Guild : ConsoleCommand
{
    public Guild()
        : base("guild", "Displays info about given player's legion.")
    {
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        Player target = paramsArr.Length > 0 ? Aion.GameServer.World.World.GetInstance().GetPlayer(paramsArr[0]) : null;
        if (target != null)
        {
            Legion legion = target.GetLegion();
            if (target.GetLegion() != null)
            {
                PacketSendUtility.SendPacket(admin, new SM_GM_SHOW_LEGION_INFO(legion));
                SplitList<LegionMember> legionMemberSplitList = new FixedElementCountSplitList<LegionMember>(legion.GetMembers(), true, 80);
                foreach (var part in legionMemberSplitList)
                    PacketSendUtility.SendPacket(admin, new SM_GM_SHOW_LEGION_MEMBERLIST(part, part.IsFirst(), part.IsLast()));
            }
        }
    }
}
