namespace BatalhaNaval.Application.DTOs;

public class UserProfileDTO
{
    public Guid Id  { get; set; }
    public string Username { get; set; }
    public int RankPoints { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public List<string> EarnedMedalCodes { get; set; } = new();
}