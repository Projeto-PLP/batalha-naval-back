using BatalhaNaval.Application.DTOs;
using BatalhaNaval.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BatalhaNaval.API.Controllers;

[ApiController]
[Route("[controller]")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
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
}