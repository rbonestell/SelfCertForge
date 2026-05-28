using System.Windows.Input;
using SelfCertForge.Core.Models;

namespace SelfCertForge.Core.Presentation;

public sealed class CsrSanOriginRowViewModel : ObservableObject
{
    public CsrSanOriginRowViewModel(string value, CsrSignedSanOrigin origin, Action<CsrSanOriginRowViewModel> remove)
    {
        Value = value;
        Origin = origin;
        RemoveCommand = new RelayCommand(() => remove(this));
    }

    public string Value { get; }
    public CsrSignedSanOrigin Origin { get; }
    public bool IsFromCsr => Origin == CsrSignedSanOrigin.FromCsr;
    public ICommand RemoveCommand { get; }
}
