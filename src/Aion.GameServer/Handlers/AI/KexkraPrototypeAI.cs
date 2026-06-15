using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/esoterrace/KexkraPrototypeAI (@author xTz).</summary>
[AIName("kexkraprototype")]
public class KexkraPrototypeAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(75);

    public KexkraPrototypeAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        hpPhases.TryEnterNextPhase(this);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        GetKnownList().ForEachPlayer(player =>
        {
            if (!player.IsDead())
            {
                PacketSendUtility.SendPacket(player, new SM_PLAY_MOVIE(false, 0, 0, 472, true));
            }
        });
        Spawn(217206, 1320.639282f, 1171.063354f, 51.494003f, (sbyte)0);
        AIActions.DeleteOwner(this);
    }
}
