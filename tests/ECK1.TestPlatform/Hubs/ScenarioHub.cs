using ECK1.TestPlatform.Data;
using Microsoft.AspNetCore.SignalR;

namespace ECK1.TestPlatform.Hubs;

public sealed class ScenarioHub(RunStore runStore) : Hub
{
    public async Task SubscribeToRun(string runId)
    {
        if (string.IsNullOrWhiteSpace(runId)) return;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"run:{runId}");

        // Immediately send current progress so late-joining clients catch up
        var progress = await runStore.GetProgressAsync(runId);
        if (progress is not null)
        {
            await Clients.Caller.SendAsync("ScenarioProgress", progress);
        }
    }

    public async Task UnsubscribeFromRun(string runId)
    {
        if (string.IsNullOrWhiteSpace(runId)) return;
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"run:{runId}");
    }
}
