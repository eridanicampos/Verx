namespace CashFlow.Identity.Api.Domain;

public static class Roles
{
    public const string Operator = "operator";
    public const string Analyst = "analyst";

    private static readonly Dictionary<string, string[]> ScopesByRole = new(StringComparer.Ordinal)
    {
        [Operator] = ["transactions:write", "transactions:read", "balances:read"],
        [Analyst] = ["transactions:read", "balances:read"],
    };

    public static bool IsValid(string role) => ScopesByRole.ContainsKey(role);

    public static IReadOnlyList<string> ScopesOf(string role) =>
        ScopesByRole.TryGetValue(role, out var scopes) ? scopes : [];
}
