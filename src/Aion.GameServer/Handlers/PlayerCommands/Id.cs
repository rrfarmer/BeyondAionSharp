using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.Utils.Extensions;

namespace Aion.GameServer.Handlers.PlayerCommands;

/// <summary>Java parity: data/handlers/playercommands/Id (Neon). Shows item/quest/npc IDs.</summary>
public class Id : PlayerCommand
{
    // Java parity: '' (bag icon, client private-use glyph).
    private const char ITEM_ICON = (char)0xE054;

    public Id()
        : base("id", "Shows item/quest/npc IDs.")
    {
        SetSyntaxInfo(
            " - Shows the ID of the selected object.",
            "<item|quest> - Shows the ID of the specified item or quest.");
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        VisibleObject target = player.GetTarget();

        if (paramsArr.Length == 0)
        {
            if (target == null)
            {
                SendInfo(player);
                return;
            }

            if (!player.IsStaff() && !(target is Npc) && !(target is Gatherable)) // regular players only see IDs of npcs and gatherables
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_INVALID_TARGET());
                return;
            }

            string msg = target.GetType().Name + ": " + ChatUtil.Path(target, true);
            if (player.IsStaff())
            {
                int staticId = target.GetSpawn() == null ? 0 : target.GetSpawn().GetStaticId();
                msg += " (Object ID: " + target.GetObjectId() + (staticId == 0 ? "" : ", Static ID: " + staticId) + ")";
            }
            SendInfo(player, msg);
            return;
        }
        else
        {
            int id = ChatUtil.GetItemId(paramsArr[0]);
            if (id != 0)
            {
                ItemTemplate template = DataManager.ITEM_DATA.GetItemTemplate(id);
                if (template == null)
                {
                    SendInfo(player, "Invalid item.");
                    return;
                }
                SendInfo(player, "Item:" + ITEM_ICON + template.GetL10n() + "\nID: " + id);
                return;
            }

            id = ChatUtil.GetQuestId(paramsArr[0]);
            if (id != 0)
            {
                QuestTemplate template = DataManager.QUEST_DATA.GetQuestById(id);
                if (template == null)
                {
                    SendInfo(player, "Invalid quest.");
                    return;
                }
                SendInfo(player, "Quest:" + GetQuestIcon(template) + template.GetL10n() + "\nID: " + id);
                return;
            }
        }

        SendInfo(player);
    }

    private char GetQuestIcon(QuestTemplate template)
    {
        switch (template.GetCategory())
        {
            case QuestCategory.EVENT:
                return (char)0xE039; // pink
            case QuestCategory.MISSION:
                return (char)0xE037; // golden
            case QuestCategory.IMPORTANT:
            case QuestCategory.SIGNIFICANT:
                return (char)0xE03F; // dark blue
        }
        return (char)0xE034; // light blue
    }
}
