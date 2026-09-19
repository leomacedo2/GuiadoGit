using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Portfolio.Api.Data;

public sealed class DatabaseExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not NpgsqlException && exception.InnerException is not NpgsqlException) return false;
        context.Response.StatusCode = 503;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = 503, Title = "Banco indisponível",
            Detail = "Não foi possível acessar a persistência. Confira a configuração PostgreSQL e as migrations no backend. Tente novamente após restabelecer a conexão."
        }, ct);
        return true;
    }
}
