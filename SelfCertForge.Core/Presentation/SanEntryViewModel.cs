using System.Windows.Input;

namespace SelfCertForge.Core.Presentation;

public sealed class SanEntryViewModel
{
    private readonly Action<SanEntryViewModel> _remove;

    public SanEntryViewModel(string type, string value, Action<SanEntryViewModel> remove)
    {
        Type = type;
        Value = value;
        _remove = remove;
        DeleteCommand = new RelayCommand(() => _remove(this));
    }

    public string Type { get; }
    public string Value { get; }
    public ICommand DeleteCommand { get; }
}
