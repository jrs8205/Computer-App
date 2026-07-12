using System.ComponentModel;
using System.Globalization;
using HardwareMonitor.Core.Settings;

namespace HardwareMonitor.App.ViewModels;

/// <summary>
/// Yksi numeroasetuskenttä: teksti ↔ float. Kelvollinen arvo menee
/// apply-delegaatille (kirjoitus AppSettingsiin + tallennus); virheellinen
/// jättää virheviestin näkyviin eikä tallenna mitään.
/// </summary>
public sealed class NumericFieldViewModel : INotifyPropertyChanged
{
    private static readonly CultureInfo Fi = CultureInfo.GetCultureInfo("fi-FI");

    private readonly float _min;
    private readonly float _max;
    private readonly Action<float> _apply;
    private readonly Func<float, string?>? _crossCheck;
    private readonly Func<float, float>? _normalize;

    private string _text;
    private string? _error;

    public NumericFieldViewModel(
        float initialValue, float min, float max,
        Action<float> apply, Func<float, string?>? crossCheck = null,
        Func<float, float>? normalize = null)
    {
        _min = min;
        _max = max;
        _apply = apply;
        _crossCheck = crossCheck;
        _normalize = normalize;
        _text = Format(initialValue);
    }

    public string Text
    {
        get => _text;
        set
        {
            _text = value;
            ParseResult result = SettingsValidator.ParseNumber(value, _min, _max);
            string? error = result.Error
                ?? (result.Value is { } v ? _crossCheck?.Invoke(v) : null);
            if (error is null && result.Value is { } ok)
            {
                // Normalisointi (esim. pyöristys kokonaisluvuksi) ENNEN
                // tallennusta ja näyttöä — kenttä ei saa näyttää eri arvoa
                // kuin sovellus oikeasti käyttää (esim. "5,6" vs. 6).
                float applied = _normalize?.Invoke(ok) ?? ok;
                _apply(applied);
                _text = Format(applied);
            }

            Error = error;
            OnChanged(nameof(Text));
        }
    }

    public string? Error
    {
        get => _error;
        private set
        {
            if (_error == value)
            {
                return;
            }

            _error = value;
            OnChanged(nameof(Error));
            OnChanged(nameof(HasError));
        }
    }

    public bool HasError => _error is not null;

    /// <summary>Päivittää tekstin ulkoisen muutoksen (oletusten palautus) jälkeen tallentamatta.</summary>
    public void Refresh(float value)
    {
        _text = Format(value);
        Error = null;
        OnChanged(nameof(Text));
    }

    private static string Format(float v) => v.ToString("0.##", Fi);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
