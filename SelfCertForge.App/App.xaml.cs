using Microsoft.Extensions.DependencyInjection;
using SelfCertForge.App.Shell;

namespace SelfCertForge.App;

public partial class App : Application
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "selfcertforge_startup_error.log");

    private readonly IServiceProvider _services;

    static App()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Dump("AppDomain.UnhandledException", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
            Dump("TaskScheduler.UnobservedTaskException", e.Exception);
    }

    /// <summary>
    /// Public entry point for command handlers / async fire-and-forget paths to
    /// route exceptions through the same disk dump used at startup. UI-thread
    /// exceptions from button clicks otherwise crash to the ObjC trampoline
    /// without leaving any managed trace.
    /// </summary>
    public static void Report(string where, Exception ex) => Dump(where, ex);

    public App(IServiceProvider services)
    {
        try
        {
            InitializeComponent();
            _services = services;
        }
        catch (Exception ex)
        {
            Dump("App ctor", ex);
            throw;
        }
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        try
        {
            var shellPage = _services.GetRequiredService<ShellPage>();
            return new Window(shellPage)
            {
                Title = "SelfCertForge",
                Width = 1100,
                Height = 760,
                MinimumWidth = 880,
                MinimumHeight = 640,
#if WINDOWS
                // Transparent BackgroundColor lets the underlying app surface
                // (and the brand_mark) show through the title bar area, so the
                // forge graphic overlaps into the title bar like it did before
                // we wired up MAUI's TitleBar control. The text/icon still
                // render via the system caption.
                TitleBar = new TitleBar
                {
                    Title = "SelfCertForge",
                    BackgroundColor = Colors.Transparent,
                    ForegroundColor = Color.FromArgb("#F2F3F6"),
                    HeightRequest = 40,
                },
#endif
            };
        }
        catch (Exception ex)
        {
            Dump("CreateWindow", ex);
            throw;
        }
    }

    private static void Dump(string where, Exception? ex)
    {
        try
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[{DateTime.Now:O}] {where}");
            for (var cur = ex; cur is not null; cur = cur.InnerException)
            {
                sb.AppendLine($"--- {cur.GetType().FullName}: {cur.Message}");
                sb.AppendLine(cur.StackTrace);
            }
            sb.AppendLine();
            File.AppendAllText(LogPath, sb.ToString());
        }
        catch { /* best-effort */ }
    }
}
