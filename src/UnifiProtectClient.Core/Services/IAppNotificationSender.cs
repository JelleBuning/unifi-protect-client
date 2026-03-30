using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace UnifiProtectClient.Services;

internal interface IAppNotificationSender
{
    void Notify(string title, string? heroImagePath);
}

[ExcludeFromCodeCoverage]
internal sealed class AppNotificationSender : IAppNotificationSender
{
    public void Notify(string title, string? heroImagePath)
    {
        var builder = new AppNotificationBuilder().AddText(title);
        if (heroImagePath is not null && File.Exists(heroImagePath))
            builder.SetHeroImage(new System.Uri(heroImagePath));
        AppNotificationManager.Default.Show(builder.BuildNotification());
    }
}
