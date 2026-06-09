using Aion.GameServer.Dataholders;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Flyring;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Controllers.Observer;

/// <summary>Java parity: controllers/observer/FlyRingObserver (xavier, Source).</summary>
public class FlyRingObserver : ActionObserver
{
    private readonly Player player;
    private readonly FlyRing ring;
    private Vector3f oldPosition;

    public FlyRingObserver(FlyRing ring, Player player)
        : base(ObserverType.MOVE)
    {
        this.player = player;
        this.ring = ring;
        this.oldPosition = new Vector3f(player.GetX(), player.GetY(), player.GetZ());
    }

    public override void Moved()
    {
        Vector3f newPosition = new Vector3f(player.GetX(), player.GetY(), player.GetZ());
        if (ring.IsCrossed(oldPosition, newPosition))
        {
            if (ring.GetTemplate().GetMap() == 400010000 || IsQuestactive() || IsInstancetactive())
            {
                SkillTemplate skillTemplate = DataManager.SKILL_DATA.GetSkillTemplate(265); // Wings of Aether
                Effect speedUp = new Effect(player, player, skillTemplate, skillTemplate.GetLvl());
                speedUp.Initialize();
                speedUp.AddAllEffectToSucess();
                speedUp.ApplyEffect();
            }
            QuestEngine.GetInstance().OnPassFlyingRing(new QuestEnv(null, player, 0), ring.Name);
        }
        oldPosition = newPosition;
    }

    private bool IsInstancetactive()
    {
        return ring.GetPosition().GetWorldMapInstance().GetInstanceHandler().OnPassFlyingRing(player, ring.Name);
    }

    private bool IsQuestactive()
    {
        int questId = player.GetRace() == Race.ASMODIANS ? 2042 : 1044;
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs == null)
            return false;

        return qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) >= 2 && qs.GetQuestVarById(0) <= 8;
    }
}
