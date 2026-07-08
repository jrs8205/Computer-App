using LibreHardwareMonitor.Hardware;

namespace HardwareMonitor.Core.Sensors;

/// <summary>
/// Käy läpi kaikki koneen laitteet ja pyytää niitä päivittämään sensoriarvonsa.
/// LibreHardwareMonitorLib vaatii tämän: pelkkä <c>sensor.Value</c> ei päivity
/// itsestään, vaan laitteelle täytyy kutsua <c>Update()</c> ennen lukemista.
/// </summary>
public sealed class UpdateVisitor : IVisitor
{
    public void VisitComputer(IComputer computer) => computer.Traverse(this);

    public void VisitHardware(IHardware hardware)
    {
        hardware.Update();
        foreach (IHardware subHardware in hardware.SubHardware)
        {
            subHardware.Accept(this);
        }
    }

    public void VisitSensor(ISensor sensor) { }

    public void VisitParameter(IParameter parameter) { }
}
