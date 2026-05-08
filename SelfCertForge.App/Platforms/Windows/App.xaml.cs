using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SelfCertForge.App.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
	/// <summary>
	/// Initializes the singleton application object.  This is the first line of authored code
	/// executed, and as such is the logical equivalent of main() or WinMain().
	/// </summary>
	public App()
	{
		this.InitializeComponent();

		// Win2D's CanvasGeometry.CombineWith fails with HRESULT 0x80070490 on
		// virtualized GPUs (WARP fallback in VMs / Parallels ARM64). The exception
		// surfaces on the WinUI dispatcher and aborts the process. Marking it
		// handled keeps the app alive; affected shapes may render without their
		// clip path. On real hardware this branch is never hit.
		this.UnhandledException += (_, e) =>
		{
			if (e.Exception is System.Runtime.InteropServices.COMException com
				&& com.HResult == unchecked((int)0x80070490))
			{
				e.Handled = true;
			}
		};
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}

