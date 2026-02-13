using BatalhaNaval.API.Extensions;
using BatalhaNaval.Application.DTOs;
using BatalhaNaval.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace batalha_naval_back.Controllers;

[ApiController]
[Route("[controller]")]
[Produces("application/json")]
[Authorize]
public class MatchController : ControllerBase
{
    private readonly ILogger<MatchController> _logger;
    private readonly IMatchService _matchService;

    public MatchController(IMatchService matchService, ILogger<MatchController> logger)
    {
        _matchService = matchService;
        _logger = logger;
    }

    /// <summary>
    ///     Inicia uma nova partida (PvP ou PvE)
    /// </summary>
    /// <remarks>
    ///     Inicia uma nova partida (PvP ou PvE).
    /// </remarks>
    /// <response code="201">Partida iniciada com sucesso.</response>
    /// <response code="400">Dados inválidos para iniciar a partida.</response>
    /// <response code="404">Recurso não encontrado.</response>
    /// <response code="409">Conflito ao iniciar partida (usuário já em jogo ou oponente ocupado).</response>
    /// <response code="500">Erro interno ao criar partida.</response>
    [HttpPost(Name = "PostStartMatch")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> StartMatch([FromBody] StartMatchInput input)
    {
        var playerId = User.GetUserId();

        var matchId = await _matchService.StartMatchAsync(input, playerId);

        return CreatedAtAction(
            nameof(StartMatch),
            new { id = matchId },
            new { matchId }
        );
    }

    /// <summary>
    ///     Posiciona os navios no tabuleiro (Fase de Setup)
    /// </summary>
    /// <remarks>
    ///     Posiciona os navios no tabuleiro durante a fase de setup.
    /// </remarks>
    /// <response code="200">Navios posicionados com sucesso.</response>
    /// <response code="400">Dados inválidos para posicionar os navios.</response>
    /// <response code="404">Partida não encontrada.</response>
    [HttpPost("setup", Name = "PostSetupShips")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetupShips([FromBody] PlaceShipsInput input)
    {
        var playerId = User.GetUserId();

        await _matchService.SetupShipsAsync(input, playerId);

        return Ok(new { message = "Navios posicionados com sucesso. Aguardando início." });
    }

    /// <summary>
    ///     Executa um tiro (Ação principal do jogo)
    /// </summary>
    /// <remarks>
    ///     Executa um tiro durante o jogo.
    /// </remarks>
    /// <response code="200">Tiro executado com sucesso.</response>
    /// <response code="400">Dados inválidos para executar o tiro.</response>
    /// <response code="404">Partida não encontrada.</response>
    [HttpPost("shot", Name = "PostExecuteShot")]
    [ProducesResponseType(typeof(TurnResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExecuteShot([FromBody] ShootInput input)
    {
        var playerId = User.GetUserId();

        var result = await _matchService.ExecutePlayerShotAsync(input, playerId);

        return Ok(result);
    }

    /// <summary>
    ///     Move um navio (Apenas Modo Dinâmico)
    /// </summary>
    /// <remarks>
    ///     Move um navio durante o jogo no modo dinâmico.
    /// </remarks>
    /// <response code="200">Navio movido com sucesso.</response>
    /// <response code="400">Dados inválidos para mover o navio.</response>
    [HttpPost("move", Name = "PostMoveShip")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MoveShip([FromBody] MoveShipInput input)
    {
        var playerId = User.GetUserId();

        await _matchService.ExecutePlayerMoveAsync(input, playerId);

        return Ok(new { message = "Navio movido com sucesso." });
    }

    /// <summary>
    ///     Cancela uma partida existente.
    /// </summary>
    /// <remarks>
    ///     Se a partida estiver em 'Setup', ela é excluída sem penalidades.
    ///     Se estiver 'InProgress', conta como derrota para quem cancelou.
    /// </remarks>
    /// <response code="204">Partida cancelada com sucesso.</response>
    /// <response code="401">Token inválido ou ausente.</response>
    /// <response code="403">Jogador não é dono da partida.</response>
    /// <response code="404">Partida não encontrada.</response>
    /// <response code="409">Partida já finalizada.</response>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelMatch(Guid id)
    {
        var playerId = User.GetUserId();

        await _matchService.CancelMatchAsync(id, playerId);

        return NoContent();
    }

    /// <summary>
    ///     Obtém o estado atual da partida (Com Fog of War).
    /// </summary>
    /// <remarks>
    ///     Retorna o tabuleiro do jogador completo e o do oponente mascarado (apenas tiros visíveis).
    /// </remarks>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(MatchGameStateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMatchState(Guid id)
    {
        var playerId = User.GetUserId();

        var state = await _matchService.GetMatchStateAsync(id, playerId);

        return Ok(state);
    }
}