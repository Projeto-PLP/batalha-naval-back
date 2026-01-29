using Microsoft.AspNetCore.Mvc;
using BatalhaNaval.Application.DTOs;
using BatalhaNaval.Application.Interfaces;
using BatalhaNaval.Domain.Interfaces;

namespace BatalhaNaval.API.Controllers;

[ApiController]
[Route("[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordService _passwordService;
    private readonly ITokenService _tokenService;

    public AuthController(
        IUserRepository userRepository,
        IPasswordService passwordService,
        ITokenService tokenService)
    {
        _userRepository = userRepository;
        _passwordService = passwordService;
        _tokenService = tokenService;
    }


    /// <summary>
    ///     Efetua o login de um usuário.
    /// </summary>
    /// <remarks>
    ///     Efetua o login de um usuário com nome de usuário e senha fornecidos.
    /// </remarks>
    /// <response code="200">Login efetuado com sucesso.</response>
    /// <response code="401">Nome de usuário ou senha inválidos.</response>
    [HttpPost("login", Name = "PostLogin")]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)] 
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
    {
        var user = await _userRepository.GetByUsernameAsync(loginDto.Username);

        if (user == null || !_passwordService.VerifyPassword(loginDto.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Usuário ou senha inválidos.", StatusCode = 401 });
        }

        var token = _tokenService.GenerateToken(user);

        return Ok(new LoginResponseDto
        {
            Token = token,
            Expiration = DateTime.UtcNow.AddHours(2),
            Username = user.Username,
            Profile = user.Profile != null ? new UserProfileDTO
            {
                RankPoints = user.Profile.RankPoints,
                Wins = user.Profile.Wins,
                Losses = user.Profile.Losses
            } : new UserProfileDTO()
        });
    }
}