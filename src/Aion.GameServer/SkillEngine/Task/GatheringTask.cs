using System;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Task;

/// <summary>Java parity: skillengine/task/GatheringTask (ATracer, Yeats).</summary>
public class GatheringTask : AbstractCraftTask
{
    private readonly Aion.GameServer.Model.Templates.Gather.GatherableTemplate template;
    private readonly ActionObserver gathererObserver;
    private readonly Aion.GameServer.Model.Templates.Gather.Material material;
    private int showBarDelay;
    private int executionSpeed;

    public GatheringTask(Aion.GameServer.Model.GameObjects.Player.Player requester, Gatherable gatherable, Aion.GameServer.Model.Templates.Gather.Material material, int skillLvlDiff)
        : base(requester, gatherable, skillLvlDiff)
    {
        this.template = gatherable.GetObjectTemplate();
        this.gathererObserver = CreateGathererObserver();
        this.material = material;
        this.delay = Aion.Commons.Utils.Rnd.Get(200, 600);
        int gatherInterval = 2500 - (skillLvlDiff * 60);
        this.interval = gatherInterval < 1200 ? 1200 : gatherInterval;
    }

    protected override void OnInteractionAbort()
    {
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(requester, new Aion.GameServer.Network.Aion.ServerPackets.SmGatherAnimation(requester.GetObjectId(), responder.GetObjectId(), template.GetHarvestSkill(), 4));
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(requester, new Aion.GameServer.Network.Aion.ServerPackets.SmGatherUpdate(template, material, 0, 0, 5, 0, 0));
    }

    protected override void OnInteractionFinish()
    {
        requester.GetObserveController().RemoveObserver(gathererObserver);
        ((Gatherable)responder).GetController().CompleteInteraction();
    }

    protected override void OnInteractionStart()
    {
        requester.GetObserveController().Attach(gathererObserver);
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(requester, new Aion.GameServer.Network.Aion.ServerPackets.SmGatherUpdate(template, material, fullBarValue, fullBarValue, 0, 0, 0));
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(requester, new Aion.GameServer.Network.Aion.ServerPackets.SmGatherUpdate(template, material, 0, 0, 1, 0, 0));
        // TODO: missing packet for initial failure/success
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(requester, new Aion.GameServer.Network.Aion.ServerPackets.SmGatherAnimation(requester.GetObjectId(), responder.GetObjectId(), template.GetHarvestSkill(), 0), true);
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(requester, new Aion.GameServer.Network.Aion.ServerPackets.SmGatherAnimation(requester.GetObjectId(), responder.GetObjectId(), template.GetHarvestSkill(), 1), true);
    }

    protected override void SendInteractionUpdate()
    {
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(requester, new Aion.GameServer.Network.Aion.ServerPackets.SmGatherUpdate(template, material, currentSuccessValue, currentFailureValue, craftType.GetProgressId(), executionSpeed, showBarDelay));
    }

    protected override void OnFailureFinish()
    {
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(requester, new Aion.GameServer.Network.Aion.ServerPackets.SmGatherUpdate(template, material, currentSuccessValue, currentFailureValue, 1, 0, 0));
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(requester, new Aion.GameServer.Network.Aion.ServerPackets.SmGatherUpdate(template, material, currentSuccessValue, currentFailureValue, 7, 0, 0));
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(requester, new Aion.GameServer.Network.Aion.ServerPackets.SmGatherAnimation(requester.GetObjectId(), responder.GetObjectId(), template.GetHarvestSkill(), 3), true);
    }

    protected override bool OnSuccessFinish()
    {
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(requester, new Aion.GameServer.Network.Aion.ServerPackets.SmGatherAnimation(requester.GetObjectId(), responder.GetObjectId(), template.GetHarvestSkill(), 2), true);
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(requester, new Aion.GameServer.Network.Aion.ServerPackets.SmGatherUpdate(template, material, currentSuccessValue, currentFailureValue, 6, 0, 0));
        if (template.GetEraseValue() > 0)
            requester.GetInventory().DecreaseByItemId(template.GetRequiredItemId(), template.GetEraseValue());
        Aion.GameServer.Services.Item.ItemService.AddItem(requester, material.GetItemId(), Aion.GameServer.Model.GameObjects.Player.Rates.GATHERING_COUNT.CalcResult(requester, 1));
        requester.GetPosition().GetWorldMapInstance().GetInstanceHandler().OnGather(requester, (Gatherable)responder);
        ((Gatherable)responder).GetController().RewardPlayer(requester);
        return true;
    }

    protected override void AnalyzeInteraction()
    {
        if (skillLvlDiff >= 41)
        {
            currentSuccessValue = fullBarValue;
            executionSpeed = 300;
            showBarDelay = 500;
            return;
        }
        else if (skillLvlDiff < 0)
        {
            currentFailureValue = fullBarValue;
            return;
        }

        craftType = CraftType.NORMAL;
        float multi = Aion.Commons.Utils.Rnd.NextFloat(1f, 2f);
        float failReduction = Math.Max(1 - skillLvlDiff * 0.015f, 0.25f); // dynamic fail rate multiplier
        bool success = Aion.Commons.Utils.Rnd.Chance() >= CraftConfig.MAX_GATHER_FAILURE_CHANCE * failReduction;

        if (success)
        {
            float critChance = Aion.Commons.Utils.Rnd.Chance();
            if (critChance < (1 + skillLvlDiff / 10f)) // PURPLE CRIT = 100%
            {
                craftType = CraftType.CRIT_PURPLE;
                currentSuccessValue = fullBarValue;
                executionSpeed = 300;
                showBarDelay = 500;
                return;
            }
            else if (critChance < (5 + skillLvlDiff / 3f)) // LIGHT BLUE CRIT = +10%
            {
                craftType = CraftType.CRIT_BLUE;
            }

            int lvlBoni = skillLvlDiff > 10 ? ((skillLvlDiff - 10) * 2) : 0;
            currentSuccessValue += (int)Math.Floor((70 + ((craftType == CraftType.CRIT_BLUE ? 100 : 0) + (((skillLvlDiff + 1) / 2f) + lvlBoni) * 10) * multi) + 0.5f);
        }
        else
        {
            currentFailureValue += (int)Math.Floor((120 + (((skillLvlDiff + 1) / 2f * 10) * multi)) + 0.5f);
        }

        if (currentSuccessValue > fullBarValue)
        {
            currentSuccessValue = fullBarValue;
        }
        else if (currentFailureValue > fullBarValue)
        {
            currentFailureValue = fullBarValue;
        }

        int speed = 900 - (skillLvlDiff * 30);
        executionSpeed = speed < 300 ? 300 : speed;
        showBarDelay = Math.Max(500, 1200 - (skillLvlDiff * 30));
    }

    public int GetGathererId()
    {
        return requester.GetObjectId();
    }

    private ActionObserver CreateGathererObserver()
    {
        return new GathererObserver(this);
    }

    // Java parity: anonymous ActionObserver(ObserverType.ALL) in createGathererObserver().
    private sealed class GathererObserver : ActionObserver
    {
        private readonly GatheringTask task;

        public GathererObserver(GatheringTask task)
            : base(ObserverType.ALL)
        {
            this.task = task;
        }

        public override void StartSkillCast(Skill skill)
        {
            task.Abort();
        }

        public override void Attack(Creature creature, int skillId)
        {
            task.Abort();
        }

        public override void Attacked(Creature creature, int skillId)
        {
            task.Abort();
        }

        public override void Moved()
        {
            task.Abort();
        }

        public override void Dotattacked(Creature creature, Effect dotEffect)
        {
            task.Abort();
        }
    }
}
