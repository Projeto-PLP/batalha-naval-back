using System.Text.Json;
using BatalhaNaval.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

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
        builder.Property(m => m.StartedAt).HasColumnName("started_at").IsRequired();
        builder.Property(m => m.LastMoveAt).HasColumnName("last_move_at").IsRequired();
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
        
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        // Conversor explícito
        var boardConverter = new ValueConverter<Board, string>(
            v => JsonSerializer.Serialize(v, jsonOptions),
            v => JsonSerializer.Deserialize<Board>(v, jsonOptions) ?? new Board()
        );

        var boardComparer = new ValueComparer<Board>(
            (c1, c2) => JsonSerializer.Serialize(c1, jsonOptions) == JsonSerializer.Serialize(c2, jsonOptions),
            c => JsonSerializer.Serialize(c, jsonOptions).GetHashCode(),
            c => JsonSerializer.Deserialize<Board>(JsonSerializer.Serialize(c, jsonOptions), jsonOptions)!
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
        
        builder.Property(m => m.Player1Misses)
            .HasColumnName("player1_misses")
            .IsRequired();    
        
        builder.Property(m => m.Player2Misses)
            .HasColumnName("player2_misses")
            .IsRequired();
        
        builder.Property(m => m.HasMovedThisTurn)
            .HasColumnName("has_moved_this_turn");

    }
}