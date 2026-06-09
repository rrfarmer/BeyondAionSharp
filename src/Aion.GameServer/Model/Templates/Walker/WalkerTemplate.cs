using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Model.Templates.Walker;

/// <summary>Java parity: model/templates/walker/WalkerTemplate (KKnD).</summary>
[XmlRoot("walker_template")]
public class WalkerTemplate
{
    [XmlType("LoopType")]
    public enum LoopType
    {
        NONE,
        NORMAL,
        WALK_BACK
    }

    [XmlElement("routestep")] private List<RouteStep> routeStepList;

    [XmlAttribute("route_id")] private string routeId;

    [XmlAttribute("pool")] private int pool = 1;

    [XmlAttribute("formation")] private Aion.GameServer.Spawnengine.WalkerGroupType formation = Aion.GameServer.Spawnengine.WalkerGroupType.POINT;

    [XmlAttribute("rows")] private string rowValues;

    [XmlAttribute("loop_type")] private LoopType loopType = LoopType.NORMAL;

    [XmlIgnore] private int[] rows;

    public WalkerTemplate()
    {
    }

    public WalkerTemplate(string routeId)
    {
        this.routeId = routeId;
    }

    // Java parity: afterUnmarshal — invoked post-load by the walker loader.
    public void AfterUnmarshal()
    {
        if (loopType == LoopType.WALK_BACK) // add steps in backward order, so npcs turn and walk the same way back
        {
            for (int i = routeStepList.Count - 2; i > 0; i--) // skip first and last step
            {
                RouteStep step = routeStepList[i];
                routeStepList.Add(new RouteStep(step.GetX(), step.GetY(), step.GetZ(), step.GetRestTime()));
            }
        }
        for (int i = 0; i < routeStepList.Count - 1; i++)
        {
            RouteStep step = routeStepList[i];
            step.SetStepIndex(i);
        }
        RouteStep lastStep = routeStepList[routeStepList.Count - 1];
        lastStep.SetStepIndex(routeStepList.Count - 1);
        lastStep.SetIsLastStep(true);

        if (pool == 2)
        {
            formation = Aion.GameServer.Spawnengine.WalkerGroupType.SQUARE;
            rows = new int[1];
            rows[0] = 2;
        }
        else if (formation == Aion.GameServer.Spawnengine.WalkerGroupType.SQUARE)
        {
            if (rowValues != null)
            {
                string[] values = rowValues.Split(',');
                rows = new int[values.Length];
                for (int i = 0; i < values.Length; i++)
                    rows[i] = int.Parse(values[i]);
            }
            else
            {
                formation = Aion.GameServer.Spawnengine.WalkerGroupType.POINT;
            }
        }
        rowValues = null;
    }

    public List<RouteStep> GetRouteSteps()
    {
        return routeStepList;
    }

    public RouteStep GetRouteStep(int stepIndex)
    {
        return routeStepList[stepIndex];
    }

    public string GetRouteId()
    {
        return routeId;
    }

    public string GetVersionId()
    {
        return DataManager.WALKER_VERSIONS_DATA.GetRouteVersionId(routeId);
    }

    public int GetPool()
    {
        return pool;
    }

    public void SetPool(int pool)
    {
        this.pool = pool;
    }

    public void SetRouteSteps(List<RouteStep> newSteps)
    {
        routeStepList = newSteps;
    }

    // Java parity: getType() — renamed GetType_ (GetType collides with object.GetType()).
    public Aion.GameServer.Spawnengine.WalkerGroupType GetType_()
    {
        return formation;
    }

    public void SetType(Aion.GameServer.Spawnengine.WalkerGroupType type)
    {
        formation = type;
    }

    public LoopType GetLoopType()
    {
        return loopType;
    }

    public void SetLoopType(LoopType loopType)
    {
        this.loopType = loopType;
    }

    public int[] GetRows()
    {
        return rows;
    }

    public void SetRows(int[] rows)
    {
        this.rows = rows;
    }
}
