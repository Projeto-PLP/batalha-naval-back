namespace BatalhaNaval.Domain.Entities;

public class PlayerProfile
{
    public Guid UserId { get; set; }
    public int RankPoints { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int CurrentStreak { get; set; }
    public int MaxStreak { get; set; }
    public List<string> EarnedMedalCodes { get; set; } = new();
    
    public double WinRate => (Wins + Losses) == 0 ? 0 : (double)Wins / (Wins + Losses);

    public void AddWin(int points)
    {
        Wins++;
        CurrentStreak++;
        if (CurrentStreak > MaxStreak) MaxStreak = CurrentStreak;
        RankPoints += points;
    }

    public void AddLoss()
    {
        Losses++;
        CurrentStreak = 0;
        // RankPoints -= points? (Regra de perda de pontos opcional)
    }
}