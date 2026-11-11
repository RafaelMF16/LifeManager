using LifeManager.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace LifeManager.Infrastructure.Test
{
    /// <summary>
    /// One disposable Postgres (the image of docker-compose.yml) per test run, with every migration applied. Tests get a
    /// fresh DbContext per call, so concurrent tasks never share one, and a clean database through ResetAsync.
    /// </summary>
    public sealed class PostgresFixture : IAsyncLifetime
    {
        private const string Image = "postgres:16-alpine";

        private PostgreSqlContainer? _container;
        private string? _connectionString;
        private string? _truncateSql;

        public string ConnectionString => _connectionString ?? throw new InvalidOperationException("The Postgres container hasn't started.");

        public async Task InitializeAsync()
        {
            try
            {
                // Building the container already looks for Docker, so it sits inside the try too.
                _container = new PostgreSqlBuilder(Image).Build();
                await _container.StartAsync();
            }
            catch (Exception exception)
            {
                // No silent skip: without Docker the integration tests fail, saying why.
                throw new InvalidOperationException(
                    "The integration tests need Docker running (check with `docker info`) to start a disposable Postgres. " +
                    "Start Docker Desktop (or the Docker daemon) and run the tests again.", exception);
            }

            // Concurrency tests open one connection per task: a roomy pool keeps the pool itself from serializing them.
            _connectionString = new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { MaxPoolSize = 50 }.ConnectionString;

            // Also fails on a model change without a migration (EF's PendingModelChangesWarning), like the deploy does.
            await using var context = CreateContext();
            await context.Database.MigrateAsync();
        }

        public LifeManagerDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<LifeManagerDbContext>()
                .UseNpgsql(ConnectionString)
                .Options;

            return new LifeManagerDbContext(options);
        }

        /// <summary>Empties every table but the migrations history, so each test starts from a clean database.</summary>
        public async Task ResetAsync()
        {
            await using var context = CreateContext();

            _truncateSql ??= await BuildTruncateSqlAsync(context);
            await context.Database.ExecuteSqlRawAsync(_truncateSql);
        }

        private static async Task<string> BuildTruncateSqlAsync(LifeManagerDbContext context)
        {
            // Read from the catalog, so a table added by a new migration is cleaned too, with no list to keep.
            var tables = await context.Database
                .SqlQueryRaw<string>("""SELECT tablename AS "Value" FROM pg_tables WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory'""")
                .ToListAsync();

            return $"TRUNCATE {string.Join(", ", tables.Select(table => $"\"{table}\""))} RESTART IDENTITY CASCADE";
        }

        public async Task DisposeAsync()
        {
            if (_container is not null)
                await _container.DisposeAsync();
        }
    }

    [CollectionDefinition(Name)]
    public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
    {
        // One collection runs serially: the truncate between tests can't share the database with a parallel test.
        public const string Name = "Postgres";
    }
}
