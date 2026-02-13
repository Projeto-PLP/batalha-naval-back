using BatalhaNaval.Application.DTOs;

namespace BatalhaNaval.Application.Interfaces;

public interface IMatchService
{
    Task<Guid> StartMatchAsync(StartMatchInput input, Guid playerId);

    Task SetupShipsAsync(PlaceShipsInput input, Guid playerId);

    Task<TurnResultDto> ExecutePlayerShotAsync(ShootInput input, Guid playerId);

    Task ExecutePlayerMoveAsync(MoveShipInput input, Guid playerId); // Modo Dinâmico
    // O turno da IA pode ser disparado automaticamente após o turno do jogador

    Task CancelMatchAsync(Guid matchId, Guid playerId);

    Task<MatchGameStateDto> GetMatchStateAsync(Guid matchId, Guid playerId);
}