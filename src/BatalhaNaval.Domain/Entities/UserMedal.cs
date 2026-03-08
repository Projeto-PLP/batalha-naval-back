using System.ComponentModel.DataAnnotations.Schema; 
using BatalhaNaval.Domain.Entities;

namespace BatalhaNaval.Domain.Entities;


[Table("user_medals")]
public class UserMedal
{
    protected UserMedal()
    {
    }

    public UserMedal(Guid userId, int medalId)
    {
        if (userId == Guid.Empty) throw new ArgumentException("User inválido");
        if (medalId <= 0) throw new ArgumentException("Medalha inválida");

        UserId = userId;
        MedalId = medalId;
        EarnedAt = DateTime.UtcNow;
    }

    [Column("user_id")]
    public Guid UserId { get; set; }
    
    [Column("medal_id")]
    public int MedalId { get; set; }
    
    [Column("earned_at")]
    public DateTime EarnedAt { get; set; }

    public virtual User User { get; set; }
    public virtual Medal Medal { get; set; }
}