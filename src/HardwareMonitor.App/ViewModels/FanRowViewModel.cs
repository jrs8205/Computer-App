using System.ComponentModel;

namespace HardwareMonitor.App.ViewModels;

/// <summary>
/// Yksi tuuletinrivi Dashboardin Tuulettimet-kortissa. Nimen voi vaihtaa
/// kaksoisklikkaamalla (IsEditing); tyhjä nimi palauttaa oletuksen.
/// </summary>
public sealed class FanRowViewModel : INotifyPropertyChanged
{
    private readonly Action<string, string> _rename;
    private string _displayName;
    private string _rpm = "—";
    private bool _isEditing;
    private string _editText = "";

    public FanRowViewModel(string identifier, string displayName, Action<string, string> rename)
    {
        Identifier = identifier;
        _displayName = displayName;
        _rename = rename;
    }

    public string Identifier { get; }

    public string DisplayName
    {
        get => _displayName;
        set { if (_displayName != value) { _displayName = value; Notify(nameof(DisplayName)); } }
    }

    public string Rpm
    {
        get => _rpm;
        set { if (_rpm != value) { _rpm = value; Notify(nameof(Rpm)); } }
    }

    public bool IsEditing
    {
        get => _isEditing;
        private set { if (_isEditing != value) { _isEditing = value; Notify(nameof(IsEditing)); } }
    }

    public string EditText
    {
        get => _editText;
        set { if (_editText != value) { _editText = value; Notify(nameof(EditText)); } }
    }

    public void BeginEdit()
    {
        EditText = DisplayName;
        IsEditing = true;
    }

    public void CommitEdit()
    {
        if (!IsEditing)
        {
            return;
        }

        IsEditing = false;
        _rename(Identifier, EditText.Trim());
    }

    public void CancelEdit() => IsEditing = false;

    private void Notify(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public event PropertyChangedEventHandler? PropertyChanged;
}
