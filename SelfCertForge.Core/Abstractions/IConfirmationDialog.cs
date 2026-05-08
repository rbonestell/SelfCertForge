namespace SelfCertForge.Core.Abstractions;

public interface IConfirmationDialog
{
    Task<bool> ShowAsync(string title, string message, string confirmLabel = "Confirm", string cancelLabel = "Cancel");
}
