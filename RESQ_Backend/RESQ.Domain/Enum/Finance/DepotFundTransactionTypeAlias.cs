namespace RESQ.Domain.Enum.Finance;

public static class DepotFundTransactionTypeAlias
{
    public const string LegacySelfAdvance = "SelfAdvance";
    public const string LegacyDebtRepayment = "DebtRepayment";

    public static string Normalize(string? transactionType)
        => transactionType switch
        {
            null => string.Empty,
            var value when value.Equals(LegacySelfAdvance, global::System.StringComparison.OrdinalIgnoreCase)
                => DepotFundTransactionType.PersonalAdvance.ToString(),
            var value when value.Equals(LegacyDebtRepayment, global::System.StringComparison.OrdinalIgnoreCase)
                => DepotFundTransactionType.AdvanceRepayment.ToString(),
            _ => transactionType!
        };

    public static bool TryParse(string? transactionType, out DepotFundTransactionType type)
        => global::System.Enum.TryParse(Normalize(transactionType), ignoreCase: true, out type);

    public static string[] Expand(IReadOnlyCollection<DepotFundTransactionType> transactionTypes)
    {
        if (transactionTypes.Count == 0)
        {
            return [];
        }

        var names = new HashSet<string>(global::System.StringComparer.OrdinalIgnoreCase);
        foreach (var transactionType in transactionTypes)
        {
            foreach (var name in GetNames(transactionType))
            {
                names.Add(name);
            }
        }

        return names.ToArray();
    }

    public static string[] GetNames(DepotFundTransactionType transactionType)
        => transactionType switch
        {
            DepotFundTransactionType.PersonalAdvance =>
            [
                DepotFundTransactionType.PersonalAdvance.ToString(),
                LegacySelfAdvance
            ],
            DepotFundTransactionType.AdvanceRepayment =>
            [
                DepotFundTransactionType.AdvanceRepayment.ToString(),
                LegacyDebtRepayment
            ],
            _ => [transactionType.ToString()]
        };
}
