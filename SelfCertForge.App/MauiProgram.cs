using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
using SkiaSharp.Views.Maui.Controls.Hosting;
#if WINDOWS
using Microsoft.Maui.LifecycleEvents;
#endif
using SelfCertForge.App.Dialogs;
using SelfCertForge.App.Navigation;
using SelfCertForge.App.Services;
using SelfCertForge.App.Shell;
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Presentation;
using SelfCertForge.Infrastructure;
using Velopack;

namespace SelfCertForge.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // Velopack must run before any other app code to handle install/update hooks.
        // Wrapped in try/catch to gracefully handle unsupported platforms (e.g. macCatalyst dev builds).
        try { VelopackApp.Build().Run(); } catch { /* not a Velopack-managed install */ }

#if MACCATALYST
        Platforms.MacCatalyst.HandlerCustomizations.Apply();
#elif WINDOWS
        Platforms.Windows.HandlerCustomizations.Apply();
#endif

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseSkiaSharp()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Inter-Regular.ttf", "InterRegular");
                fonts.AddFont("Inter-SemiBold.ttf", "InterSemiBold");
                fonts.AddFont("Inter-Bold.ttf", "InterBold");
                fonts.AddFont("JetBrainsMono-Regular.ttf", "JetBrainsMono");
                fonts.AddFont("JetBrainsMono-Medium.ttf", "JetBrainsMonoMedium");
                fonts.AddFont("JetBrainsMono-SemiBold.ttf", "JetBrainsMonoSemiBold");
            })
#if WINDOWS
            .ConfigureMauiHandlers(handlers =>
            {
                handlers.AddHandler<Microsoft.Maui.Controls.Shapes.Path,
                    Platforms.Windows.NativeMauiPathHandler>();
                handlers.AddHandler<Microsoft.Maui.Controls.Shapes.Ellipse,
                    Platforms.Windows.NativeMauiEllipseHandler>();
                handlers.AddHandler<Microsoft.Maui.Controls.Border,
                    Platforms.Windows.NativeMauiBorderHandler>();
            })
            .ConfigureLifecycleEvents(events => events.AddWindows(w => w.OnWindowCreated(window =>
                Platforms.Windows.WindowCustomizations.ApplyTitleBar(window))))
#endif
        ;

        builder.Services.AddSelfCertForgeInfrastructure();

        // GitHub releases informational poll. Single endpoint, polled once per
        // launch — IHttpClientFactory would be over-engineered. The HttpClient
        // singleton lives for the app's lifetime alongside the service.
        builder.Services.AddSingleton<IGithubReleaseService>(_ =>
            new GithubReleaseService(new HttpClient(), "rbonestell", "SelfCertForge"));

        // All app data lives under a dedicated SelfCertForge subfolder of the
        // platform app-data root. Resolving it also runs the one-time migration
        // that relocates legacy files which earlier builds wrote straight into
        // the root. Computed once here so every store shares the same path.
        var dataRoot = DataFolderLayout.Resolve(FileSystem.AppDataDirectory);

        // Preferences store — must register before consumers (activity log, dialogs, settings vm).
        builder.Services.AddSingleton<IUserPreferencesStore>(sp =>
        {
            var store = new JsonUserPreferencesStore(dataRoot);
            // Best-effort load; failures fall back to defaults inside the store.
            _ = store.LoadAsync();
            return store;
        });

        builder.Services.AddSingleton<ICertificateStore>(_ =>
            new JsonCertificateStore(dataRoot));
        builder.Services.AddSingleton<IActivityLog>(sp =>
            new JsonActivityLog(dataRoot,
                sp.GetRequiredService<IUserPreferencesStore>()));

        // Platform-specific data-folder reveal service.
#if MACCATALYST
        builder.Services.AddSingleton<IDataFolderService>(_ =>
            new Platforms.MacCatalyst.MacDataFolderService(dataRoot));
#elif WINDOWS
        builder.Services.AddSingleton<IDataFolderService>(_ =>
            new Platforms.Windows.WindowsDataFolderService(dataRoot));
