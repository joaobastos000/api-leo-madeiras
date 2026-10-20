using ApiFoundation.Integrations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ApiFoundation.Tests;

public class LeomobApiClientFakeTests
{
    private static LeomobApiClientFake Criar(ModoFalhaLeomob modo) =>
        new(Options.Create(new LeomobOptions { ModoFalha = modo }), NullLogger<LeomobApiClientFake>.Instance);

    [Fact]
    public async Task Modo_alternado_falha_na_primeira_e_passa_na_segunda()
    {
        var leomob = Criar(ModoFalhaLeomob.Alternado);

        await Assert.ThrowsAsync<HttpRequestException>(() => leomob.NotificarAsync(1012, 21763699, [], default));
        await leomob.NotificarAsync(1012, 21763699, [], default);
    }

    [Fact]
    public async Task Modo_sempre_falha() =>
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            Criar(ModoFalhaLeomob.Sempre).NotificarAsync(1012, 21763699, [], default));

    [Fact]
    public async Task Modo_nunca_passa() =>
        await Criar(ModoFalhaLeomob.Nunca).NotificarAsync(1012, 21763699, [], default);
}
