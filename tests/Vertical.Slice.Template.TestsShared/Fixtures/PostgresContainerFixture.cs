using Dapper;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;
using Vertical.Slice.Template.Shared.Core.Extensions;
using Vertical.Slice.Template.TestsShared.Helpers;
using Xunit.Sdk;

namespace Vertical.Slice.Template.TestsShared.Fixtures;

public class PostgresContainerFixture : IAsyncLifetime
{
    private readonly IMessageSink _messageSink;
    public PostgresContainerOptions PostgresContainerOptions { get; }
    public PostgreSqlContainer Container { get; }
    public int HostPort => Container.GetMappedPublicPort(PostgreSqlBuilder.PostgreSqlPort);
    public int TcpContainerPort => PostgreSqlBuilder.PostgreSqlPort;

    public PostgresContainerFixture(IMessageSink messageSink)
    {
        _messageSink = messageSink;
        PostgresContainerOptions = ConfigurationHelper.BindOptions<PostgresContainerOptions>();
        PostgresContainerOptions.NotBeNull();

        var postgresContainerBuilder = new PostgreSqlBuilder()
            .WithDatabase(PostgresContainerOptions.DatabaseName)
            .WithCleanUp(true)
            .WithName(PostgresContainerOptions.Name)
            .WithImage(PostgresContainerOptions.ImageName);

        Container = postgresContainerBuilder.Build();
    }

    public async Task InitializeAsync()
    {
        await Container.StartAsync();
        _messageSink.OnMessage(
            new DiagnosticMessage(
                $"Postgres fixture started on Host port {HostPort} and container tcp port {TcpContainerPort}..."
            )
        );
    }

    public async Task ResetDbAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(Container.GetConnectionString());
            await connection.OpenAsync(cancellationToken);
            // after new nugget version respawn than 6 according this https://github.com/jbogard/Respawn/pull/115 pull request we don't need this check and should remove
            await CheckForExistingDatabase(connection);

            var checkpoint = await Respawner.CreateAsync(
                connection,
                new RespawnerOptions { DbAdapter = DbAdapter.Postgres }
            );
            // https://github.com/jbogard/Respawn/issues/108
            // https://github.com/jbogard/Respawn/pull/115 - fixed
            // waiting for new nuget version of respawn, current is 6.
            await checkpoint.ResetAsync(connection)!;
        }
        catch (Exception e)
        {
            throw new Exception(e.Message);
        }
    }

    public async Task DisposeAsync()
    {
        await Container.StopAsync();
        await Container.DisposeAsync(); //important for the event to cleanup to be fired!
        _messageSink.OnMessage(new DiagnosticMessage("Postgres fixture stopped."));
    }

    private async Task CheckForExistingDatabase(NpgsqlConnection connection)
    {
        var existsDb = await connection.ExecuteScalarAsync<bool>(
            "SELECT 1 FROM  pg_catalog.pg_database WHERE datname= @dbname",
            param: new { dbname = PostgresContainerOptions.DatabaseName }
        );
        if (existsDb == false)
        {
            await connection.ExecuteAsync(
                "CREATE DATABASE @DBName",
                param: new { DBName = PostgresContainerOptions.DatabaseName }
            );
        }

        // //https://github.com/jbogard/Respawn/issues/108
        // var existsFoo = await connection.ExecuteScalarAsync<bool>(
        //     "SELECT EXISTS (SELECT FROM information_schema.tables WHERE  table_schema = 'foo' AND table_name = 'public')"
        // );
        // if (existsFoo == false)
        // {
        //     await connection.ExecuteAsync(
        //         "create table \"foo\" (value int)");
        // }
    }
}

public sealed class PostgresContainerOptions
{
    public string Name { get; set; } = "postgres_" + Guid.NewGuid();
    public string ImageName { get; set; } = "postgres:latest";
    public string DatabaseName { get; set; } = "test_db";
}
