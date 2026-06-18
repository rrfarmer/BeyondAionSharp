using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Actions;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Megaphone (ginho1, Neon). Sends a message to the global faction chat (client must be started with -megaphone).</summary>
public class Megaphone : AdminCommand
{
    private readonly List<MegaphoneChatColor> colors;

    public Megaphone()
        : base("megaphone", "Sends a message to the global faction chat (client must be started with -megaphone to show the megaphone chat window).")
    {
        colors = CollectColors();

        SetSyntaxInfo(
            "<none|elyos|asmo> <name> <message> - Sends the message with given sender name and faction prefix.",
            "<color ID> <none|elyos|asmo> <name> <message> - Sends the message in the color of given color ID.",
            "Color IDs: " + ColorIds());
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length < 3)
        {
            SendInfo(admin);
            return;
        }

        int i = 0;
        int colorIndex = Regex.IsMatch(paramsArr[i], "^\\d+$") ? int.Parse(paramsArr[i++]) - 1 : 0;
        if (colorIndex >= colors.Count)
        {
            SendInfo(admin, "Invalid color ID.");
            return;
        }
        int megaphoneItemId = colors[colorIndex].MegaphoneItemId;
        string label = paramsArr[i++].ToLower();
        SM_MEGAPHONE.FactionLabel factionLabel;
        if ("none".StartsWith(label))
            factionLabel = SM_MEGAPHONE.FactionLabel.NONE;
        else if ("elyos".StartsWith(label))
            factionLabel = SM_MEGAPHONE.FactionLabel.ELYOS;
        else if ("asmodians".StartsWith(label))
            factionLabel = SM_MEGAPHONE.FactionLabel.ASMODIANS;
        else
        {
            SendInfo(admin);
            return;
        }
        string sender = paramsArr[i++];
        string message = string.Join(" ", paramsArr.Skip(i));

        PacketSendUtility.BroadcastToWorld(new SM_MEGAPHONE(factionLabel, sender, message, megaphoneItemId));
    }

    private List<MegaphoneChatColor> CollectColors()
    {
        List<MegaphoneChatColor> colors = new List<MegaphoneChatColor>();
        foreach (ItemTemplate itemTemplate in DataManager.ITEM_DATA.GetItemTemplates())
        {
            if (itemTemplate.GetActions() != null)
            {
                foreach (AbstractItemAction itemAction in itemTemplate.GetActions().GetItemActions())
                {
                    if (itemAction is MegaphoneAction && colors.All(c => c.Color != ((MegaphoneAction)itemAction).GetColor()))
                        colors.Add(new MegaphoneChatColor(itemTemplate.GetTemplateId(), ((MegaphoneAction)itemAction).GetColor()));
                }
            }
        }
        colors = colors.OrderByDescending(m => m.Color).ToList();
        return colors;
    }

    private string ColorIds()
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < colors.Count; i++)
        {
            if (sb.Length > 0)
                sb.Append(", ");
            sb.Append(ChatUtil.Color(i + 1 + " █", colors[i].Color));
        }
        return sb.ToString();
    }

    private class MegaphoneChatColor
    {
        public readonly int MegaphoneItemId;
        public readonly int Color;

        public MegaphoneChatColor(int megaphoneItemId, int color)
        {
            this.MegaphoneItemId = megaphoneItemId;
            this.Color = color;
        }
    }
}
