using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Ai;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Services.Panesterra;
using Aion.GameServer.Services.Panesterra.Ahserion;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Yeats, Estrayl
/// </summary>
[AIName("ahserion")]
public class AhserionAI : AggressiveNpcAI
{
    private static readonly ILogger log = NullLogger.Instance;

    public AhserionAI(Npc owner) : base(owner)
    {
    }

    public override void ModifyOwnerStat(Stat2 stat)
    {
        if (stat.GetStat() == StatEnum.MAXHP)
            stat.SetBaseRate(SiegeConfig.AHSERION_MAX_PLAYERS_PER_TEAM / 100f);
    }

    public override float ModifyDamage(Creature attacker, float damage, Effect effect)
    {
        if (effect != null && effect.GetSkillId() == 21583) // Artillery Blast
            return damage * (SiegeConfig.AHSERION_MAX_PLAYERS_PER_TEAM / 100f);
        return base.ModifyDamage(attacker, damage, effect);
    }

    public override void OnStartUseSkill(SkillTemplate st, int lv)
    {
        switch (st.GetSkillId())
        {
            case 21566: // Thunder Crash
                if (lv == 55 && System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - GetOwner().GetGameStats().GetFightStartingTime() < 30000)
                    PacketSendUtility.BroadcastMessage(GetOwner(), 1501157); // _01: Beritra thinks me unready. I shall prove myself in your slaughter!
                break;
            case 21570: // Death Shriek
                if (lv == 58)
                    PacketSendUtility.BroadcastMessage(GetOwner(), 1501163); // _07: Not enough time… My Lords, come save me!
                break;
            case 21571: // Ereshkigal's Reign
            {
                WorldPosition p = GetPosition();
                Spawn(297186, p.GetX(), p.GetY(), p.GetZ() + 10, (sbyte)0); // Ereshkigal's Voice
                break;
            }
            case 21573: // Distortion Pulse
                if (lv == 58)
                    PacketSendUtility.BroadcastMessage(GetOwner(), 1501162); // _06: Impudent pestilence! Be gone!
                break;
            // Ide Destruction
            case 21574:
                PacketSendUtility.BroadcastMessage(GetOwner(), 1501164); // _08: I will return you to your beloved Aion's dust!
                break;
        }
    }

    public override void OnEndUseSkill(SkillTemplate st, int lv)
    {
        switch (st.GetSkillId())
        {
            case 21564: // Dragon Claw
                if (lv == 58)
                    HandleBaseAssault();
                break;
            case 21567: // Toxic Spew
                if (lv == 58)
                    HandleSupportSpawns();
                break;
            case 21574: // Ide Destruction
                HandleIdeDestruction();
                break;
        }

        // Custom solution to resolve the retail add hate event (switch_target_by_attacker_indicator)
        if (lv == 57 || lv == 26)
        {
            GetAggroList().AddHate(GetAggroList().GetTarget(AggroTarget.RANDOM), 100000);
        }
    }

    public override void OnEffectApplied(Effect effect)
    {
        if (effect.GetSkillId() == 21571) // Ereshkigal's Reign
            PacketSendUtility.BroadcastMessage(GetOwner(), 1501159); // _03: My head… Nooooo! Get out of my head!
    }

    public override void OnEffectEnd(Effect effect)
    {
        if (effect.GetSkillId() == 21571) // Ereshkigal's Reign
            PacketSendUtility.BroadcastMessage(GetOwner(), 1501161); // _03: Ereshkigal…? No! Nooooooo-argh!
    }

    private void HandleSupportSpawns()
    {
        Spawn(297353, 475.530f, 536.809f, 675.989f, (sbyte)75);
        Spawn(297353, 532.324f, 545.949f, 675.746f, (sbyte)75);
        Spawn(297353, 541.742f, 491.245f, 675.462f, (sbyte)75);
        Spawn(297353, 487.297f, 479.381f, 678.300f, (sbyte)75);
    }

    private void HandleBaseAssault()
    {
        if (GetOwner().GetWorldId() == 400030000 && AhserionRaid.GetInstance().IsStarted())
        {
            foreach (PanesterraFaction faction in System.Enum.GetValues<PanesterraFaction>())
            {
                PanesterraTeam team = PanesterraService.GetInstance().GetTeam(faction);
                if (team != null && !team.IsEliminated())
                    AhserionRaid.GetInstance().SpawnStage(5, faction);
            }
        }
    }

    /// <summary>
    /// Retail: activate_skillarea 21575
    /// </summary>
    private void HandleIdeDestruction()
    {
        GetKnownList().StreamPlayers().Where(p => !p.IsDead() && PositionUtil.IsInRange(p, 509.64f, 513.25f, 675.145f, 45))
            .ToList().ForEach(p => SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 21575, 1, p).UseWithoutPropSkill());
    }

    protected override void HandleDied()
    {
        if (GetOwner().GetWorldId() == 400030000 && AhserionRaid.GetInstance().IsStarted())
        {
            Dictionary<PanesterraFaction, int> panesterraDamage = new Dictionary<PanesterraFaction, int>();

            // Only players can attack Ahserion on this map.
            foreach (DamageInfo<Creature> damageInfo in GetAggroList().GetFinalDamageList().GetCreatureDamages())
            {
                if (damageInfo.GetAttacker() is Player player)
                {
                    PanesterraTeam team = PanesterraService.GetInstance().GetTeam(player);
                    if (team != null && !team.IsEliminated())
                    {
                        PanesterraFaction faction = team.GetFaction();
                        if (panesterraDamage.TryGetValue(faction, out int existing))
                            panesterraDamage[faction] = existing + damageInfo.GetDamage();
                        else
                            panesterraDamage[faction] = damageInfo.GetDamage();
                    }
                }
            }
            PanesterraFaction? winner = FindWinnerTeam(panesterraDamage);
            if (winner != null)
                AhserionRaid.GetInstance().HandleBossKilled(GetOwner(), winner.Value);
        }
        LogMetrics();
        base.HandleDied();
    }

    private PanesterraFaction? FindWinnerTeam(Dictionary<PanesterraFaction, int> panesterraDamage)
    {
        PanesterraFaction? winner = null;
        int maxDmg = 0;
        foreach (PanesterraFaction faction in System.Enum.GetValues<PanesterraFaction>())
        {
            if (panesterraDamage.TryGetValue(faction, out int dmg) && !PanesterraService.GetInstance().GetTeam(faction).IsEliminated())
            {
                if (dmg > maxDmg)
                {
                    maxDmg = dmg;
                    winner = faction;
                }
            }
        }
        return winner;
    }

    private void LogMetrics()
    {
        long fullFightTime = (System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - GetOwner().GetGameStats().GetFightStartingTime()) / 1000;
        string damageDealt = string.Join(", ", GetAggroList().GetFinalDamageList().GetCreatureDamages()
            .OrderByDescending(di => di.GetDamage())
            .Select(ai => string.Format("{0} (ID: {1}, Dmg: {2})", ai.GetAttacker().GetName(), ai.GetAttacker().GetObjectId(), ai.GetDamage())));

        log.LogInformation("[{Map}] {Name} (ID:{Id}) was killed in {Time}s. Damage List: {Damage}", GetPosition().GetWorldMapInstance().GetTemplate().GetName(), GetOwner().GetName(),
            GetNpcId(), fullFightTime, damageDealt);
    }
}
