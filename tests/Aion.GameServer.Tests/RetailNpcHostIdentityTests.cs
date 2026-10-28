using System.Globalization;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Retail;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcHostIdentityTests
{
    [Fact]
    public void IdentityIsStableAcrossCultureAndDetectsNestedStatChanges()
    {
        var template = new NpcTemplate { npcId = 1, name = "Synthetic", level = 1,
            statsTemplate = new() { MaxHp = 1234, Pdef = 222, Speeds = new() { WalkSpeed = 1.25f } } };
        string original = RetailNpcHostIdentity.Template(template);
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.Equal(original, RetailNpcHostIdentity.Template(template));
            template.statsTemplate.Pdef++;
            Assert.NotEqual(original, RetailNpcHostIdentity.Template(template));
            template.statsTemplate.Pdef--;
            Assert.Equal(original, RetailNpcHostIdentity.Template(template));
            template.level++;
            Assert.NotEqual(original, RetailNpcHostIdentity.Template(template));
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }
}
