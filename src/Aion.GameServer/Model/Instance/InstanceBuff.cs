using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Instance.Instancescore;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Instance_bonusatrr;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Change;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Model.Instance;

/// <summary>Java parity: model/instance/InstanceBuff. implements StatOwner→IStatOwner; Future→ScheduledTask.</summary>
public class InstanceBuff : IStatOwner
{
    private readonly List<IStatFunction> functions = new List<IStatFunction>();
    private readonly InstanceBonusAttr instanceBonusAttr;
    private ScheduledTask task;
    private long endTime;

    public InstanceBuff(int buffId)
    {
        instanceBonusAttr = DataManager.INSTANCE_BUFF_DATA.GetInstanceBonusattr(buffId);
    }

    public void ApplyEffect(Player player, int time)
    {
        if (IsActive() || instanceBonusAttr == null)
        {
            return;
        }
        if (time != 0)
        {
            InstanceBuffTask buffTask = new InstanceBuffTask(this, player);
            task = ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                buffTask.Run();
                return ValueTask.CompletedTask;
            }, TimeSpan.FromMilliseconds(time));
        }
        endTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + time;
        foreach (InstancePenaltyAttr instancePenaltyAttr in instanceBonusAttr.GetPenaltyAttr())
        {
            StatEnum stat = instancePenaltyAttr.GetStat();
            int statToModified = player.GetGameStats().GetStat(stat, 0).GetBase();
            int value = instancePenaltyAttr.GetValue();
            int valueModified = instancePenaltyAttr.GetFunc() == Func.PERCENT ? (statToModified * value / 100) : (value);
            functions.Add(new StatAddFunction(stat, valueModified, true));
        }
        player.GetGameStats().AddEffect(this, functions);
    }

    public void EndEffect(Player player)
    {
        functions.Clear();
        if (IsActive())
        {
            task.Cancel();
        }
        player.GetGameStats().EndEffect(this);
        Notify(player);
    }

    private void Notify(Player player)
    {
        WorldMapInstance wmi = player.GetWorldMapInstance();
        InstanceScore<InstancePlayerReward> score = wmi.GetInstanceHandler().GetInstanceScore();
        if (score is HarmonyArenaScore harmonyScore)
        {
            wmi.ForEachPlayer(p => PacketSendUtility.SendPacket(p, new SM_INSTANCE_SCORE(wmi.GetMapId(),
                new HarmonyScoreWriter(harmonyScore, InstanceScoreType.UPDATE_PLAYER_BUFF_STATUS, player), harmonyScore.GetTime())));
        }
        else if (score is PvPArenaScore arenaScore)
        {
            wmi.ForEachPlayer(
                p => PacketSendUtility.SendPacket(p, new SM_INSTANCE_SCORE(wmi.GetMapId(), new ArenaScoreWriter(arenaScore, p.GetObjectId(), false))));
        }
        PacketSendUtility.SendPacket(player, new SM_ABNORMAL_STATE(new List<Effect>(), player.GetEffectController().GetAbnormals(), 0));
    }

    public int GetRemainingTime()
    {
        return (int) Math.Max(0, endTime - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    private class InstanceBuffTask
    {
        private readonly InstanceBuff outer;
        private readonly Player player;

        public InstanceBuffTask(InstanceBuff outer, Player player)
        {
            this.outer = outer;
            this.player = player;
        }

        public void Run()
        {
            outer.EndEffect(player);
        }
    }

    public bool IsActive()
    {
        return task != null && !task.Completion.IsCompleted;
    }
}
