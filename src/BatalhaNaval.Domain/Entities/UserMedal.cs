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

    public Guid UserId { get; private set; }
    public int MedalId { get; private set; }
    public DateTime EarnedAt { get; private set; }

    public virtual User User { get; private set; }
    public virtual Medal Medal { get; private set; }
}