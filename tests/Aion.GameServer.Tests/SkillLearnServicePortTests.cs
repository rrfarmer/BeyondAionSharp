using Aion.GameServer.Services;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Tests;

public sealed class SkillLearnServicePortTests
{
    [Fact]
    public void SkillRemovalEndsOnlyEffectsThatDependOnTheLearnedSkill()
    {
        Assert.True(SkillLearnService.ShouldRemoveEffectOnSkillRemoval(new SkillTemplate
        {
            activationAttribute = ActivationAttribute.PASSIVE,
            stack = "PASSIVE_SKILL"
        }));
        Assert.True(SkillLearnService.ShouldRemoveEffectOnSkillRemoval(new SkillTemplate
        {
            activationAttribute = ActivationAttribute.TOGGLE,
            stack = "TOGGLE_SKILL"
        }));
        Assert.True(SkillLearnService.ShouldRemoveEffectOnSkillRemoval(new SkillTemplate
        {
            activationAttribute = ActivationAttribute.ACTIVE,
            isDeityAvatar = true,
            stack = "AVATAR_SKILL"
        }));
        Assert.True(SkillLearnService.ShouldRemoveEffectOnSkillRemoval(new SkillTemplate
        {
            activationAttribute = ActivationAttribute.ACTIVE,
            stack = "WS_BOOSTATKSPEED"
        }));
        Assert.False(SkillLearnService.ShouldRemoveEffectOnSkillRemoval(new SkillTemplate
        {
            activationAttribute = ActivationAttribute.ACTIVE,
            stack = "STIGMA_BUFF"
        }));
    }
}
