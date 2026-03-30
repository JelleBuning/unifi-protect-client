using System.Collections.Generic;
using System.Threading;
using UnifiProtectClient.Domain.Events;

namespace UnifiProtectClient.Application.Ports;

public interface IProtectEventStream
{
    IAsyncEnumerable<ProtectEvent> SubscribeAsync(CancellationToken ct = default);
}
