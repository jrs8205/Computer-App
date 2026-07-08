using System.Collections.ObjectModel;

namespace HardwareMonitor.App.ViewModels;

/// <summary>
/// Yhden laitteen näkymämalli puussa. <see cref="Items"/> sisältää sekä
/// alalaitteet (HardwareViewModel) että sensorit (SensorViewModel) samassa
/// listassa; WPF:n TreeView valitsee oikean mallipohjan automaattisesti
/// tietotyypin perusteella.
/// </summary>
public sealed class HardwareViewModel
{
    public HardwareViewModel(string name, string type)
    {
        Name = name;
        Type = type;
    }

    public string Name { get; }

    public string Type { get; }

    public string Header => $"{Name}  ({Type})";

    public ObservableCollection<object> Items { get; } = new();
}
