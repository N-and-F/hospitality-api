namespace Hospitality.Api.Ledger;

public sealed class WalletSetSeedHostedService(IServiceProvider services) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var wallets = scope.ServiceProvider.GetRequiredService<WalletSetService>();
        await wallets.EnsureMainAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
