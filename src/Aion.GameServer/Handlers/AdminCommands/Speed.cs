using System.Collections.Generic;
using System.Globalization;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Speed (ATracer, Neon). Sets your speed.</summary>
public class Speed : AdminCommand, IStatOwner
{
    public Speed()
        : base("speed", "Sets your speed.")
    {
        SetSyntaxInfo("<0-100> - Set your speed to the specified value (0 to reset).");
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(admin);
            return;
        }

        float parameter = 0;
        if (!float.TryParse(paramsArr[0], NumberStyles.Float, CultureInfo.InvariantCulture, out parameter))
        {
            SendInfo(admin, (string)null); // default info for NumberFormatException
            return;
        }
        if (parameter < 0 || parameter > 100)
        {
            SendInfo(admin, "Speed must be between 0 and 100.");
            return;
        }

        admin.GetGameStats().EndEffect(this);
        if (parameter == 0)
        {
            SendInfo(admin, "Your standard speed has been recovered.");
            return;
        }

        List<IStatFunction> functions = new List<IStatFunction>();
        functions.Add(new SpeedFunction(StatEnum.SPEED, parameter));
        functions.Add(new SpeedFunction(StatEnum.FLY_SPEED, parameter));
        admin.GetGameStats().AddEffect(this, functions);
        SendInfo(admin, "Your speed is now fixed at " + parameter);
    }

    private class SpeedFunction : StatFunction
    {
        private int speed;

        public SpeedFunction(StatEnum stat, float speed)
        {
            this.Stat = stat;
            this.speed = (int)(speed * 1000);
        }

        public override void Apply(Stat2 otherStat, params CalculationType[] calculationTypes)
        {
            otherStat.SetBase(speed);
            otherStat.SetBaseRate(1);
            otherStat.SetBonus(0);
        }

        public override int GetPriority()
        {
            return 120;
        }
    }
}
