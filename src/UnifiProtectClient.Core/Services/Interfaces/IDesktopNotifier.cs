using UnifiProtectClient.Domain.Events;

namespace UnifiProtectClient.Services.Interfaces;

public interface IDesktopNotifier
{
    void Notify(ProtectEvent protectEvent, string cameraName);
}