namespace LifeManager.Domain.StatementImports
{
    /// <summary>A draft row as listed: the row plus its category's name, read through a join.</summary>
    public record StatementImportRowListItem(StatementImportRow Row, string? CategoryName);
}
