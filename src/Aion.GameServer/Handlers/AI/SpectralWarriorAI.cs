using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/empyreanCrucible/SpectralWarriorAI (Luzien).</summary>
[AIName("spectral_warrior")]
public class SpectralWarriorAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(50);

    public SpectralWarriorAI(Npc owner)
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
        GetPosition().GetWorldMapInstance().GetInstanceHandler().OnChangeStage(StageType.START_STAGE_6_ROUND_5);
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            ResurrectAllies();
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(2000));
    }

    private void ResurrectAllies()
    {
        GetKnownList().ForEachNpc(npc =>
        {
            if (npc.IsDead())
                return;

            switch (npc.GetNpcId())
            {
                case 205413:
                    Spawn(217576, npc.GetX(), npc.GetY(), npc.GetZ(), (sbyte)npc.GetHeading());
                    npc.GetController().Delete();
                    break;
                case 205414:
                    Spawn(217577, npc.GetX(), npc.GetY(), npc.GetZ(), (sbyte)npc.GetHeading());
                    npc.GetController().Delete();
                    break;
            }
        });
    }
}
