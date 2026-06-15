using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/esoterrace/WardenSuramaAI (xTz).</summary>
[AIName("wardensurama")]
public class WardenSuramaAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(50, 25, 5);

    public WardenSuramaAI(Npc owner)
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
        SpawnGeysers();
    }

    private void SpawnGeysers()
    {
        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19332, 50, GetOwner()).UseNoAnimationSkill();
        Spawn(282171, 1317.097656f, 1145.419556f, 53.203529f, (sbyte)0, 595);
        Spawn(282172, 1343.426147f, 1170.675293f, 53.203529f, (sbyte)0, 596);
        Spawn(282173, 1316.953979f, 1196.861328f, 53.203529f, (sbyte)0, 598);
        Spawn(282174, 1290.778442f, 1170.730957f, 53.203529f, (sbyte)0, 597);
        Spawn(282425, 1305.310059f, 1159.337769f, 53.203529f, (sbyte)0, 721);
        Spawn(282426, 1328.446289f, 1159.062500f, 53.203529f, (sbyte)0, 718);
        Spawn(282427, 1328.613770f, 1182.369873f, 53.203529f, (sbyte)0, 722);
        Spawn(282428, 1305.083130f, 1182.424927f, 53.203529f, (sbyte)0, 719);

        PacketSendUtility.BroadcastPacket(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_IDF4Re_Drana_10());
        PacketSendUtility.BroadcastPacket(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_IDF4Re_Drana_09());
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            DespawnGeysers();
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(13000));
    }

    private void DespawnGeysers()
    {
        foreach (Npc npc in GetPosition().GetWorldMapInstance().GetNpcs(282171, 282172, 282173, 282174, 282425, 282426, 282427, 282428))
            npc.GetController().Delete();
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        hpPhases.Reset();
    }
}
