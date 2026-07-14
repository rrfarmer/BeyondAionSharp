using System.Collections.Generic;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Assemblednpc;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Assemblednpc;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.Utils.IdFactory;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/SpawnAssembledNpc (xTz).</summary>
public class SpawnAssembledNpc : AdminCommand
{
    public SpawnAssembledNpc()
        : base("spawnAssembledNpc")
    {
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length != 1)
        {
            Info(player, null);
            return;
        }
        int spawnId = 0;
        if (!TryParseInt(paramsArr[0], out spawnId))
        {
            Info(player, null);
            return;
        }

        AssembledNpcTemplate template = DataManager.ASSEMBLED_NPC_DATA.GetAssembledNpcTemplate(spawnId);
        if (template == null)
        {
            PacketSendUtility.SendMessage(player, "This spawnId is wrong.");
            return;
        }
        List<AssembledNpcPart> assembledParts = new List<AssembledNpcPart>();
        foreach (AssembledNpcTemplate.AssembledNpcPartTemplate npcPart in template.GetAssembledNpcPartTemplates())
        {
            assembledParts.Add(new AssembledNpcPart(IDFactory.GetInstance().NextId(), npcPart));
        }
        AssembledNpc npc = new AssembledNpc(template.GetRouteId(), template.GetMapId(), template.GetLiveTime(), assembledParts);
        PacketSendUtility.BroadcastToWorld(new SM_NPC_ASSEMBLER(npc));
    }

    private void Info(Player player, string message)
    {
        PacketSendUtility.SendMessage(player, "syntax //spawnAssembledNpc <spawnId>");
    }
}
