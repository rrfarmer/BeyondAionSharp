using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/quests/NidalberBalaurAI (@author Yeats, Pad).</summary>
[AIName("nidalber_balaur")]
public class NidalberBalaurAI : AggressiveNpcAI
{
    private Npc questNpc = null;

    public NidalberBalaurAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        FindQuestNpc();
    }

    private void SetNewSpawnPosition()
    {
        GetSpawnTemplate().SetX(271.153f);
        GetSpawnTemplate().SetY(178.903f);
        GetSpawnTemplate().SetZ(204.52f);
    }

    private void FindQuestNpc()
    {
        int mapId = GetOwner().GetPosition().GetMapId();
        if (mapId != 310040000 && mapId != 320040000)
            return;
        questNpc = GetOwner().GetPosition().GetWorldMapInstance().GetNpc(mapId == 310040000 ? 204044 : 204432);
        if (questNpc != null && !questNpc.IsDead())
        {
            SetNewSpawnPosition();
            MoveToQuestNpc();
        }
        else
        {
            GetOwner().GetController().Delete();
        }
    }

    private void MoveToQuestNpc()
    {
        SetStateIfNot(AIState.WALKING);
        GetOwner().SetState(CreatureState.ACTIVE, true);
        GetOwner().GetMoveController().MoveToPoint(GetSpawnTemplate().GetX(), GetSpawnTemplate().GetY(), GetSpawnTemplate().GetZ());
    }

    protected override void HandleNotAtHome()
    {
        if (GetOwner().GetPosition().GetMapId() != 310040000 && GetOwner().GetPosition().GetMapId() != 320040000)
            base.HandleNotAtHome();
        else
            MoveToQuestNpc();
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        AddHate();
    }

    protected override void HandleMoveArrived()
    {
        base.HandleMoveArrived();
        if (GetOwner().IsAtSpawnLocation())
            AddHate();
    }

    private void AddHate()
    {
        if (GetOwner().GetPosition().GetMapId() != 310040000 && GetOwner().GetPosition().GetMapId() != 320040000)
            return;
        if (questNpc == null || questNpc.IsDead())
        {
            GetOwner().GetController().Delete();
            return;
        }

        if (!GetAggroList().IsHating(questNpc))
            GetAggroList().AddHate(questNpc, 100);
    }
}
