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
    [HttpPost(Name = "PostStartMatch")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StartMatch([FromBody] StartMatchInput input)
    {
        try
        {
            var matchId = await _matchService.StartMatchAsync(input);
            return CreatedAtAction(nameof(StartMatch), new { id = matchId }, matchId);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
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
        try
        {
            await _matchService.SetupShipsAsync(input);
            return Ok(new { message = "Navios posicionados com sucesso. Aguardando início." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao posicionar navios.");
            return StatusCode(500, new { error = "Erro interno ao processar setup." });
        }
    }

    /// <summary>
    ///     Executa um tiro (Ação principal do jogo)
    /// </summary>
    /// <remarks>
    ///     Executa um tiro durante o jogo.
    /// </remarks>
    /// <response code="200">Tiro executado com sucesso.</response>
    /// <response code="400">Dados inválidos para executar o tiro.</response>
    [HttpPost("shot", Name = "PostExecuteShot")]
    [ProducesResponseType(typeof(TurnResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExecuteShot([FromBody] ShootInput input)
    {
        try
        {
            var result = await _matchService.ExecutePlayerShotAsync(input);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex) // Turno errado, jogo finalizado, etc.
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (TimeoutException ex) // Tempo esgotado
        {
            return StatusCode(408, new { error = ex.Message });
        }
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
        try
        {
            await _matchService.ExecutePlayerMoveAsync(input);
            return Ok(new { message = "Navio movido com sucesso." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}