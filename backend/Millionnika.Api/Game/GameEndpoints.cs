using System.Security.Claims;

namespace Millionnika.Api.Game;

public static class GameEndpoints
{
    public static IEndpointRouteBuilder MapGameEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var game = endpoints.MapGroup("/api/game").RequireAuthorization();

        game.MapGet("/contract", GetContractAsync);
        game.MapPost("/contract/execute", ExecuteContractAsync);
        game.MapGet("/training", GetTraining);
        game.MapPost("/training/execute", ExecuteTrainingAsync);
        game.MapGet("/logs", GetLogsAsync);

        return endpoints;
    }

    private static async Task<IResult> GetContractAsync(
        IGameActionService service,
        CancellationToken cancellationToken)
    {
        var contract = await service.GetContractAsync(cancellationToken);
        return contract is null ? Results.NotFound() : Results.Ok(contract);
    }

    private static IResult GetTraining(IGameActionService service) =>
        Results.Ok(service.GetTrainingConfig());

    private static async Task<IResult> ExecuteContractAsync(
        ActionRequest request,
        ClaimsPrincipal principal,
        IGameActionService service,
        CancellationToken cancellationToken)
    {
        if (request.RequestId == Guid.Empty)
        {
            return InvalidRequestId();
        }

        var result = await service.ExecuteContractAsync(GetUserId(principal), request.RequestId, cancellationToken);
        return result.ResourceRejected
            ? Results.Conflict(result.Response)
            : Results.Ok(result.Response);
    }

    private static async Task<IResult> ExecuteTrainingAsync(
        TrainingRequest request,
        ClaimsPrincipal principal,
        IGameActionService service,
        CancellationToken cancellationToken)
    {
        if (request.RequestId == Guid.Empty)
        {
            return InvalidRequestId();
        }

        if (!GameRules.TryParseTrainingStat(request.Stat, out var stat))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["stat"] = ["Разрешены только strength, speed, defense или dexterity."]
            });
        }

        var result = await service.TrainAsync(GetUserId(principal), request.RequestId, stat, cancellationToken);
        return result.ResourceRejected
            ? Results.Conflict(result.Response)
            : Results.Ok(result.Response);
    }

    private static async Task<IResult> GetLogsAsync(
        ClaimsPrincipal principal,
        IGameActionService service,
        CancellationToken cancellationToken) =>
        Results.Ok(await service.GetRecentLogsAsync(GetUserId(principal), cancellationToken));

    private static Guid GetUserId(ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static IResult InvalidRequestId() =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["requestId"] = ["Некорректный ключ действия."]
        });
}
