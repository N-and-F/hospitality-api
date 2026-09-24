namespace Hospitality.Api.Ledger;

public sealed record WalletSetDto(Guid Id, string Name, bool IsMain);

public sealed record WalletSetListResponse(IReadOnlyList<WalletSetDto> Items);

public sealed class CreateWalletSetRequest
{
    public string Name { get; set; } = "";
}

public sealed class RenameWalletSetRequest
{
    public string Name { get; set; } = "";
}

public sealed class SetWalletRequest
{
    public Guid WalletId { get; set; }
}
