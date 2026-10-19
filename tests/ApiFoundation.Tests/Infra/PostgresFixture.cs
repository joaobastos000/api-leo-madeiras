using ApiFoundation.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ApiFoundation.Tests.Infra;

/// <summary>
/// Sobe um único container PostgreSQL para toda a execução dos testes.
/// Cada teste recebe um banco novo, com a migration aplicada, para não interferir nos outros.
/// </summary>
public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public string ConnectionStringNova() =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = $"t_{Guid.NewGuid():N}"
        }.ConnectionString;

    public async Task<AppDbContext> CriarBancoAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionStringNova())
            .Options;

        var db = new AppDbContext(options);
        await db.Database.MigrateAsync(); // cria o banco, as tabelas e o seed
        return db;
    }
}

[CollectionDefinition("postgres")]
public class PostgresCollection : ICollectionFixture<PostgresFixture>
{
}
