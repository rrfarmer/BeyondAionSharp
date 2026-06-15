using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/aturamSkyFortress/ExplosionShadowsAI (@author xTz).</summary>
[AIName("explosion_shadows")]
public class ExplosionShadowsAI : AggressiveNpcAI
{
    public ExplosionShadowsAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        GetPosition().GetWorldMapInstance().SetDoorState(2, false); // this actually opens it on client side (wtf)
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        GetPosition().GetWorldMapInstance().SetDoorState(17, false); // this actually opens it on client side (wtf)
        GetPosition().GetWorldMapInstance().SetDoorState(2, false); // this actually opens it on client side (wtf)
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        base.OnEndUseSkill(skillTemplate, skillLevel);
        if (skillTemplate.GetSkillId() == 19425) // Self Destruct
            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                Check();
                return ValueTask.CompletedTask;
            }, 1500L);
    }

    private void Check()
    {
        if (!IsDead())
        {
            GetKnownList().ForEachPlayer(player =>
            {
                if (player.GetEffectController().HasAbnormalEffect(19502))
                {
                    Npc npc = (Npc)Spawn(799657, player.GetX(), player.GetY(), player.GetZ(), (sbyte)player.GetHeading());
                    PacketSendUtility.SendPacket(player, new SM_EMOTION(npc, EmotionType.DIE));
                    player.GetEffectController().RemoveEffect(19502);
                }
            });
            AIActions.DeleteOwner(this);
        }
    }
}
