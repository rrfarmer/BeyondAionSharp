using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Items.Actions;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Controllers.Observer;

/// <summary>
/// Watches all conditions when a stance needs to be removed.
/// Java parity: controllers/observer/StanceObserver (Neon).
/// </summary>
public class StanceObserver : ActionObserver
{
    private readonly Player _player;
    private readonly int _stanceSkillId;

    public StanceObserver(Player player, int stanceSkillId)
        : base(ObserverType.ALL)
    {
        _player = player;
        _stanceSkillId = stanceSkillId;
    }

    public int GetStanceSkillId()
    {
        return _stanceSkillId;
    }

    public override void StartSkillCast(Skill skill)
    {
        string stack = skill.GetSkillTemplate().GetStack();
        if (!stack.StartsWith("ITEM_") && !stack.StartsWith("REMEDY_") && !stack.StartsWith("POTION_")) // pots and scrolls don't stop stance
            _player.GetController().StopStance();
    }

    public override void Itemused(Item item)
    {
        ItemActions actions = item.GetItemTemplate().GetActions();
        if (actions != null && actions.GetSkillUseAction() == null) // skill actions are checked in startSkillCast, here we stop on RideAction etc.
            _player.GetController().StopStance();
    }

    public override void Abnormalsetted(AbnormalState state)
    {
        if ((state.GetId() & AbnormalState.STANCE_OFF.GetId()) != 0)
            _player.GetController().StopStance();
    }
}
