using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/theShugoEmperorsVault/ShugoMorpher (@author Yeats).</summary>
[AIName("emperorsVaultMorphNPC")]
public class ShugoMorpher : GeneralNpcAI
{
    private readonly AtomicBoolean started = new AtomicBoolean(false);

    public ShugoMorpher(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        // do nothing
    }

    protected override void HandleDialogStart(Player player)
    {
        if (DialogService.IsInteractionAllowed(player, GetOwner()) && started.CompareAndSet(false, true))
        {
            const int delay = 1000;
            ItemUseObserver obs = null;
            obs = new MorpherItemUseObserver(this, player, () => obs);

            player.GetObserveController().Attach(obs);
            PacketSendUtility.SendPacket(player, new SM_USE_OBJECT(player.GetObjectId(), GetObjectId(), delay, 1));
            PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(player, EmotionType.START_QUESTLOOT, 0, GetObjectId()), true);
            player.GetController().AddTask(TaskId.ACTION_ITEM_NPC, ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(player, EmotionType.END_QUESTLOOT, 0, GetObjectId()), true);
                PacketSendUtility.SendPacket(player, new SM_USE_OBJECT(player.GetObjectId(), GetObjectId(), delay, 2));
                player.GetObserveController().RemoveObserver(obs);
                HandleUseItemFinish(player);
                return System.Threading.Tasks.ValueTask.CompletedTask;
            }, (long)delay));
        }
    }

    private class MorpherItemUseObserver : ItemUseObserver
    {
        private readonly ShugoMorpher _ai;
        private readonly Player _player;
        private readonly System.Func<ItemUseObserver> _self;

        public MorpherItemUseObserver(ShugoMorpher ai, Player player, System.Func<ItemUseObserver> self)
        {
            _ai = ai;
            _player = player;
            _self = self;
        }

        public override void Abort()
        {
            _ai.started.Set(false);
            _player.GetObserveController().RemoveObserver(_self());
            _player.GetController().CancelTask(TaskId.ACTION_ITEM_NPC);
            PacketSendUtility.BroadcastPacket(_player, new SM_EMOTION(_player, EmotionType.END_QUESTLOOT, 0, _ai.GetObjectId()), true);
            PacketSendUtility.SendPacket(_player, new SM_USE_OBJECT(_player.GetObjectId(), _ai.GetObjectId(), 0, 2));
        }
    }

    private void HandleUseItemFinish(Player player)
    {
        Race race = player.GetRace();
        bool morphed = GetOwner().GetNpcId() switch
        {
            833491 or 833494 or 832935 => SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), race == Race.ELYOS ? 21829 : 21832, 1, player).UseSkill(),
            833492 or 833495 or 832936 => SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), race == Race.ELYOS ? 21830 : 21833, 1, player).UseSkill(),
            833493 or 833496 or 832937 => SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), race == Race.ELYOS ? 21831 : 21834, 1, player).UseSkill(),
            _ => false,
        };

        if (morphed)
        {
            GetOwner().GetController().Delete();
        }
        else
        {
            started.Set(false);
        }
    }

    public override float ModifyDamage(Creature attacker, float damage, Effect effect)
    {
        return 0;
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.REWARD_AP_XP_DP_LOOT or AIQuestion.REWARD_LOOT => false,
            _ => base.Ask(question),
        };
    }
}
