namespace BatalhaNaval.Domain.Exceptions;

/// <summary>
///     Lançada quando o jogador tenta iniciar uma partida de campanha
///     após já ter concluído todos os estágios.
/// </summary>
public class CampaignAlreadyCompletedException : Exception
{
    public CampaignAlreadyCompletedException()
        : base("Você já concluiu o modo campanha! Toda a frota inimiga foi destruída, Almirante.")
    {
    }
}

