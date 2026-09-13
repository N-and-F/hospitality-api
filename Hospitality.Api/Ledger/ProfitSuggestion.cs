namespace Hospitality.Api.Ledger;

public static class ProfitSuggestion
{
    public static decimal For(string method, decimal amount)
    {
        if (TransactionMethods.ForcesZeroProfit.Contains(method))
        {
            return 0;
        }

        if (method is TransactionMethods.LoadViaGcash or TransactionMethods.LoadViaTpc)
        {
            return amount < 100 ? 5 : 10;
        }

        if (method is TransactionMethods.CashIn or TransactionMethods.CashOut or TransactionMethods.CashOutWithCharge)
        {
            if (amount < 100)
            {
                return 5;
            }

            var twoPercent = amount * 0.02m;
            var rounded = Math.Round(twoPercent / 10m, MidpointRounding.AwayFromZero) * 10m;
            return Math.Max(10m, rounded);
        }

        return 0;
    }
}
