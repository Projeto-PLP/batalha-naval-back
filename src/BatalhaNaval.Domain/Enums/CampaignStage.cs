namespace BatalhaNaval.Domain.Enums;

/// <summary>
///     Representa o estágio atual da campanha do jogador.
///     A progressão é linear: Stage1Basic → Stage2Intermediate → Stage3Advanced → Completed.
/// </summary>
public enum CampaignStage
{
    /// <summary>Primeiro estágio — deve vencer a IA Básica.</summary>
    Stage1Basic = 1,

    /// <summary>Segundo estágio — deve vencer a IA Intermediária.</summary>
    Stage2Intermediate = 2,

    /// <summary>Terceiro e último estágio — deve vencer a IA Avançada.</summary>
    Stage3Advanced = 3,

    /// <summary>Campanha concluída com sucesso.</summary>
    Completed = 4
}

