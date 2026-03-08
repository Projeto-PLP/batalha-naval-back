using BatalhaNaval.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BatalhaNaval.API.Middlewares;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Ocorreu uma exceção não tratada: {Message}", exception.Message);

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Erro Interno do Servidor",
            Detail = "Ocorreu um erro crítico no processamento da requisição.",
            Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1"
        };

        if (exception is ArgumentException)
        {
            problemDetails.Status = StatusCodes.Status400BadRequest;
            problemDetails.Title = "Erro de Validação";
            problemDetails.Detail = exception.Message;
        }

        if (exception is UnauthorizedAccessException)
        {
            problemDetails.Status = StatusCodes.Status403Forbidden;
            problemDetails.Title = "Acesso Negado";
            problemDetails.Detail = exception.Message;
        }

        if (exception is InvalidOperationException)
        {
            problemDetails.Status = StatusCodes.Status409Conflict;
            problemDetails.Title = "Operação Inválida";
            problemDetails.Detail = exception.Message;
        }

        if (exception is KeyNotFoundException)
        {
            problemDetails.Status = StatusCodes.Status404NotFound;
            problemDetails.Title = "Recurso Não Encontrado";
            problemDetails.Detail = exception.Message;
        }

        if (exception is InvalidCoordinateException)
        {
            problemDetails.Status = StatusCodes.Status400BadRequest;
            problemDetails.Title = "Coordenada Inválida";
            problemDetails.Detail = exception.Message;
        }

        if (exception is UserHasActiveMatchException)
        {
            problemDetails.Status = StatusCodes.Status409Conflict;
            problemDetails.Title = "Partida Em Andamento";
            problemDetails.Detail = exception.Message;
        }

        if (exception is OpponentBusyException)
        {
            problemDetails.Status = StatusCodes.Status409Conflict;
            problemDetails.Title = "Oponente Indisponível";
            problemDetails.Detail = exception.Message;
        }

        if (exception is CampaignAlreadyCompletedException)
        {
            problemDetails.Status = StatusCodes.Status400BadRequest;
            problemDetails.Title = "Campanha Já Concluída";
            problemDetails.Detail = exception.Message;
        }

        if (exception is TimeoutException)
        {
            problemDetails.Status = StatusCodes.Status408RequestTimeout;
            problemDetails.Title = "Tempo Esgotado";
            problemDetails.Detail = exception.Message;
        }

        httpContext.Response.StatusCode = problemDetails.Status.Value;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}