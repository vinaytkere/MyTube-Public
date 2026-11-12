using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MyTube.Backend.Application;

public interface IDownloadQueue
{
    ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default);
    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken = default);
    void Clear();
    bool TryDequeue(out Guid jobId);
}