#endif

        builder.Services.AddSingleton<SettingsViewModel>(sp => new SettingsViewModel(
            sp.GetRequiredService<IUpdateService>(),
            sp.GetRequiredService<IUserPreferencesStore>(),
            sp.GetRequiredService<IActivityLog>(),
            // Optional — only registered on macCatalyst/Windows; null on other TFMs.
            sp.GetService<IDataFolderService>(),
            sp.GetRequiredService<IConfirmationDialog>(),
            sp.GetRequiredService<IGithubReleaseService>()));
        builder.Services.AddSingleton<ShellViewModel>(sp => new ShellViewModel(
            sp.GetRequiredService<SettingsViewModel>(),
            sp.GetRequiredService<IGithubReleaseService>()));
        builder.Services.AddSingleton<ShellPage>(sp => new ShellPage(
            sp.GetRequiredService<ShellViewModel>()));
        builder.Services.AddSingleton<CertificatesViewModel>(sp => new CertificatesViewModel(
            sp.GetRequiredService<ICertificateStore>(),
            sp.GetRequiredService<ICertificateExportService>(),
            sp.GetRequiredService<IFolderPicker>(),
            sp.GetRequiredService<IPfxPasswordDialog>(),
            sp.GetRequiredService<IConfirmationDialog>(),
            sp.GetRequiredService<ITrustStoreChecker>()));
        builder.Services.AddSingleton<DashboardViewModel>(sp => new DashboardViewModel(
            sp.GetRequiredService<ICertificateStore>(),
            sp.GetRequiredService<IActivityLog>(),
            sp.GetRequiredService<ITrustStoreChecker>()));
        // Dialogs — transient so each open starts fresh.
        builder.Services.AddSingleton<IForgeService>(sp => new ForgeService(
            sp.GetRequiredService<ICertificateStore>(),
            sp.GetRequiredService<IActivityLog>(),
            sp.GetRequiredService<ICertificateWorkflowService>(),
            dataRoot));
        builder.Services.AddSingleton<INavigationService, NavigationService>();
        builder.Services.AddSingleton<IFolderPicker, MauiFolderPicker>();
        builder.Services.AddSingleton<IPfxPasswordDialog, MauiPfxPasswordDialog>();
        builder.Services.AddSingleton<IConfirmationDialog, MauiConfirmationDialog>();
        builder.Services.AddSingleton<ITrustStoreChecker, SystemTrustStoreChecker>();
        builder.Services.AddSingleton<ICreateRootDialog, CreateRootDialogHost>();
        builder.Services.AddSingleton<ICreateSignedCertDialog, CreateSignedCertDialogHost>();
        builder.Services.AddSingleton<ICsrFilePicker, MauiCsrFilePicker>();
        builder.Services.AddSingleton<ILoadingOverlay, MauiLoadingOverlay>();
        builder.Services.AddSingleton<ICreateFromCsrDialog>(sp => new CreateFromCsrDialogHost(sp));
        builder.Services.AddTransient<CreateFromCsrDialog>();
        builder.Services.AddTransient<CreateFromCsrDialogViewModel>(sp => new CreateFromCsrDialogViewModel(
            sp.GetRequiredService<IForgeService>(),
            sp.GetRequiredService<IUserPreferencesStore>()));
        builder.Services.AddTransient<CreateRootDialogViewModel>(sp => new CreateRootDialogViewModel(
            sp.GetRequiredService<IForgeService>(),
            sp.GetRequiredService<IUserPreferencesStore>()));
        builder.Services.AddTransient<CreateSignedCertDialogViewModel>(sp => new CreateSignedCertDialogViewModel(
            sp.GetRequiredService<IForgeService>(),
            sp.GetRequiredService<IUserPreferencesStore>()));
        builder.Services.AddTransient<CreateRootDialog>();
        builder.Services.AddTransient<CreateSignedCertDialog>();

        // AuthoritiesViewModel depends on both dialog services — registered after them.
        builder.Services.AddSingleton<AuthoritiesViewModel>(sp => new AuthoritiesViewModel(
            sp.GetRequiredService<ICertificateStore>(),
            sp.GetRequiredService<ICreateRootDialog>(),
            sp.GetRequiredService<ICreateSignedCertDialog>(),
            sp.GetRequiredService<INavigationService>(),
            sp.GetRequiredService<ICertificateExportService>(),
            sp.GetRequiredService<IFolderPicker>(),
            sp.GetRequiredService<IPfxPasswordDialog>(),
            sp.GetRequiredService<IConfirmationDialog>(),
            sp.GetRequiredService<ITrustStoreChecker>(),
            sp.GetRequiredService<ICreateFromCsrDialog>(),
            sp.GetRequiredService<ICsrFilePicker>(),
            sp.GetRequiredService<ICertificateWorkflowService>()));

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
