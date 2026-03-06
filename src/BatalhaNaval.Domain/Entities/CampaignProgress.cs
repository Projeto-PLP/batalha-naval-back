using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BatalhaNaval.Domain.Enums;

namespace BatalhaNaval.Domain.Entities;

[Table("campaign_progress")]
public class CampaignProgress
{
    [Key]
    [Column("id")]
    [Description("Identificador único do progresso de campanha")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("user_id")]
    [Description("Identificador do jogador dono deste progresso")]
    public Guid UserId { get; set; }

    [Description("Usuário associado ao progresso")]
    public virtual User? User { get; set; }

    [Column("current_stage")]
    [Description("Estágio atual da campanha")]
    public CampaignStage CurrentStage { get; set; } = CampaignStage.Stage1Basic;

    [Column("completed_at")]
    [Description("Data e hora em que a campanha foi concluída (null se ainda não concluída)")]
    public DateTime? CompletedAt { get; set; }

    [Column("updated_at")]
    [Description("Data e hora da última atualização")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // ====================================================================
    // HELPERS DE NEGÓCIO
    // ====================================================================

    /// <summary>Indica se a campanha foi concluída.</summary>
    [NotMapped]
    public bool IsCompleted => CurrentStage == CampaignStage.Completed;

    /// <summary>Retorna a dificuldade da IA correspondente ao estágio atual.</summary>
    [NotMapped]
    public Difficulty? CurrentStageDifficulty => CurrentStage switch
    {
        CampaignStage.Stage1Basic        => Difficulty.Basic,
        CampaignStage.Stage2Intermediate => Difficulty.Intermediate,
        CampaignStage.Stage3Advanced     => Difficulty.Advanced,
        _                                => null
    };

    /// <summary>
    ///     Avança a campanha para o próximo estágio após uma vitória.
    ///     Retorna true se a campanha acabou de ser concluída nesta chamada.
    /// </summary>
    public bool AdvanceStage()
    {
        if (IsCompleted) return false;

        CurrentStage = CurrentStage switch
        {
            CampaignStage.Stage1Basic        => CampaignStage.Stage2Intermediate,
            CampaignStage.Stage2Intermediate => CampaignStage.Stage3Advanced,
            CampaignStage.Stage3Advanced     => CampaignStage.Completed,
            _                                => CurrentStage
        };

        UpdatedAt = DateTime.UtcNow;

        if (CurrentStage == CampaignStage.Completed)
        {
            CompletedAt = DateTime.UtcNow;
            return true;
        }

        return false;
    }
}

