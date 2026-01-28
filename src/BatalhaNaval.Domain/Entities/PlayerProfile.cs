using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BatalhaNaval.Domain.Entities;

[Table("player_profiles")]
public class PlayerProfile
{
    [Key]
    [ForeignKey(nameof(User))]
    [Column("user_id")]
    [Description("Identificador único do jogador")]
    public Guid UserId { get; set; }

    [Description("Usuário associado ao perfil do jogador")]
    public virtual User? User { get; set; }

    [Description("Pontos de ranking do jogador")]
    [Column("rank_points")]
    public int RankPoints { get; set; }

    [Description("Número de vitórias do jogador")]
    [Column("wins")]
    public int Wins { get; set; }

    [Description("Número de derrotas do jogador")]
    [Column("losses")]
    public int Losses { get; set; }

    [Description("Sequência atual de vitórias do jogador")]
    [Column("current_streak")]
    public int CurrentStreak { get; set; }

    [Description("Maior sequência de vitórias do jogador")]
    [Column("max_streak")]
    public int MaxStreak { get; set; }

    [Description("Data e hora da atualização do perfil")]
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped] public double WinRate => Wins + Losses == 0 ? 0 : (double)Wins / (Wins + Losses);

    [NotMapped] public List<string> EarnedMedalCodes { get; set; } = new();

    [NotMapped] public List<int> MedalIds { get; } = new();

    public bool HasMedal(int medalId)
    {
        return MedalIds.Contains(medalId);
    }

    public void AddMedal(int medalId)
    {
        if (!HasMedal(medalId)) MedalIds.Add(medalId);
    }

    public void AddWin(int points)
    {
        Wins++;
        CurrentStreak++;
        if (CurrentStreak > MaxStreak) MaxStreak = CurrentStreak;
        RankPoints += points;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddLoss()
    {
        Losses++;
        CurrentStreak = 0;
        // RankPoints -= points? (Regra de perda de pontos opcional)
        UpdatedAt = DateTime.UtcNow;
    }
}