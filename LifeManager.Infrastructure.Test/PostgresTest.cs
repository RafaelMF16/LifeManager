namespace LifeManager.Infrastructure.Test
{
    /// <summary>
    /// Base of every integration test: joins the serial Postgres collection, carries the Integration trait
    /// (<c>dotnet test --filter Category=Integration</c>) and starts each test on an empty database.
    /// </summary>
    [Collection(PostgresCollection.Name)]
    [Trait("Category", "Integration")]
    public abstract class PostgresTest(PostgresFixture fixture) : IAsyncLifetime
    {
        protected PostgresFixture Fixture { get; } = fixture;

        public Task InitializeAsync() => Fixture.ResetAsync();

        public Task DisposeAsync() => Task.CompletedTask;
    }
}
