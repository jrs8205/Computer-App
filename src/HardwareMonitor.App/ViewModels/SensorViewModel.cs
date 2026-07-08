using System.ComponentModel;
using System.Globalization;
using HardwareMonitor.Core.Sensors;

namespace HardwareMonitor.App.ViewModels;

/// <summary>
/// Yhden sensorin näkymämalli. Nimi ja tyyppi eivät muutu, joten vain
/// <see cref="DisplayValue"/> ilmoittaa muutoksista käyttöliittymälle.
/// Näin puu ei "vilku" joka sekunti, vaan vain arvot päivittyvät.
/// </summary>
public sealed class SensorViewModel : INotifyPropertyChanged
{
    private string _displayValue = "—";

    public SensorViewModel(SensorReading reading)
    {
        Name = reading.SensorName;
        Type = reading.SensorType;
        Unit = reading.Unit;
        Identifier = reading.Identifier;
        Update(reading);
    }

    public string Name { get; }

    public string Type { get; }

    public string Unit { get; }

    public string Identifier { get; }

    public string DisplayValue
    {
        get => _displayValue;
        private set
        {
            if (_displayValue == value)
            {
                return;
            }

            _displayValue = value;
            PropertyChanged?.Invoke(this, DisplayValueChangedArgs);
        }
    }

    public void Update(SensorReading reading) => DisplayValue = Format(reading);

    private static string Format(SensorReading reading)
    {
        if (reading.Value is not { } value)
        {
            return "—";
        }

        string number = value.ToString("0.###", CultureInfo.CurrentCulture);
        return string.IsNullOrEmpty(reading.Unit) ? number : $"{number} {reading.Unit}";
    }

    private static readonly PropertyChangedEventArgs DisplayValueChangedArgs = new(nameof(DisplayValue));

    public event PropertyChangedEventHandler? PropertyChanged;
}
