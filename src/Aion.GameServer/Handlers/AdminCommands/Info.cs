using System;
using System.Threading;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.Players.Npcfaction;
using Aion.GameServer.Model.GameObjects.Siege;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Restrictions;
using Aion.GameServer.Services;
using Aion.GameServer.SpawnEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Info (Nemiroff, Neon). Shows information about your target.</summary>
public class Info : AdminCommand
{
    public Info()
        : base("info", "Shows information about your target.")
    {
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        VisibleObject target = admin.GetTarget();

        if (target == null)
        {
            PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_INVALID_TARGET());
            return;
        }

        SendInfo(admin, "[Info about " + target.GetType().Name + "]\n\tName: " + target.GetName() + ", ObjectId: " + target.GetObjectId()
            + "\n\tTemplateId: " + target.GetObjectTemplate().GetTemplateId());

        if (target is Creature creature)
        {
            if (creature is Player player)
            {
                Aion.GameServer.Model.GameObjects.Pet pet = player.GetPet();
                SendInfo(admin, (pet != null ? "Pet Id: " + pet.GetObjectTemplate().GetTemplateId() + ", ObjectId: " + pet.GetObjectId() + "\n\t" : "")
                    + "Town ID: " + TownService.GetInstance().GetTownResidence(player));
                SendInfo(admin, "Current Panesterra Faction: " + player.GetPanesterraFaction());
                PlayerGameStats pgs = player.GetGameStats();
                SendInfo(admin,
                    "[Stats]"
                            + "\n\tHP: " + player.GetLifeStats().GetCurrentHp() + "/" + pgs.GetMaxHp().GetCurrent()
                            + ", MP: " + player.GetLifeStats().GetCurrentMp() + "/" + pgs.GetMaxMp().GetCurrent()
                            + ", FP: " + player.GetLifeStats().GetCurrentFp() + "/" + pgs.GetFlyTime().GetCurrent()
                            + ", DP: " + player.GetCommonData().GetDp() + "/" + pgs.GetMaxDp().GetCurrent()
                            + "\n\tPower: " + pgs.GetPower().GetCurrent()
                            + ", Health: " + pgs.GetHealth().GetCurrent()
                            + ", Agility: " + pgs.GetAgility().GetCurrent()
                            + ", Accuracy: " + pgs.GetAccuracy().GetCurrent()
                            + ", Knowledge: " + pgs.GetKnowledge().GetCurrent()
                            + ", Will: " + pgs.GetWill().GetCurrent()
                            + "\n\tCast Time Boost: " + (pgs.GetStat(StatEnum.BOOST_CASTING_TIME, 1000).GetCurrent() * 0.1f - 100) + "%"
                            + "\n\tBase Attack Speed " + pgs.GetAttackSpeed().GetBase() * 0.001f
                            + "\n\tCurrent Attack Speed: " + pgs.GetAttackSpeed().GetCurrent() * 0.001f
                            + "\n\tMovement Speed: " + pgs.GetMovementSpeedFloat()
                            + "\n\t-------------Offence-------------"
                            + "\n\tMagic Boost: " + pgs.GetMBoost().GetCurrent()
                            + "\n\tM. Accuracy: " + pgs.GetMAccuracy().GetCurrent()
                            + "\n\tM. Critical: " + pgs.GetMCritical().GetCurrent()
                            + "\n\t\t---------Main Hand-----------"
                            + "\n\t\tM. Attack: " + (pgs.GetMainHandMAttack(CalculationType.DISPLAY).GetCurrent())
                            + "\n\t\tP. Attack: " + pgs.GetMainHandPAttack(CalculationType.DISPLAY).GetCurrent()
                            + "\n\t\tP. Accuracy: " + pgs.GetMainHandPAccuracy().GetCurrent()
                            + "\n\t\tP. Critical: " + pgs.GetMainHandPCritical().GetCurrent()
                            + "\n\t\t-----------Off Hand-----------"
                            + "\n\t\tM. Attack displayed: " + (pgs.GetOffHandMAttack(CalculationType.DISPLAY).GetCurrent())
                            + ", min: " + (int)(pgs.GetOffHandMAttack().GetCurrent() * pgs.GetMinDamageRatio())
                            + ", max: " + pgs.GetOffHandMAttack().GetCurrent()
                            + "\n\t\tP. Attack displayed: " + (pgs.GetOffHandPAttack(CalculationType.DISPLAY).GetCurrent())
                            + ", min: " + (int)(pgs.GetOffHandPAttack().GetCurrent() * pgs.GetMinDamageRatio())
                            + ", max: " + pgs.GetOffHandPAttack().GetCurrent()
                            + "\n\t\tP. Accuracy: " + pgs.GetOffHandPAccuracy().GetCurrent()
                            + "\n\t\tP. Critical: " + pgs.GetOffHandPCritical().GetCurrent()
                            + "\n\t-------------Defence--------------"
                            + "\n\t\tM. Defence: " + pgs.GetMDef().GetCurrent()
                            + "\n\t\tMagic Resist: " + pgs.GetMResist().GetCurrent()
                            + "\n\t\tCrit. Spell Resist: " + pgs.GetMCR()
                            + "\n\t\tCrit. Spell Fortitude: " + pgs.GetStat(StatEnum.MAGICAL_CRITICAL_DAMAGE_REDUCE, 0).GetCurrent()
                            + "\n\t\tP. Defence: " + pgs.GetPDef().GetCurrent()
                            + "\n\t\tBlock: " + pgs.GetBlock().GetCurrent()
                            + "\n\t\tParry: " + pgs.GetParry().GetCurrent()
                            + "\n\t\tEvasion: " + pgs.GetEvasion().GetCurrent()
                            + "\n\t\tCrit. Strike Resist: " + pgs.GetPCR().GetCurrent()
                            + "\n\t\tCrit. Strike Fortitude: " + pgs.GetStat(StatEnum.PHYSICAL_CRITICAL_DAMAGE_REDUCE, 0).GetCurrent()
                            + "\n\t\tWind Defense: " + pgs.GetElementalDefenseFor(SkillElement.WIND)
                            + "\n\t\tWater Defense: " + pgs.GetElementalDefenseFor(SkillElement.WATER)
                            + "\n\t\tEarth Defense: " + pgs.GetElementalDefenseFor(SkillElement.EARTH)
                            + "\n\t\tFire Defense: " + pgs.GetElementalDefenseFor(SkillElement.FIRE)
                            + "\n\t\tDark Defense: " + pgs.GetElementalDefenseFor(SkillElement.DARK)
                            + "\n\t\tLight Defense: " + pgs.GetElementalDefenseFor(SkillElement.LIGHT)
                            + "\n\t-------------PvP Stats-------------"
                            + "\n\tPvP attack: " + pgs.GetStat(StatEnum.PVP_ATTACK_RATIO, 0).GetCurrent() * 0.1f + "%"
                            + "\n\tPvP p. attack: " + pgs.GetStat(StatEnum.PVP_ATTACK_RATIO_PHYSICAL, 0).GetCurrent() * 0.1f + "%"
                            + "\n\tPvP m. attack: " + pgs.GetStat(StatEnum.PVP_ATTACK_RATIO_MAGICAL, 0).GetCurrent() * 0.1f + "%"
                            + "\n\tPvP defend: " + pgs.GetStat(StatEnum.PVP_DEFEND_RATIO, 0).GetCurrent() * 0.1f + "%"
                            + "\n\tPvP p. defend: " + pgs.GetStat(StatEnum.PVP_DEFEND_RATIO_PHYSICAL, 0).GetCurrent() * 0.1f + "%"
                            + "\n\tPvP m. defend: " + pgs.GetStat(StatEnum.PVP_DEFEND_RATIO_MAGICAL, 0).GetCurrent() * 0.1f + "%");

                for (int i = 0; i < 2; i++)
                {
                    NpcFaction faction = player.GetNpcFactions().GetActiveNpcFaction(i == 0);
                    if (faction != null)
                    {
                        SendInfo(admin,
                            player.GetName() + " have join to " + (i == 0 ? "mentor" : "daily") + " faction: " + DataManager.NPC_FACTIONS_DATA.GetNpcFactionById(faction.GetId()).GetName()
                                    + "\n\tCurrent quest state: " + faction.GetState().ToString()
                                    + (faction.GetState().Equals(ENpcFactionQuestState.COMPLETE) ? ("\n\tNext after: " + ((faction.GetTime() - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000) / 3600f) + " h.") : ""));
                    }
                }
            }
            else if (creature is Npc npc)
            {
                SendInfo(admin, "[Template info]\n\tRating: " + npc.GetRating() + ", Rank: " + npc.GetRank()
                        + "\n\tTemplateType: " + npc.GetNpcTemplateType() + ", AbyssType: " + npc.GetAbyssNpcType()
                        + "\n\tRelative XP reward: " + StatFunctions.CalculateExperienceReward(admin.GetLevel(), npc));
                if (npc is SiegeNpc)
                    SendInfo(admin, "[Siege info]\n\tSiegeId: " + ((SiegeNpc)npc).GetSiegeId() + ", SiegeRace: " + ((SiegeNpc)npc).GetSiegeRace());
                SendInfo(admin,
                    "[AI info]\n\tAI: " + npc.GetAi().GetName()
                            + "\n\tState: " + npc.GetAi().GetState() + ", SubState: " + npc.GetAi().GetSubState());
                SendInfo(admin,
                    "[Sense range]\n\tRadius: " + npc.GetAggroRange()
                            + "\n\tShort-Radius: " + npc.GetShortAggroRange()
                            + "\n\tAngle: " + npc.GetAggroAngle()
                            + "\n\tSide: " + npc.GetObjectTemplate().GetBoundRadius().GetSide() + ", Front: " + npc.GetObjectTemplate().GetBoundRadius().GetFront() + ", Upper: " + npc.GetObjectTemplate().GetBoundRadius().GetUpper()
                            + "\n\tDirectional bound: " + PositionUtil.GetDirectionalBound(npc, admin, true)
                            + "\n\tDistance: " + (npc.GetAggroRange() + PositionUtil.GetDirectionalBound(npc, admin, true)));
                SendInfo(admin, "[Spawn info]\n\tStaticId: " + npc.GetSpawn().GetStaticId() + ", DistToSpawn: " + npc.GetDistanceToSpawnLocation() + "m");
                if (npc.IsPathWalker())
                {
                    SendInfo(admin, "\tRouteId: " + npc.GetSpawn().GetWalkerId());
                    if (npc.GetWalkerGroup() != null)
                    {
                        ClusteredNpc snpc = npc.GetWalkerGroup().GetClusterData(npc);
                        SendInfo(admin, "\tWalkerGroupType: " + npc.GetWalkerGroup().GetWalkType() + ", XDelta: " + snpc.GetXDelta() + ", YDelta: "
                            + snpc.GetYDelta() + ", Index: " + snpc.GetWalkerIndex());
                    }
                }
                else if (npc.IsRandomWalker())
                {
                    SendInfo(admin, "\tRandomWalkRange: " + npc.GetSpawn().GetRandomWalkRange() + "m");
                }
            }
            SendInfo(admin, CreateZoneInfo(creature));
            SendInfo(admin, "[Tribe]\n\tRace: " + creature.GetRace() + ", Tribe: " + creature.GetTribe() + ", TribeBase: " + creature.GetBaseTribe());
            SendInfo(admin, "[Your relation]\n\tisEnemy: " + admin.IsEnemy(creature) + ", canAttack: " + PlayerRestrictions.CanAttack(admin, target));
            SendInfo(admin, "[Targets relation]\n\tisEnemy: " + creature.IsEnemy(admin)
                + (creature is Npc ? ", Hostility: " + ((Npc)creature).GetType_(admin) : ""));
            SendInfo(admin, "[Life stats]\n\tHP: " + creature.GetLifeStats().GetCurrentHp() + " / " + creature.GetLifeStats().GetMaxHp()
                    + "\n\tMP: " + creature.GetLifeStats().GetCurrentMp() + " / " + creature.GetLifeStats().GetMaxMp());
            SendInfo(admin, CreateAggroInfo(creature));
        }
        else if (target.GetSpawn() != null && target.GetSpawn().GetStaticId() != 0)
        {
            SendInfo(admin, "\tStaticId: " + target.GetSpawn().GetStaticId());
        }
    }

    private string CreateZoneInfo(Creature creature)
    {
        FortressLocation fortress = SiegeService.GetInstance().FindFortress(creature.GetWorldId(), creature.GetX(), creature.GetY(), creature.GetZ());
        int townId = TownService.GetInstance().GetTownIdByPosition(creature);
        System.Text.StringBuilder sb = new System.Text.StringBuilder("[Current zone]");
        sb.Append("\n\t" + creature.GetPosition().ToCoordString());
        sb.Append("\n\tFortress Location ID: " + (fortress == null ? "-" : fortress.GetLocationId().ToString()));
        sb.Append("\n\tTown ID: " + (townId == 0 ? "-" : townId.ToString()));
        sb.Append("\n\tPvP: " + creature.IsInsidePvPZone());
        return sb.ToString();
    }

    private string CreateAggroInfo(Creature creature)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder("[AggroList]");
        int aDmg = 0, eDmg = 0, tDmg = 0;
        foreach (AggroInfo ai in creature.GetAggroList().Stream())
        {
            string name = ai.GetAttacker().GetName();
            Creature master = ai.GetAttacker().GetMaster();
            Interlocked.Add(ref tDmg, ai.GetDamage());
            if (master.GetRace() == Race.ASMODIANS)
                Interlocked.Add(ref aDmg, ai.GetDamage());
            else if (master.GetRace() == Race.ELYOS)
                Interlocked.Add(ref eDmg, ai.GetDamage());
            if (!master.Equals(ai.GetAttacker()))
                name = master.GetName() + "'s " + ai.GetAttacker().GetObjectTemplate().GetL10n();
            sb.Append("\n\tName: " + name + ", Dmg: " + ai.GetDamage() + ", Hate: " + ai.GetHate());
        }
        if (tDmg > 0)
        {
            sb.Append("\n\tTotal Dmg: ").Append(tDmg);
            sb.Append("\n\t\t(A) Dmg: ").Append(aDmg);
            sb.Append("\n\t\t(E) Dmg: ").Append(eDmg);
            sb.Append("\n\t\t(N) Dmg: ").Append(tDmg - aDmg - eDmg);
        }
        return sb.ToString();
    }
}
