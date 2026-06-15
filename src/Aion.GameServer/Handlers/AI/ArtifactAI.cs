using System;
using System.Threading.Tasks;
using Aion.Commons.Lang;
using Aion.GameServer.Ai;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.Siege;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Model.Templates.Siegelocation;
using Aion.GameServer.Model.Templates.Spawns.Siegespawns;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.SkillEngine.Properties;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/ArtifactAI (ATracer, Source).</summary>
[AIName("artifact")]
public class ArtifactAI : NpcAI
{
    private static readonly ILogger log = NullLogger.Instance;

    public ArtifactAI(Npc owner)
        : base(owner)
    {
    }

    protected new SiegeSpawnTemplate GetSpawnTemplate()
    {
        return (SiegeSpawnTemplate)base.GetSpawnTemplate();
    }

    protected override void HandleDialogStart(Player player)
    {
        ArtifactLocation loc = SiegeService.GetInstance().GetArtifact(GetSpawnTemplate().GetSiegeId());
        // open artifact activation window
        AIActions.AddRequest(this, player, SM_QUESTION_WINDOW.STR_ASK_ARTIFACT_POPUPDIALOG, loc.GetCoolDown(), new ActivationRequest(this, player));
    }

    protected override void HandleDialogFinish(Player player)
    {
    }

