using System;
using Aion.Commons.Configuration;
using Aion.Commons.Configuration.Transformers;

namespace Aion.GameServer.Services.Cron;

/// <summary>Java parity: services/cron/CronExpressionTransformer (Neon) extends PropertyTransformer&lt;CronExpression&gt;. Config property transformer that parses a string into an interned CronExpression (empty -> null). matches(Class)->Matches(Type); value.isEmpty()->Length==0. Quartz CronExpression / commons PropertyTransformer red-tolerated.</summary>
public class CronExpressionTransformer : PropertyTransformer<CronExpression>
{
    public override bool Matches(Type targetType)
    {
        return targetType == typeof(CronExpression);
    }

    protected override CronExpression ParseObject(string value, TransformationTypeInfo typeInfo)
    {
        return value.Length == 0 ? null : CronExpressions.GetOrCreate(value);
    }
}
