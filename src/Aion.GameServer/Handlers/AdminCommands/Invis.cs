using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Invis (Divinity, Neon).</summary>
public class Invis : AdminCommand
{
    public Invis()
        : base("invis", "Sets/unsets advanced invisibility.")
    {
    }

    protected override void Execute(Player player, params string[] paramsArr)
    {
        if (!player.IsInVisualState(CreatureVisualState.HIDE20))
        {
            player.GetEffectController().SetAbnormal(AbnormalState.HIDE);
            player.SetVisualState(CreatureVisualState.HIDE20);
            player.GetController().OnHide();
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_EFFECT_INVISIBLE_BEGIN());
        }
        else
        {
            player.GetEffectController().UnsetAbnormal(AbnormalState.HIDE);
            player.UnsetVisualState(CreatureVisualState.HIDE20);
            player.GetController().OnHideEnd();
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_EFFECT_INVISIBLE_END());
        }
        PacketSendUtility.BroadcastPacket(player, new SM_PLAYER_STATE(player), true);
        // required because without a skill this isn't sent automatically (outdated abnormals can cause issues when opening a private store for example)
        PacketSendUtility.SendPacket(player, new SM_ABNORMAL_STATE(new List<Effect>(), player.GetEffectController().GetAbnormals(), 0));
    }
}
