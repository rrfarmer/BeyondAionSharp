using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Npcshout;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services;

/// <summary>This class is handling NPC shouts. Java parity: services/NpcShoutsService (Rolandas, Neon).</summary>
public class NpcShoutsService
{
    private readonly ConcurrentDictionary<int, long> shoutCooldowns;

    private NpcShoutsService()
    {
        shoutCooldowns = new ConcurrentDictionary<int, long>();
    }

    public void RegisterShoutTask(Npc npc)
    {
        int worldId = npc.GetSpawn().GetWorldId();

        List<NpcShout> shouts = DataManager.NPC_SHOUT_DATA.GetNpcShouts(worldId, npc.GetNpcId(), ShoutEventType.IDLE);
        if (shouts == null || shouts.Count == 0)
            return;

        int pollDelay = Aion.Commons.Utils.Rnd.Get(180, 360) * 1000;
        foreach (NpcShout shout in shouts)
        {
            if (shout.GetPollDelay() != 0 && shout.GetPollDelay() < pollDelay)
                pollDelay = shout.GetPollDelay();
        }

        NpcShoutTask task = new NpcShoutTask(this, npc, shouts);
        npc.GetController().AddTask(TaskId.SHOUT, ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct =>
        {
            task.Run();
            return ValueTask.CompletedTask;
        }, TimeSpan.Zero, TimeSpan.FromMilliseconds(pollDelay)));
    }

    public void RemoveShoutCooldown(Npc npc)
    {
        shoutCooldowns.TryRemove(npc.GetObjectId(), out _);
    }

    public bool MayShout(Npc npc)
    {
        if (!shoutCooldowns.TryGetValue(npc.GetObjectId(), out long cd))
            return true;
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() >= cd;
    }

    public void ShoutRandom(Npc sender, Aion.GameServer.Model.GameObjects.Player.Player target, List<NpcShout> shouts, int shoutCooldown)
    {
        if (shouts == null || shouts.Count == 0)
            return;
        Shout(sender, target, Aion.Commons.Utils.Rnd.Get(shouts), shoutCooldown);
    }

    public void Shout(Npc sender, Aion.GameServer.Model.GameObjects.Player.Player target, NpcShout shout, int shoutCooldown)
    {
        if (sender == null || shout == null)
            return;

        if (shout.GetPattern() != null && !sender.GetAi().OnPatternShout(shout.GetWhen(), shout.GetPattern(), shout.GetSkillNo()))
            return;

        int shoutRange = sender.GetObjectTemplate().GetMinimumShoutRange();
        if (target != null && !PositionUtil.IsInRange(target, sender, shoutRange))
            return;

        string param = shout.GetParam();
        if (sender.GetTarget() != null && "target".Equals(param))
            param = sender.GetTarget().GetObjectTemplate().GetName();

        if (shoutCooldown > 0 && target != null && "quest".Equals(shout.GetPattern()))
            shoutCooldown = 0;

        Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage message = new Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage(ChatType.NPC, sender, shout.GetStringId(), param);

        if (target != null)
        {
            PacketSendUtility.SendPacket(target, message);
        }
        else
        {
            PacketSendUtility.BroadcastPacket(sender, message, player => PositionUtil.IsInRange(player, sender, shoutRange));
        }
        if (shoutCooldown <= 0)
            RemoveShoutCooldown(sender);
        else
            shoutCooldowns[sender.GetObjectId()] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + shoutCooldown * 1000 - 50; // 50ms offset to avoid tight cooldown conflicts
    }

    public static NpcShoutsService GetInstance()
    {
        return SingletonHolder.instance;
    }

    private static class SingletonHolder
    {
        internal static readonly NpcShoutsService instance = new NpcShoutsService();
    }

    private class NpcShoutTask
    {
        private readonly NpcShoutsService service;
        private Npc npc;
        private List<NpcShout> shouts;

        internal NpcShoutTask(NpcShoutsService service, Npc npc, List<NpcShout> shouts)
        {
            this.service = service;
            this.npc = npc;
            this.shouts = shouts;
        }

        public void Run()
        {
            if (npc.GetPosition().IsMapRegionActive() && npc.GetAi().Ask(AIQuestion.CAN_SHOUT))
                service.ShoutRandom(npc, null, shouts, 0);
        }
    }
}
