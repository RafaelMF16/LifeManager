using System.Text;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Application.Transactions.DTOs;
using LifeManager.Application.Transactions.Export;
using LifeManager.Application.Transactions.Services;
using LifeManager.Domain.Categories;
using LifeManager.Domain.FinanceDashboard.Errors;
using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.Rewards;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Transactions;
using LifeManager.Domain.Transactions.Enums;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Transactions
{
    /// <summary><c>TransactionExportService</c>: the CSV of a period's transactions.</summary>
    [Collection("ApplicationServices")]
    public class TransactionExportServiceTest : BaseTest
    {
        private static readonly UserId FirstUserId = new(1);
        private static readonly UserId SecondUserId = new(2);

        private const int MarketCategoryId = 1;
        private const int SalaryCategoryId = 2;
        private const int OtherUserCategoryId = 3;

        private const int JanuaryId = 10;
        private const int FebruaryId = 11;
        private const int MarchId = 12;
        private const int OtherUserFebruaryId = 20;

        private readonly TransactionExportService _service;

        public TransactionExportServiceTest()
        {
            _service = ServiceProvider.GetRequiredService<TransactionExportService>();

            TransactionSingleton.Instance.Clear();
            MonthlySummarySingleton.Instance.Clear();
            CategorySingleton.Instance.Clear();
            RewardRedemptionSingleton.Instance.Clear();

            SeedCategory(MarketCategoryId, FirstUserId, "Mercado");
            SeedCategory(SalaryCategoryId, FirstUserId, "Salário");
            SeedCategory(OtherUserCategoryId, SecondUserId, "Outros");

            MonthlySummarySingleton.Instance.Add(MonthlySummary.FromPersistence(JanuaryId, FirstUserId.Value, 1, 2026, 0, 0, 0));
            MonthlySummarySingleton.Instance.Add(MonthlySummary.FromPersistence(FebruaryId, FirstUserId.Value, 2, 2026, 0, 0, 0));
            MonthlySummarySingleton.Instance.Add(MonthlySummary.FromPersistence(MarchId, FirstUserId.Value, 3, 2026, 0, 0, 0));
            MonthlySummarySingleton.Instance.Add(MonthlySummary.FromPersistence(OtherUserFebruaryId, SecondUserId.Value, 2, 2026, 0, 0, 0));
        }

        [Theory]
        [InlineData(null, "2026-02", "FinanceDashboard.InvalidFrom")]
        [InlineData("2026-13", "2026-02", "FinanceDashboard.InvalidFrom")]
        [InlineData("2026-01", "fev", "FinanceDashboard.InvalidTo")]
        [InlineData("2026-03", "2026-02", "FinanceDashboard.EndBeforeStart")]
        public async Task PrepareAsync_ShouldReturnTheDashboardsError_WhenThePeriodIsInvalid(string? from, string? to, string code)
        {
            var result = await _service.PrepareAsync(FirstUserId, new TransactionExportQueryDto { From = from, To = to }, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(code, result.Error.Code);
        }

        [Fact]
        public async Task PrepareAsync_ShouldReturnPeriodTooLong_WhenThePeriodHasMoreThan36Months()
        {
            var tooLong = await _service.PrepareAsync(FirstUserId, new TransactionExportQueryDto { From = "2023-01", To = "2026-01" }, CancellationToken.None);
            var longest = await _service.PrepareAsync(FirstUserId, new TransactionExportQueryDto { From = "2023-02", To = "2026-01" }, CancellationToken.None);

            Assert.False(tooLong.IsSuccess);
            Assert.Equal(FinanceDashboardErrors.PeriodTooLong, tooLong.Error);
            Assert.True(longest.IsSuccess);
        }

        [Fact]
        public async Task PrepareAsync_ShouldCountTheRowsAndNameTheFile_WhenThePeriodIsValid()
        {
            SeedTransaction(1, FebruaryId, MoneyFlowType.Expense, MarketCategoryId, 10m, "Pão", new DateOnly(2026, 2, 3));
            SeedTransaction(2, MarchId, MoneyFlowType.Income, SalaryCategoryId, 100m, "Salário", new DateOnly(2026, 3, 1));
            SeedTransaction(3, OtherUserFebruaryId, MoneyFlowType.Expense, OtherUserCategoryId, 99m, "Alheio", new DateOnly(2026, 2, 3));

            var ptBr = await _service.PrepareAsync(FirstUserId, new TransactionExportQueryDto { From = "2026-01", To = "2026-02" }, CancellationToken.None);
            var enUs = await _service.PrepareAsync(FirstUserId, new TransactionExportQueryDto { From = "2026-01", To = "2026-03", Format = TransactionCsvFormat.EnUs }, CancellationToken.None);

            Assert.True(ptBr.IsSuccess);
            Assert.Equal(1, ptBr.Value!.RowCount);
            Assert.Equal("lancamentos-2026-01_2026-02.csv", ptBr.Value.FileName);
            Assert.Equal(new DateOnly(2026, 1, 1), ptBr.Value.From);
            Assert.Equal(new DateOnly(2026, 2, 28), ptBr.Value.To);

            Assert.True(enUs.IsSuccess);
            Assert.Equal(2, enUs.Value!.RowCount);
            Assert.Equal("transactions-2026-01_2026-03.csv", enUs.Value.FileName);
        }

        [Fact]
        public async Task PrepareAsync_ShouldCountZero_WhenThePeriodHasNoTransactions()
        {
            var result = await _service.PrepareAsync(FirstUserId, new TransactionExportQueryDto { From = "2025-01", To = "2025-12" }, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(0, result.Value!.RowCount);
        }

        [Fact]
        public async Task WriteAsync_ShouldWriteTheBomTheHeaderAndTheRowsByDateThenId_OnlyAsynchronously()
        {
            SeedTransaction(5, FebruaryId, MoneyFlowType.Expense, MarketCategoryId, 30m, "Feira", new DateOnly(2026, 2, 10));
            SeedTransaction(2, FebruaryId, MoneyFlowType.Expense, MarketCategoryId, 20m, "Padaria", new DateOnly(2026, 2, 10));
            SeedTransaction(9, JanuaryId, MoneyFlowType.Income, SalaryCategoryId, 1000m, "Salário", new DateOnly(2026, 1, 5));

            var (bytes, text) = await Export(FirstUserId, new TransactionExportQueryDto { From = "2026-01", To = "2026-03" });

            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
            Assert.Equal(
                "Data;Descrição;Categoria;Tipo;Valor;Mês;Origem\r\n"
                + "05/01/2026;Salário;Salário;Receita;1000,00;2026-01;Manual\r\n"
                + "10/02/2026;Padaria;Mercado;Despesa;-20,00;2026-02;Manual\r\n"
                + "10/02/2026;Feira;Mercado;Despesa;-30,00;2026-02;Manual\r\n",
                text);
        }

        [Fact]
        public async Task WriteAsync_ShouldWriteOnlyTheHeader_WhenThePeriodHasNoTransactions()
        {
            var (_, text) = await Export(FirstUserId, new TransactionExportQueryDto { From = "2026-01", To = "2026-03", Format = TransactionCsvFormat.EnUs });

            Assert.Equal("Date,Description,Category,Type,Amount,Month,Source\r\n", text);
        }

        [Theory]
        [InlineData(TransactionTypeFilter.Expense, "Pão")]
        [InlineData(TransactionTypeFilter.Income, "Salário")]
        [InlineData(TransactionTypeFilter.Investment, "Tesouro")]
        public async Task WriteAsync_ShouldKeepOnlyTheType_WhenFilteredByType(TransactionTypeFilter type, string description)
        {
            SeedTransaction(1, FebruaryId, MoneyFlowType.Expense, MarketCategoryId, 10m, "Pão", new DateOnly(2026, 2, 3));
            SeedTransaction(2, FebruaryId, MoneyFlowType.Income, SalaryCategoryId, 100m, "Salário", new DateOnly(2026, 2, 4));
            SeedTransaction(3, FebruaryId, MoneyFlowType.Investment, SalaryCategoryId, 50m, "Tesouro", new DateOnly(2026, 2, 5));

            var query = new TransactionExportQueryDto { From = "2026-02", To = "2026-02", Type = type };
            var prepared = await _service.PrepareAsync(FirstUserId, query, CancellationToken.None);
            var (_, text) = await Export(FirstUserId, query);

            var lines = DataLines(text);
            Assert.Equal(1, prepared.Value!.RowCount);
            Assert.Single(lines);
            Assert.Equal(description, lines[0].Split(';')[1]);
        }

        [Fact]
        public async Task WriteAsync_ShouldNeverWriteAnotherUsersTransactions()
        {
            SeedTransaction(1, FebruaryId, MoneyFlowType.Expense, MarketCategoryId, 10m, "Meu", new DateOnly(2026, 2, 3));
            SeedTransaction(2, OtherUserFebruaryId, MoneyFlowType.Expense, OtherUserCategoryId, 99m, "Alheio", new DateOnly(2026, 2, 3));

            var (_, mine) = await Export(FirstUserId, new TransactionExportQueryDto { From = "2026-02", To = "2026-02" });
            var (_, theirs) = await Export(SecondUserId, new TransactionExportQueryDto { From = "2026-02", To = "2026-02" });

            Assert.Equal(["03/02/2026;Meu;Mercado;Despesa;-10,00;2026-02;Manual"], DataLines(mine));
            Assert.Equal(["03/02/2026;Alheio;Outros;Despesa;-99,00;2026-02;Manual"], DataLines(theirs));
        }

        [Fact]
        public async Task WriteAsync_ShouldLeaveOutTheMonthsOutsideThePeriod()
        {
            SeedTransaction(1, JanuaryId, MoneyFlowType.Expense, MarketCategoryId, 10m, "Janeiro", new DateOnly(2026, 1, 31));
            SeedTransaction(2, FebruaryId, MoneyFlowType.Expense, MarketCategoryId, 10m, "Fevereiro", new DateOnly(2026, 2, 1));
            SeedTransaction(3, MarchId, MoneyFlowType.Expense, MarketCategoryId, 10m, "Março", new DateOnly(2026, 3, 1));

            var (_, text) = await Export(FirstUserId, new TransactionExportQueryDto { From = "2026-02", To = "2026-02" });

            Assert.Equal(["01/02/2026;Fevereiro;Mercado;Despesa;-10,00;2026-02;Manual"], DataLines(text));
        }

        [Fact]
        public async Task WriteAsync_ShouldNameEachOrigin()
        {
            SeedTransaction(1, FebruaryId, MoneyFlowType.Expense, MarketCategoryId, 10m, "Manual", new DateOnly(2026, 2, 1));
            SeedTransaction(2, FebruaryId, MoneyFlowType.Expense, MarketCategoryId, 10m, "Recorrente", new DateOnly(2026, 2, 2), recurringId: 7);
            SeedTransaction(3, FebruaryId, MoneyFlowType.Expense, MarketCategoryId, 10m, "Importada", new DateOnly(2026, 2, 3), importId: 8, externalId: "abc");
            SeedTransaction(4, FebruaryId, MoneyFlowType.Expense, MarketCategoryId, 10m, "Import fora do histórico", new DateOnly(2026, 2, 4), externalId: "def");
            SeedTransaction(5, FebruaryId, MoneyFlowType.Expense, MarketCategoryId, 10m, "Jantar", new DateOnly(2026, 2, 5));
            RewardRedemptionSingleton.Instance.Add(RewardRedemption.FromPersistence(
                1, 1, FirstUserId.Value, "Jantar", null, 50, new DateOnly(2026, 2, 5), DateTimeOffset.UtcNow, null, idTransaction: 5));

            var (_, text) = await Export(FirstUserId, new TransactionExportQueryDto { From = "2026-02", To = "2026-02" });

            Assert.Equal(
                ["Manual", "Recorrente", "Importação", "Importação", "Resgate"],
                DataLines(text).Select(line => line.Split(';')[^1]));
        }

        [Fact]
        public async Task WriteAsync_ShouldWriteEveryRow_WhenThereAreManyMoreThanOneBuffer()
        {
            const int count = 2000;
            for (var id = 1; id <= count; id++)
                SeedTransaction(id, MarchId, MoneyFlowType.Expense, MarketCategoryId, 1m, $"Lançamento com uma descrição longa número {id}", new DateOnly(2026, 3, 1 + id % 28));

            var (_, text) = await Export(FirstUserId, new TransactionExportQueryDto { From = "2026-03", To = "2026-03" });

            Assert.Equal(count, DataLines(text).Length);
        }

        private async Task<(byte[] Bytes, string Text)> Export(UserId userId, TransactionExportQueryDto query)
        {
            var prepared = await _service.PrepareAsync(userId, query, CancellationToken.None);
            Assert.True(prepared.IsSuccess);

            using var output = new AsyncOnlyStream();
            await _service.WriteAsync(prepared.Value!, output, CancellationToken.None);

            var bytes = output.ToArray();
            return (bytes, new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3));
        }

        private static string[] DataLines(string text)
            => [.. text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Skip(1)];

        private static void SeedCategory(int id, UserId userId, string name)
        {
            var category = Category.Create(userId.Value, name).Value!;
            category.AssignId(id);
            CategorySingleton.Instance.Add(category);
        }

        private static void SeedTransaction(
            int id, int monthId, MoneyFlowType type, int categoryId, decimal amount, string description, DateOnly date,
            int? recurringId = null, int? importId = null, string? externalId = null)
            => TransactionSingleton.Instance.Add(Transaction.FromPersistence(id, monthId, type, categoryId, amount, description, date, recurringId, importId, externalId));

        /// <summary>Refuses synchronous writes, like Kestrel's response body.</summary>
        private sealed class AsyncOnlyStream : MemoryStream
        {
            private bool _writingAsync;

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (!_writingAsync) throw SyncWrite();
                base.Write(buffer, offset, count);
            }

            public override void Write(ReadOnlySpan<byte> buffer)
            {
                if (!_writingAsync) throw SyncWrite();
                base.Write(buffer);
            }

            public override void WriteByte(byte value) => throw SyncWrite();

            public override void Flush() => throw SyncWrite();

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                _writingAsync = true;
                try
                {
                    Write(buffer.Span);
                }
                finally
                {
                    _writingAsync = false;
                }

                return ValueTask.CompletedTask;
            }

            private static InvalidOperationException SyncWrite() => new("Synchronous writes are not allowed");
        }
    }
}
