using BatalhaNaval.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Newtonsoft.Json;

namespace BatalhaNaval.Infrastructure.Persistence.Configurations;

public class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> builder)
    {
        builder.ToTable("matches");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.Player1Id).HasColumnName("player1_id");
        builder.Property(m => m.Player2Id).HasColumnName("player2_id");
        builder.Property(m => m.WinnerId).HasColumnName("winner_id");
        builder.Property(m => m.StartedAt).HasColumnName("started_at");
        builder.Property(m => m.LastMoveAt).HasColumnName("last_move_at");
        builder.Property(m => m.CurrentTurnPlayerId).HasColumnName("current_turn_player_id");

        builder.Property(m => m.Mode)
            .HasConversion<string>()
            .HasColumnName("game_mode");

        builder.Property(m => m.AiDifficulty)
            .HasConversion<string>()
            .HasColumnName("ai_difficulty");

        builder.Property(m => m.Status)
            .HasConversion<string>()
            .HasColumnName("status");

        // --- CONVERSORES PARA BOARD (JSONB) ---

        // Conversor explícito
        var boardConverter = new ValueConverter<Board, string>(
            v => JsonConvert.SerializeObject(v),
            v => JsonConvert.DeserializeObject<Board>(v) ?? new Board()
        );

        // O Comparer do Board é mais chato (objeto complexo). 
        // Vamos forçar a serialização para comparar se mudou. É custoso mas seguro.
        var boardComparer = new ValueComparer<Board>(
            (c1, c2) => JsonConvert.SerializeObject(c1) == JsonConvert.SerializeObject(c2),
            c => JsonConvert.SerializeObject(c).GetHashCode(),
            c => JsonConvert.DeserializeObject<Board>(JsonConvert.SerializeObject(c))!
        );

        builder.Property(m => m.Player1Board)
            .HasColumnName("player1_board_json")
            .HasColumnType("jsonb")
            .HasConversion(boardConverter)
            .Metadata.SetValueComparer(boardComparer);

        builder.Property(m => m.Player2Board)
            .HasColumnName("player2_board_json")
            .HasColumnType("jsonb")
            .HasConversion(boardConverter)
            .Metadata.SetValueComparer(boardComparer);

        builder.Ignore(m => m.IsFinished);

        builder.Property(m => m.Player1Hits)
            .HasColumnName("player1_hits")
            .IsRequired();

        builder.Property(m => m.Player2Hits)
            .HasColumnName("player2_hits")
            .IsRequired();
    }
}