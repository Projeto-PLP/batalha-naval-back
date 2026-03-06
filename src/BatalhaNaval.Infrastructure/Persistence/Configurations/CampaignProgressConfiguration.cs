using BatalhaNaval.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BatalhaNaval.Infrastructure.Persistence.Configurations;

public class CampaignProgressConfiguration : IEntityTypeConfiguration<CampaignProgress>
{
    public void Configure(EntityTypeBuilder<CampaignProgress> builder)
    {
        builder.ToTable("campaign_progress");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");

        builder.Property(c => c.UserId).HasColumnName("user_id").IsRequired();

        builder.Property(c => c.CurrentStage)
            .HasConversion<string>()
            .HasColumnName("current_stage")
            .IsRequired();

        builder.Property(c => c.CompletedAt).HasColumnName("completed_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at").IsRequired();

        // Garante um registro único por jogador
        builder.HasIndex(c => c.UserId).IsUnique();

        // Relacionamento com User
        builder.HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ignorar propriedades calculadas (NotMapped)
        builder.Ignore(c => c.IsCompleted);
        builder.Ignore(c => c.CurrentStageDifficulty);
    }
}

