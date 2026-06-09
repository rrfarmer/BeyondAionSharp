using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Walker;

/// <summary>Java parity: model/templates/walker/RouteStep (KKnD, Rolandas).</summary>
[XmlRoot("routestep")]
public class RouteStep
{
    [XmlAttribute("x")] private float x;

    [XmlAttribute("y")] private float y;

    [XmlAttribute("z")] private float z;

    [XmlAttribute("rest_time")] private int restTime = 0;

    [XmlIgnore] private int stepIndex;

    [XmlIgnore] private bool isLastStep;

    protected RouteStep()
    {
    }

    public RouteStep(float x, float y, float z, int restTime)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.restTime = restTime;
    }

    public float GetX()
    {
        return x;
    }

    public float GetY()
    {
        return y;
    }

    public float GetZ()
    {
        return z;
    }

    public void SetZ(float z)
    {
        this.z = z;
    }

    public int GetRestTime()
    {
        return restTime;
    }

    public int GetStepIndex()
    {
        return stepIndex;
    }

    public void SetStepIndex(int stepIndex)
    {
        this.stepIndex = stepIndex;
    }

    public bool IsLastStep()
    {
        return isLastStep;
    }

    public void SetIsLastStep(bool isLastStep)
    {
        this.isLastStep = isLastStep;
    }
}
