using ApiFoundation.Integrations;
using ApiFoundation.Tests.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ApiFoundation.Tests.Infra;

/// <summary>
/// Sobe a API inteira em memória, com um banco novo no container
/// e os espiões no lugar das integrações externas.
/// </summary>
public class ApiFactory(PostgresFixture pg) : WebApplicationFactory<Program>
{
    private readonly string _connectionString = pg.ConnectionStringNova();

    public ArmazenamentoEspiao Armazenamento { get; } = new();
    public LeomobEspiao Leomob { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", _connectionString);

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IArmazenamentoArquivos>(Armazenamento);
            services.AddSingleton<ILeomobApiClient>(Leomob);
        });
    }
}
