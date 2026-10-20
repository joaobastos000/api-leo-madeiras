using ApiFoundation.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ApiFoundation.Tests;

public class GlobalExceptionHandlerTests
{
    private static async Task<(int Status, string Corpo)> TratarAsync(Exception exception)
    {
        var contexto = new DefaultHttpContext();
        contexto.Response.Body = new MemoryStream();

        var tratou = await new GlobalExceptionHandler().TryHandleAsync(contexto, exception, default);
        Assert.True(tratou);

        contexto.Response.Body.Position = 0;
        return (contexto.Response.StatusCode, await new StreamReader(contexto.Response.Body).ReadToEndAsync());
    }

    [Fact]
    public async Task Violacao_de_unicidade_vira_409()
    {
        var violacao = new PostgresException("duplicado", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation);

        var (status, _) = await TratarAsync(new DbUpdateException("erro ao gravar", violacao));

        Assert.Equal(StatusCodes.Status409Conflict, status);
    }

    [Fact]
    public async Task Erro_generico_vira_500_sem_detalhes()
    {
        var (status, corpo) = await TratarAsync(new InvalidOperationException("segredo interno"));

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.DoesNotContain("segredo", corpo);
    }
}
