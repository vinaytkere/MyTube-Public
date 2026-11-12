using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace MyTube.Backend.Api.Hubs;

public class DownloadHub : Hub
{
    public async Task JoinBatch(string batchId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"batch_{batchId}");
    }

    public async Task LeaveBatch(string batchId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"batch_{batchId}");
    }
}
