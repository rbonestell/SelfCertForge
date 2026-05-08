using Microsoft.Extensions.DependencyInjection;
using SelfCertForge.Core.Models;
using SelfCertForge.Core.Presentation;

namespace SelfCertForge.App.Pages;

public partial class SettingsView : ContentView
{
    public SettingsView()
        : this(ResolveViewModel()) { }

    public SettingsView(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private static SettingsViewModel ResolveViewModel()
    {
        var services = IPlatformApplication.Current?.Services
            ?? throw new InvalidOperationException("Platform service provider is not available.");
        return services.GetRequiredService<SettingsViewModel>();
    }

    private SettingsViewModel Vm => (SettingsViewModel)BindingContext;

    // -- Key size segments ---------------------------------------------------
    private void OnKeyBits2048(object? sender, EventArgs e) => Vm.KeyBits = 2048;
    private void OnKeyBits3072(object? sender, EventArgs e) => Vm.KeyBits = 3072;
    private void OnKeyBits4096(object? sender, EventArgs e) => Vm.KeyBits = 4096;

    // -- Hash algorithm segments --------------------------------------------
    private void OnHashSha256(object? sender, EventArgs e) => Vm.HashAlgorithm = HashAlgorithmKind.Sha256;
    private void OnHashSha384(object? sender, EventArgs e) => Vm.HashAlgorithm = HashAlgorithmKind.Sha384;
    private void OnHashSha512(object? sender, EventArgs e) => Vm.HashAlgorithm = HashAlgorithmKind.Sha512;

    // -- Activity retention segments ----------------------------------------
    private void OnRetention100(object? sender, EventArgs e)        => Vm.ActivityRetention = ActivityRetention.OneHundred;
    private void OnRetention500(object? sender, EventArgs e)        => Vm.ActivityRetention = ActivityRetention.FiveHundred;
    private void OnRetention1000(object? sender, EventArgs e)       => Vm.ActivityRetention = ActivityRetention.OneThousand;
    private void OnRetentionUnlimited(object? sender, EventArgs e)  => Vm.ActivityRetention = ActivityRetention.Unlimited;
}
