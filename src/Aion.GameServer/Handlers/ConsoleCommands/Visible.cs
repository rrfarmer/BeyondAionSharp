using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Visible (ginho1, Neon). Unsets advanced invisibility.</summary>
public class Visible : ConsoleCommand
{
    public Visible()
        : base("visible", "Unsets advanced invisibility.")
    {
    }

    protected override void Execute(Player player, params string[] paramsArr)
    {
        if (player.IsInVisualState(CreatureVisualState.HIDE20))
        {
            player.GetEffectController().UnsetAbnormal(AbnormalState.HIDE);
            player.UnsetVisualState(CreatureVisualState.HIDE20);
            player.GetController().OnHideEnd();
            PacketSendUtility.BroadcastPacket(player, new SM_PLAYER_STATE(player), true);
        }
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_EFFECT_INVISIBLE_END());
        // required because without a skill this isn't sent automatically (outdated abnormals can cause issues when opening a private store for example)
        PacketSendUtility.SendPacket(player, new SM_ABNORMAL_STATE(new List<Effect>(), player.GetEffectController().GetAbnormals(), 0));
    }
}
