using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ApiFoundation.Infrastructure;

/// <summary>
/// Transforma exceções não tratadas em respostas ProblemDetails, sem expor detalhes internos.
/// O log da exceção já é feito pelo middleware UseExceptionHandler.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        // Duas requisições iguais ao mesmo tempo: a segunda esbarra no índice único.
        var violouIndiceUnico = exception is DbUpdateException
        {
            InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }
        };

        var status = violouIndiceUnico
            ? StatusCodes.Status409Conflict
            : StatusCodes.Status500InternalServerError;

        var problema = new ProblemDetails
        {
            Status = status,
            Title = violouIndiceUnico ? "Conflito de concorrência" : "Erro inesperado",
            Detail = violouIndiceUnico ? "Requisição simultânea com os mesmos dados. Reenviar é seguro." : null
        };

        // Permite achar a requisição nos logs sem mostrar o erro ao cliente.
        problema.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problema, options: null, contentType: "application/problem+json", ct);
        return true;
    }
}
