namespace Genisis.Api.Data;

public static class SqlText
{
    /// <summary>Makes user text match literally inside a SQL Server LIKE pattern.</summary>
    public static string EscapeLike(string value) =>
        value.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
}
