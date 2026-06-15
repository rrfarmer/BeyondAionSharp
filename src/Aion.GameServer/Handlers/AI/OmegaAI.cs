using System.Linq;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Ai;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/worlds/inggison/OmegaAI (@author Luzien, xTz).</summary>
[AIName("omega")]
public class OmegaAI : SummonerAI
{
    public OmegaAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleBeforeSpawn(Percentage percent)
    {
        AIActions.UseSkill(this, 19189);
        AIActions.UseSkill(this, 19191);
    }

    protected override bool CheckBeforeSpawn()
    {
        return GetKnownList().StreamPlayers().Any(player => IsInRange(player, 30));
    }
}