    public void OnActivate(Player player)
    {
        ArtifactLocation loc = SiegeService.GetInstance().GetArtifact(GetSpawnTemplate().GetSiegeId());

        // Get Skill id, item, count and target defined for each artifact.
        ArtifactActivation activation = loc.GetTemplate().GetActivation();
        int skillId = activation.GetSkillId();
        int itemId = activation.GetItemId();
        int count = activation.GetCount();
        SkillTemplate skillTemplate = DataManager.SKILL_DATA.GetSkillTemplate(skillId);

        if (skillTemplate == null)
        {
            log.LogError("No skill template for artifact effect id : " + skillId);
            return;
        }

        if (loc.GetCoolDown() > 0 || !loc.GetStatus().Equals(ArtifactStatus.IDLE))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANNOT_USE_ARTIFACT_OUT_OF_ORDER());
            return;
        }

        if (loc.GetLegionId() != 0)
            if (!player.IsLegionMember() || player.GetLegion().GetLegionId() != loc.GetLegionId()
                || !player.GetLegionMember().HasRights(LegionPermissionsMask.ARTIFACT))
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANNOT_USE_ARTIFACT_HAVE_NO_AUTHORITY());
                return;
            }

        if (player.GetInventory().GetItemCountByItemId(itemId) < count)
            return;

        if (LoggingConfig.LOG_SIEGE)
            log.LogInformation("Artifact " + GetSpawnTemplate().GetSiegeId() + " activated by " + player.GetName() + " (race: " + player.GetRace() + ")");

        if (!loc.GetStatus().Equals(ArtifactStatus.IDLE))
            return;
        // Brodcast start activation.
        SM_SYSTEM_MESSAGE startMessage = SM_SYSTEM_MESSAGE.STR_ARTIFACT_CASTING(player.GetRace().GetL10n(), player.GetName(), skillTemplate.GetL10n());
        loc.SetStatus(ArtifactStatus.ACTIVATION);
        SM_ABYSS_ARTIFACT_INFO3 artifactInfo = new SM_ABYSS_ARTIFACT_INFO3(loc.GetLocationId());
        player.GetPosition().GetWorldMapInstance().ForEachPlayer(p =>
        {
            PacketSendUtility.SendPacket(p, startMessage);
            PacketSendUtility.SendPacket(p, artifactInfo);
        });

        PacketSendUtility.SendPacket(player, new SM_USE_OBJECT(player.GetObjectId(), GetObjectId(), 10000, 1));
        PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(player, EmotionType.START_QUESTLOOT, 0, GetObjectId()), true);

        ItemUseObserver observer = new ArtifactItemUseObserver(this, player, loc, skillTemplate);
        player.GetObserveController().Attach(observer);
        player.GetController().AddTask(TaskId.ACTION_ITEM_NPC, ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            player.GetObserveController().RemoveObserver(observer);

            PacketSendUtility.SendPacket(player, new SM_USE_OBJECT(player.GetObjectId(), GetObjectId(), 10000, 0));
            PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(player, EmotionType.END_QUESTLOOT, 0, GetObjectId()), true);
            if (!player.GetInventory().DecreaseByItemId(itemId, count))
                return ValueTask.CompletedTask;
            SM_SYSTEM_MESSAGE message = SM_SYSTEM_MESSAGE.STR_ARTIFACT_CORE_CASTING(loc.GetRace().GetL10n(), skillTemplate.GetL10n());
            loc.SetStatus(ArtifactStatus.CASTING);
            SM_ABYSS_ARTIFACT_INFO3 artifactInfoInner = new SM_ABYSS_ARTIFACT_INFO3(loc.GetLocationId());

            player.GetPosition().GetWorldMapInstance().ForEachPlayer(p =>
            {
                PacketSendUtility.SendPacket(p, message);
                PacketSendUtility.SendPacket(p, artifactInfoInner);
            });

            loc.SetLastActivation(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            if (loc.GetTemplate().GetRepeatCount() == 1)
                ThreadPoolManager.GetInstance().Schedule(new ArtifactUseSkill(this, loc, player, skillTemplate), 13000L);
            else
            {
                ScheduledTask s = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(new ArtifactUseSkill(this, loc, player, skillTemplate), 13000L,
                    loc.GetTemplate().GetRepeatInterval() * 1000L);
                ThreadPoolManager.GetInstance().Schedule(_ =>
                {
                    s.Cancel(true);
                    loc.SetStatus(ArtifactStatus.IDLE);
                    return ValueTask.CompletedTask;
                }, 13000L + (loc.GetTemplate().GetRepeatInterval() * loc.GetTemplate().GetRepeatCount() * 1000L));
            }
            return ValueTask.CompletedTask;
        }, 10000L));
    }

    private sealed class ActivationRequest : AIRequest
    {
        private readonly ArtifactAI ai;
        private readonly Player player;

        public ActivationRequest(ArtifactAI ai, Player player)
        {
            this.ai = ai;
            this.player = player;
        }

        public override void AcceptRequest(Creature requester, Player responder, int requestId)
        {
            // show required item and count in confirm dialog
            AIActions.AddRequest(ai, player, SM_QUESTION_WINDOW.STR_ASK_USE_ARTIFACT, new UseArtifactRequest(ai),
                ChatUtil.L10n(716570), SiegeService.GetInstance().GetArtifact(ai.GetSpawnTemplate().GetSiegeId()).GetTemplate().GetActivation().GetCount());
        }
    }

    private sealed class UseArtifactRequest : AIRequest
    {
        private readonly ArtifactAI ai;

        public UseArtifactRequest(ArtifactAI ai)
        {
            this.ai = ai;
        }

        public override void AcceptRequest(Creature requester, Player responder, int requestId)
        {
            ai.OnActivate(responder);
        }
    }

    private sealed class ArtifactItemUseObserver : ItemUseObserver
    {
        private readonly ArtifactAI ai;
        private readonly Player player;
        private readonly ArtifactLocation loc;
        private readonly SkillTemplate skillTemplate;

        public ArtifactItemUseObserver(ArtifactAI ai, Player player, ArtifactLocation loc, SkillTemplate skillTemplate)
        {
            this.ai = ai;
            this.player = player;
            this.loc = loc;
            this.skillTemplate = skillTemplate;
        }

        public override void Abort()
        {
            player.GetController().CancelTask(TaskId.ACTION_ITEM_NPC);
            PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(player, EmotionType.END_QUESTLOOT, 0, ai.GetObjectId()), true);
            PacketSendUtility.SendPacket(player, new SM_USE_OBJECT(player.GetObjectId(), ai.GetObjectId(), 10000, 0));
            SM_SYSTEM_MESSAGE message = SM_SYSTEM_MESSAGE.STR_ARTIFACT_CANCELED(loc.GetRace().GetL10n(), skillTemplate.GetL10n());
            loc.SetStatus(ArtifactStatus.IDLE);
            SM_ABYSS_ARTIFACT_INFO3 artifactInfo = new SM_ABYSS_ARTIFACT_INFO3(loc.GetLocationId());
            ai.GetOwner().GetPosition().GetWorldMapInstance().ForEachPlayer(p =>
            {
                PacketSendUtility.SendPacket(p, message);
                PacketSendUtility.SendPacket(p, artifactInfo);
            });
        }
    }

    private sealed class ArtifactUseSkill : Runnable
    {
        private readonly ArtifactAI ai;
        private readonly ArtifactLocation artifact;
        private readonly Player player;
        private readonly SkillTemplate skill;
        private int runCount = 1;
        private readonly SM_ABYSS_ARTIFACT_INFO3 pkt;
        private readonly SM_SYSTEM_MESSAGE message;

        public ArtifactUseSkill(ArtifactAI ai, ArtifactLocation artifact, Player activator, SkillTemplate skill)
        {
            this.ai = ai;
            this.artifact = artifact;
            this.player = activator;
            this.skill = skill;
            this.pkt = new SM_ABYSS_ARTIFACT_INFO3(artifact.GetLocationId());
            this.message = SM_SYSTEM_MESSAGE.STR_ARTIFACT_FIRE(activator.GetRace().GetL10n(), player.GetName(), skill.GetL10n());
        }

        public void Run()
        {
            if (artifact.GetTemplate().GetRepeatCount() < runCount)
                return;

            bool start = (runCount == 1);
            bool end = (runCount == artifact.GetTemplate().GetRepeatCount());
            runCount++;
            if (start)
            {
                artifact.SetStatus(ArtifactStatus.ACTIVATED);
            }
            ai.GetOwner().GetPosition().GetWorldMapInstance().ForEachPlayer(p =>
            {
                if (start)
                {
                    PacketSendUtility.SendPacket(p, message);
                    PacketSendUtility.SendPacket(p, pkt);
                }
                if (end)
                {
                    PacketSendUtility.SendPacket(p, pkt);
                }
            });
            bool pc = skill.GetProperties().GetTargetSpecies() == TargetSpeciesAttribute.PC;
            artifact.ForEachCreature(creature =>
            {
                if (creature.GetActingCreature() is Player || (creature is SiegeNpc && !pc))
                {
                    switch (skill.GetProperties().GetTargetRelation())
                    {
                        case TargetRelationAttribute.FRIEND:
                            if (player.IsEnemy(creature))
                                return;
                            break;
                        case TargetRelationAttribute.ENEMY:
                            if (!player.IsEnemy(creature))
                                return;
                            break;
                    }
                    SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(skill, skill.GetLvl(), ai.GetOwner(), creature);
                }
            });
        }
    }
}
