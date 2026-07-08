using System.ComponentModel;
using HardwareMonitor.Core.Analysis;

namespace HardwareMonitor.App.ViewModels;

/// <summary>Yksi levyrivi Dashboardin Levyt-kortissa: teksti + väritila.</summary>
public sealed class DiskRowViewModel : INotifyPropertyChanged
{
    private string _text = "";
    private ThresholdState _state;

    public string Text
    {
        get => _text;
        set { if (_text != value) { _text = value; Notify(nameof(Text)); } }
    }

    public ThresholdState State
    {
        get => _state;
        set { if (_state != value) { _state = value; Notify(nameof(State)); } }
    }

    private void Notify(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public event PropertyChangedEventHandler? PropertyChanged;
}
