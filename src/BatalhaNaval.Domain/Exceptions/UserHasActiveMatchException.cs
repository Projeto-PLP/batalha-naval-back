namespace BatalhaNaval.Domain.Exceptions;

public class UserHasActiveMatchException : Exception
{
    public UserHasActiveMatchException(Guid matchId)
        : base($"O usuário já possui uma partida ativa (ID: {matchId}).")
    {
        MatchId = matchId;
    }

    public Guid MatchId { get; }
}