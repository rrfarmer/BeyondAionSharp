using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Custom.Pvpmap;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Geo;
using TYPE = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.TYPE;
using LOG = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.LOG;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Yeats, Neon
/// </summary>
[AIName("customcdreset")]
public class SkillCooltimeResetAI : NpcAI
{
    private const int PRICE = 50_000;
    private const int MAX_SKILL_COOLDOWN_TIME = 3060; // = 5min 6sec
    private const int MAX_ITEM_COOLDOWN_SECONDS = 300; // excludes items like Fine Bracing Water, Leader's Recovery Scroll, Recovery Crystal etc.

    private readonly IDictionary<int, long> playersInSight = new ConcurrentDictionary<int, long>();

    public SkillCooltimeResetAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        if (PvpMapService.GetInstance().IsOnPvPMap(GetOwner()))
        {
            GetOwner().GetController().AddTask(TaskId.DESPAWN, ThreadPoolManager.GetInstance().Schedule(_ => { GetOwner().GetController().Delete(); return ValueTask.CompletedTask; }, 30000L));
            ThreadPoolManager.GetInstance().Schedule(_ => { GetOwner().GetKnownList().ForEachPlayer(TryNotify); return ValueTask.CompletedTask; }, 1000L);
        }
    }

    protected override void HandleDialogStart(Player player)
    {
        // remove players if they are already 5 mins+ in the map
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        foreach (int key in playersInSight.Where(e => now > e.Value + 300000).Select(e => e.Key).ToList())
            playersInSight.Remove(key);
        if (player.GetLifeStats().IsAboutToDie() || player.IsDead())
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANNOT_USE_IN_DEAD_STATE());
        else if (!PvpMapService.GetInstance().IsOnPvPMap(player) && player.GetController().IsInCombat())
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_CANT_CAST_IN_COMBAT_STATE());
        else if (player.IsTransformed() && player.GetTransformModel().GetType_() == TransformType.AVATAR)
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_CAN_NOT_ACT_WHILE_IN_ABNORMAL_STATE());
        else if (CollectResettableSkillCooldownIds(player).Count == 0 && CollectResettableItemCooldownIds(player).Count == 0)
            PacketSendUtility.SendPacket(player, new SM_MESSAGE(GetOwner(), "Daeva has no skill cooldowns to reset, yang.", ChatType.NPC));
        else
            SendRequest(player);
    }

    protected override void HandleCreatureMoved(Creature creature)
    {
        if (creature is Player player)
            TryNotify(player);
    }

    private void TryNotify(Player player)
    {
        if (player.IsDead())
            return;
        if (!GetOwner().CanSee(player))
            return;
        if (playersInSight.ContainsKey(player.GetObjectId()))
            return;
        if (PositionUtil.IsInRange(GetOwner(), player, 8) && GeoService.GetInstance().CanSee(GetOwner(), player))
        {
            playersInSight[player.GetObjectId()] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            PacketSendUtility.SendPacket(player,
                new SM_MESSAGE(GetOwner(), string.Format("I can heal you and reset your skill cooldowns for {0:N0} Kinah, yang yang.", PRICE), ChatType.NPC));
        }
    }

    private void SendRequest(Player player)
    {
        AIActions.AddRequest(this, player, 1300765, GetObjectTemplate().GetTalkDistance(), new SkillCooltimeResetRequest(this));
    }

    private sealed class SkillCooltimeResetRequest : AIRequest
    {
        private readonly SkillCooltimeResetAI ai;

        public SkillCooltimeResetRequest(SkillCooltimeResetAI ai)
        {
            this.ai = ai;
        }

        public override void AcceptRequest(Creature requester, Player responder, int requestId)
        {
            if (responder.GetInventory().GetKinah() < PRICE)
                PacketSendUtility.SendPacket(responder, SM_SYSTEM_MESSAGE.STR_MSG_NOT_ENOUGH_KINA(PRICE));
            else if (responder.GetLifeStats().IsAboutToDie() || responder.IsDead())
                PacketSendUtility.SendPacket(responder, SM_SYSTEM_MESSAGE.STR_CANNOT_USE_IN_DEAD_STATE());
            else if (responder.GetController().IsInCombat())
                PacketSendUtility.SendPacket(responder, SM_SYSTEM_MESSAGE.STR_SKILL_CANT_CAST_IN_COMBAT_STATE());
            else if (responder.IsTransformed() && responder.GetTransformModel().GetType_() == TransformType.AVATAR)
                PacketSendUtility.SendPacket(responder, SM_SYSTEM_MESSAGE.STR_SKILL_CAN_NOT_ACT_WHILE_IN_ABNORMAL_STATE());
            else
            {
                ISet<int> skillCooldownIds = ai.CollectResettableSkillCooldownIds(responder);
                ISet<int> itemCooldownIds = ai.CollectResettableItemCooldownIds(responder);
                if (skillCooldownIds.Count != 0 || itemCooldownIds.Count != 0)
                {
                    if (responder.GetInventory().TryDecreaseKinah(PRICE))
                    {
                        responder.GetLifeStats().IncreaseHp(TYPE.HP, responder.GetLifeStats().GetMaxHp(), ai.GetOwner());
                        responder.GetLifeStats().IncreaseMp(TYPE.HEAL_MP, responder.GetLifeStats().GetMaxMp(), 0, LOG.MPHEAL);
                        if (skillCooldownIds.Count != 0)
                        {
                            foreach (int id in skillCooldownIds)
                                responder.RemoveSkillCoolDown(id);
                            PacketSendUtility.SendPacket(responder, new SM_SKILL_COOLDOWN(responder, skillCooldownIds));
                        }
                        if (itemCooldownIds.Count != 0)
                        {
                            Dictionary<int, ItemCooldown> dummyCds = new Dictionary<int, ItemCooldown>(); // 4.8 client ignores reuseTime <= currentTime, but sending old cds + useDelay 0 works
                            foreach (int itemCooldownId in itemCooldownIds)
                            {
                                dummyCds[itemCooldownId] = new ItemCooldown(responder.GetItemReuseTime(itemCooldownId), 0);
                                responder.RemoveItemCoolDown(itemCooldownId);
                            }
                            PacketSendUtility.SendPacket(responder, new SM_ITEM_COOLDOWN(dummyCds));
                        }
                        if (PvpMapService.GetInstance().IsOnPvPMap(ai.GetOwner()))
                        {
                            ai.GetOwner().GetController().Delete();
                        }
                    }
                    else
                    {
                        PacketSendUtility.SendPacket(responder, SM_SYSTEM_MESSAGE.STR_MSG_NOT_ENOUGH_KINA(PRICE));
                    }
                }
            }
        }
    }

    private ISet<int> CollectResettableItemCooldownIds(Player player)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        ISet<int> itemCooldownIds = player.GetItemCoolDowns()
            .Where(e => e.Value.GetUseDelay() <= MAX_ITEM_COOLDOWN_SECONDS && e.Value.GetReuseTime() > now)
            .Select(e => e.Key)
            .ToHashSet();
        if (itemCooldownIds.Count != 0)
            itemCooldownIds.IntersectWith(CollectBuffItemAndPotionCooldownIds(player));
        return itemCooldownIds;
    }

    private ISet<int> CollectBuffItemAndPotionCooldownIds(Player player)
    {
        ISet<int> cooldownIds = new HashSet<int>();
        foreach (Item item in player.GetInventory().GetItems())
        {
            ItemTemplate itemTemplate = item.GetItemTemplate();
            ItemUseLimits useLimits = itemTemplate.GetUseLimits();
            if (useLimits == null || useLimits.GetDelayId() == 0)
                continue;
            if (itemTemplate.GetActions() == null || itemTemplate.GetActions().GetSkillUseAction() == null)
                continue;
            SkillTemplate skillTemplate = DataManager.SKILL_DATA.GetSkillTemplate(itemTemplate.GetActions().GetSkillUseAction().GetSkillId());
            if (skillTemplate == null || skillTemplate.GetTargetSlot() != SkillTargetSlot.BUFF && !skillTemplate.GetStack().StartsWith("ITEM_POTION_")
                && !skillTemplate.GetStack().StartsWith("ITEM_ARENA_POTION_"))
                continue;
            cooldownIds.Add(useLimits.GetDelayId());
        }
        return cooldownIds;
    }

    private ISet<int> CollectResettableSkillCooldownIds(Player player)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        ISet<int> cooldownIds = new HashSet<int>();
        foreach (PlayerSkillEntry skill in player.GetSkillList().GetAllSkills())
        {
            SkillTemplate st = DataManager.SKILL_DATA.GetSkillTemplate(skill.GetSkillId());
            if (st == null || st.GetCooldown() > MAX_SKILL_COOLDOWN_TIME)
                continue;
            if (st.IsDeityAvatar())
                continue;
            if (player.GetSkillCoolDown(st.GetCooldownId()) < now)
                continue;
            cooldownIds.Add(st.GetCooldownId());
        }
        return cooldownIds;
    }
}
