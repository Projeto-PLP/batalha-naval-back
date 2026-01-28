namespace BatalhaNaval.Application.DTOs;

public class LoginResponseDto
{
    public string Token { get; set; }
    public DateTime Expiration { get; set; }
    public string Username { get; set; }
    public UserProfileDTO Profile { get; set; }
}
