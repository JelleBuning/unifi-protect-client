using UniFiApiProtectWebhookDotnet.Abstraction;

namespace UnifiProtectClient.Services.Interfaces;

public interface IDesktopNotifier
{
    void Notify(IAlarmEvent alarmEvent);
}