using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Handler;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/siege/SiegeCannonAI (@author Whoop).</summary>
[AIName("siege_cannon")]
public class SiegeCannonAI : NpcAI
{
    public SiegeCannonAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        TalkEventHandler.OnTalk(this, player);
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        int morphSkill = GetMorphSkill();
        Npc owner = GetOwner();

        if (dialogActionId == DialogAction.SETPRO1 && morphSkill != 0)
        {
            TeleportService.TeleportTo(player, owner.GetWorldId(), owner.GetInstanceId(), owner.GetX(), owner.GetY(), owner.GetZ(), owner.GetHeading());
            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), morphSkill >> 8, morphSkill & 0xFF, player).UseNoAnimationSkill();
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
            AIActions.DeleteOwner(this);
        }
        return true;
    }

    public int GetMorphSkill()
    {
        switch (GetNpcId())
        {
            case 251725: // Krotan Elyos - Sky Cannon
            case 251726:
            case 251727:
            case 251728:
            case 251729:
            case 251730:
            case 251731:
            case 251732:
            case 251733:
            case 251734:
            case 251745: // Kysis Elyos - Sky Cannon
            case 251746:
            case 251747:
            case 251748:
            case 251749:
            case 251750:
            case 251751:
            case 251752:
            case 251753:
            case 251754:
            case 251765: // Miren Elyos - Sky Cannon
            case 251766:
            case 251767:
            case 251768:
            case 251769:
            case 251770:
            case 251771:
            case 251772:
            case 251773:
            case 251774:
            case 882253: // Divine
            case 882255:
                return 0x540D41; // 21517 65
            case 252164: // Wealhtheow Elyos
            case 252165:
            case 252166:
            case 252167:
            case 252168:
            case 252169:
            case 252170:
                return 0x538941; // 21385 65
            case 251735: // Krotan Asmo - Sky Cannon
            case 251736:
            case 251737:
            case 251738:
            case 251739:
            case 251740:
            case 251741:
            case 251742:
            case 251743:
            case 251744:
            case 251755: // Kysis Asmo - Sky Cannon
            case 251756:
            case 251757:
            case 251758:
            case 251759:
            case 251760:
            case 251761:
            case 251762:
            case 251763:
            case 251764:
            case 251775: // Miren Asmo - Sky Cannon
            case 251776:
            case 251777:
            case 251778:
            case 251779:
            case 251780:
            case 251781:
            case 251782:
            case 251783:
            case 251784:
            case 882254: // Divine Asmo Sky Cannon
            case 882256:
                return 0x540E41; // 21518 65
            case 252171: // Wealhtheow Asmo
            case 252172:
            case 252173:
            case 252174:
            case 252175:
            case 252176:
            case 252177:
                return 0x538A41; // 21386 65
            default:
                return 0;
        }
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.ALLOW_RESPAWN or AIQuestion.REWARD_LOOT => false,
            _ => base.Ask(question),
        };
    }
}
