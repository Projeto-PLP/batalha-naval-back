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
public class CampaignController : ControllerBase
{
    private readonly ICampaignService _campaignService;

    public CampaignController(ICampaignService campaignService)
    {
        _campaignService = campaignService;
    }

    /// <summary>
    ///     Retorna o progresso atual do jogador no modo campanha.
    /// </summary>
    /// <remarks>
    ///     Retorna o estágio atual, se a campanha foi concluída e quando.
    ///     Se o jogador ainda não iniciou a campanha, cria automaticamente o progresso no estágio 1.
    /// </remarks>
    /// <response code="200">Progresso retornado com sucesso.</response>
    /// <response code="401">Token inválido ou ausente.</response>
    [HttpGet(Name = "GetCampaignProgress")]
    [ProducesResponseType(typeof(CampaignProgressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProgress()
    {
        var userId = User.GetUserId();
        var progress = await _campaignService.GetProgressAsync(userId);
        return Ok(progress);
    }

    /// <summary>
    ///     Inicia uma partida do estágio atual da campanha.
    /// </summary>
    /// <remarks>
    ///     Cria uma partida contra a IA correspondente ao estágio atual do jogador.
    ///     O jogador não pode pular estágios — precisa vencer o estágio atual para avançar.
    ///     Sequência: IA Básica → IA Intermediária → IA Avançada → Campanha Concluída.
    ///     Após receber o matchId, utilize o endpoint POST /match/setup para posicionar os navios
    ///     e continuar o fluxo normal de jogo.
    /// </remarks>
    /// <response code="201">Partida de campanha criada com sucesso.</response>
    /// <response code="400">Campanha já concluída ou dados inválidos.</response>
    /// <response code="409">Jogador já possui uma partida ativa.</response>
    [HttpPost("start", Name = "PostStartCampaignMatch")]
    [ProducesResponseType(typeof(StartCampaignMatchResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> StartCampaignMatch()
    {
        var userId = User.GetUserId();
        var result = await _campaignService.StartCampaignMatchAsync(userId);

        return CreatedAtAction(
            nameof(GetProgress),
            new { },
            result
        );
    }
}


