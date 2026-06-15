using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Items.Actions;
using Aion.GameServer.Services.Event;
using Aion.GameServer.Services.Items;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

[AIName("icefestival_ice_chunk")]
public class IceChunkAI : NpcAI
{
    public IceChunkAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        Item iceCarvingTool = player.GetInventory().GetFirstItemByItemId(164002294);
        if (iceCarvingTool != null)
        {
            SkillUseAction skillUseAction = (SkillUseAction)iceCarvingTool.GetItemTemplate().GetActions().GetItemActions().First();
            skillUseAction.Act(player, iceCarvingTool, null);
        }
    }

    public override void OnEffectApplied(Effect effect)
    {
        if (GetNpcId() < 833516 || GetNpcId() > 833519)
            return;
        if (effect.GetSkillId() == 11014 && effect.GetEffector() is Player player && GetOwner().GetController().Delete())
        {
            if (Rnd.Chance() < 69)
            {
                switch (GetNpcId())
                {
                    case 833516: ItemService.AddItem(player, 188053955, 1, true); break; // [Event] Ice Sculptor's Box
                    case 833517: ItemService.AddItem(player, 188053956, 1, true); break; // [Event] Talented Ice Sculptor's Box
                    case 833518: ItemService.AddItem(player, 188053957, 1, true); break; // [Event] Enheartened Ice Sculptor's Box
                    case 833519: ItemService.AddItem(player, 188053958, 1, true); break; // [Event] Soulful Ice Sculptor's Box
                }
                int nextId = GetNpcId() + 1;
                bool isCompletedIceSculpture = nextId == 833520;
                if (isCompletedIceSculpture)
                {
                    PacketSendUtility.SendMonologue(player, 1501360); // The ice sculpture is a great success!
                    Npc completedIceSculpture = (Npc)Spawn(nextId, "icefestival_completed_ice_sculpture");
                    completedIceSculpture.GetController().AddTask(TaskId.DESPAWN, ThreadPoolManager.GetInstance().Schedule(_ =>
                    {
                        completedIceSculpture.GetController().Delete();
                        Spawn(833516, GetSpawnTemplate().GetAiName());
                        return ValueTask.CompletedTask;
                    }, System.TimeSpan.FromHours(2)));
                }
                else
                {
                    Spawn(nextId, GetSpawnTemplate().GetAiName());
                }
            }
            else
            {
                PacketSendUtility.SendMonologue(player, 1501361); // Oh, the ice sculpture has shattered...
                ThreadPoolManager.GetInstance().Schedule(_ => { Spawn(833516, GetSpawnTemplate().GetAiName()); return ValueTask.CompletedTask; }, System.TimeSpan.FromMinutes(2));
            }
        }
    }

    private VisibleObject Spawn(int npcId, string aiName)
    {
        if (!EventService.GetInstance().IsEventActive(GetSpawnTemplate().GetEventTemplate().GetName()))
            return null;
        return Spawn(npcId, GetPosition().GetX(), GetPosition().GetY(), GetPosition().GetZ(), (sbyte)GetPosition().GetHeading(), 0, aiName);
    }

    public override bool Ask(AIQuestion question)
    {
        if (question == AIQuestion.ALLOW_RESPAWN)
            return false;
        return base.Ask(question);
    }
}
