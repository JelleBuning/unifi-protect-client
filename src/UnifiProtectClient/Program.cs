using System;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;

namespace UnifiProtectClient;

public class Program
{
    private static App _app = null!;

    [STAThread]
    static int Main(string[] _)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        var isRedirect = DecideRedirection();

        if (!isRedirect)
        {
            Microsoft.UI.Xaml.Application.Start((p) =>
            {
                var context = new DispatcherQueueSynchronizationContext(
                    DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                _app = new App();
            });
        }

        return 0;
    }

    private static bool DecideRedirection()
    {
        var isRedirect = false;
        var args = AppInstance.GetCurrent().GetActivatedEventArgs();
        var keyInstance = AppInstance.FindOrRegisterForKey("UnifiProtectClient");

        if (keyInstance.IsCurrent)
        {
            keyInstance.Activated += OnActivated;
        }
        else
        {
            isRedirect = true;
            RedirectActivationTo(args, keyInstance);
        }

        return isRedirect;
    }

    private static void OnActivated(object? sender, AppActivationArguments args)
    {
        if (args.Kind == ExtendedActivationKind.ToastNotification)
        {
            _app.OnToastClicked();
        }
    }

    private static void RedirectActivationTo(AppActivationArguments args, AppInstance keyInstance)
    {
        keyInstance.RedirectActivationToAsync(args).AsTask().Wait();
    }
}