using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeManager.Infrastructure.Migrations
{
    /// <summary>
    /// The four NO ACTION foreign keys were meant to be checked once the user's DELETE had cascaded everywhere, but
    /// Postgres checks NO ACTION at the end of each cascade: Users → Categories found the transactions that
    /// Users → MonthlySummaries → Transactions hadn't deleted yet, and Users → HabitCheckIns came before
    /// Users → Habits → HabitCheckIns, so deleting a user failed (23503). DEFERRABLE INITIALLY DEFERRED checks them at
    /// the commit instead, when the whole cascade is done; a category still in use still can't be deleted.
    /// The EF model doesn't express deferrability, so the model and the snapshot don't change. Additive: the previous
    /// revision works the same on it (expand/contract).
    /// </summary>
    public partial class MakeUserDeletionForeignKeysDeferrable : Migration
    {
        private static readonly (string Table, string Constraint)[] ForeignKeys =
        [
            ("HabitCheckIns", "FK_HabitCheckIns_Users_UserId"),
            ("HabitProgressEntries", "FK_HabitProgressEntries_Users_UserId"),
            ("Transactions", "FK_Transactions_Categories_CategoryId"),
            ("RecurringTransactions", "FK_RecurringTransactions_Categories_CategoryId"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, constraint) in ForeignKeys)
                migrationBuilder.Sql($"""ALTER TABLE "{table}" ALTER CONSTRAINT "{constraint}" DEFERRABLE INITIALLY DEFERRED;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, constraint) in ForeignKeys)
                migrationBuilder.Sql($"""ALTER TABLE "{table}" ALTER CONSTRAINT "{constraint}" NOT DEFERRABLE;""");
        }
    }
}
