namespace BatalhaNaval.Domain.Entities;

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

    public Guid UserId { get; set; }
    public int MedalId { get; set; }
    public DateTime EarnedAt { get; set; }

    public virtual User User { get; set; }
    public virtual Medal Medal { get; set; }
}