using System.Collections.Generic;

using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/events/RainbowSnakeAI.</summary>
[AIName("rainbow_snake")]
public class RainbowSnakeAI : GeneralNpcAI
{
    private readonly HashSet<int> messagedPlayers = new HashSet<int>(); // TODO remove + fix NpcShoutsService

    public RainbowSnakeAI(Npc npc)
        : base(npc)
    {
    }

    public override void HandleCreatureDetected(Creature creature)
    {
        base.HandleCreatureDetected(creature);
        if (creature is Player player && messagedPlayers.Add(player.GetObjectId()))
            PacketSendUtility.SendMessage(player, GetOwner(), 1501203); // Hey, you there... Yeah, you... Come here!
    }

    protected override void HandleCreatureNotSee(Creature creature)
    {
        base.HandleCreatureNotSee(creature);
        messagedPlayers.Remove(creature.GetObjectId());
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        if (dialogActionId != DialogAction.SETPRO1)
            return false;
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(0, 0));
        switch (GetNpcId())
        {
            case 832963:
            case 832974: // Serpente Rumin (FRI-SUN)
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 10976, 1, player).UseWithoutPropSkill(); // [Event] Rainbow Snake's Splendor (Drop rate +50%)
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 10977, 1, player).UseWithoutPropSkill(); // [Event] Rainbow Snake's Grace (Gathering XP +100%)
                break;
            case 832964:
            case 832975: // Ahas Rumin (MON-THU)
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 10978, 1, player).UseWithoutPropSkill(); // [Event] Rainbow Snake's Love (Crafting XP +100%)
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 10979, 1, player).UseWithoutPropSkill(); // [Event] Rainbow Snake's Judgement (AP +50%)
                break;
        }
        return true;
    }
}
