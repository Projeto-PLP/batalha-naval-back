using System.Security.Claims;
using BatalhaNaval.Application.DTOs;
using BatalhaNaval.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatalhaNaval.API.Controllers;

[ApiController]
[Route("[controller]")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly ICacheService _cacheService;
    private readonly IUserService _userService;

    public UsersController(IUserService userService, ICacheService cacheService)
    {
        _userService = userService;
        _cacheService = cacheService;
    }

    /// <summary>
    ///     Cria um novo usuário.
    /// </summary>
    /// <remarks>
    ///     Cria um usuário com nome de usuário e senha fornecidos.
    /// </remarks>
    /// <response code="201">Usuário criado com sucesso.</response>
    /// <response code="400">Nome de usuário já está em uso ou dados inválidos.</response>
    [HttpPost(Name = "PostCreateUser")]
    [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateUserDto request)
    {
        try
        {
            var result = await _userService.RegisterUserAsync(request);
            return CreatedAtAction(nameof(Create), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Erro interno ao criar usuário." });
        }
    }

    /// <summary>
    ///     Obtém o perfil atualizado do usuário logado.
    /// </summary>
    /// <remarks>
    ///     Obtém o perfil do usuário autenticado, incluindo pontos de ranking, vitórias, derrotas e outras informações
    ///     relevantes.
    /// </remarks>
    /// <response code="200">Perfil obtido com sucesso.</response>
    /// <response code="401">Usuário não autenticado.</response>
    /// <response code="404">Usuário não encontrado.</response>
    [HttpGet("profile")]
    [Authorize]
    [ProducesResponseType(typeof(UserProfileDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile()
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdString, out var userId))
            return Unauthorized();

        var cacheKey = $"profile:{userId}";
        var cachedProfile = await _cacheService.GetAsync<UserProfileDTO>(cacheKey);

        if (cachedProfile != null)
            return Ok(cachedProfile);

        var user = await _userService.GetByIdAsync(userId);
        if (user == null) return NotFound("Usuário não encontrado.");

        // TODO Verificar se pode adicionar Medalhas ao cache
        var profileDto = new UserProfileDTO
        {
            RankPoints = user.Profile.RankPoints,
            Wins = user.Profile.Wins,
            Losses = user.Profile.Losses
        };

        await _cacheService.SetAsync(cacheKey, profileDto, TimeSpan.FromMinutes(10));

        return Ok(profileDto);
    }


    /// <summary>
    ///     Obtem o ranking global de jogadores.
    /// </summary>
    /// <remarks>
    ///     Retorna uma lista dos melhores jogadores baseada em pontos de ranking,
    ///     incluindo a classificação em categorias (S, A, B, C). Os dados são cacheados por 5 minutos.
    /// </remarks>
    /// <response code="200">Ranking obtido com sucesso.</response>
    /// <response code="500">Erro interno ao processar o ranking.</response>
    [HttpGet("player_stats")]
    [Authorize]
    [ProducesResponseType(typeof(UserProfileDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRanking()
    {
        const string cacheKey = "global_ranking";
        var cachedRanking = await _cacheService.GetAsync<List<RankingEntryDto>>(cacheKey);

        if (cachedRanking != null) return Ok(cachedRanking);

        var ranking = await _userService.GetRankingAsync();

        //cache de 5 minutos eh suficiente
        await _cacheService.SetAsync(cacheKey, ranking, TimeSpan.FromMinutes(5));

        return Ok(ranking);
    }
}