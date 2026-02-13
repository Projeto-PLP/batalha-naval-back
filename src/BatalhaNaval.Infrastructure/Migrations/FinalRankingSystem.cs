#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace BatalhaNaval.Infrastructure.Migrations;

/// <inheritdoc />
public partial class FinalRankingSystem : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "matches",
            table => new
            {
                id = table.Column<Guid>("uuid", nullable: false),
                player1_hits = table.Column<int>("integer", nullable: false),
                player2_hits = table.Column<int>("integer", nullable: false),
                player1_id = table.Column<Guid>("uuid", nullable: false),
                player2_id = table.Column<Guid>("uuid", nullable: true),
                player1_board_json = table.Column<string>("jsonb", nullable: false),
                player2_board_json = table.Column<string>("jsonb", nullable: false),
                game_mode = table.Column<string>("text", nullable: false),
                ai_difficulty = table.Column<string>("text", nullable: true),
                status = table.Column<string>("text", nullable: false),
                finished_at = table.Column<DateTime>("timestamp with time zone", nullable: true),
                current_turn_player_id = table.Column<Guid>("uuid", nullable: false),
                winner_id = table.Column<Guid>("uuid", nullable: true),
                started_at = table.Column<DateTime>("timestamp with time zone", nullable: false),
                last_move_at = table.Column<DateTime>("timestamp with time zone", nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_matches", x => x.id); });

        migrationBuilder.CreateTable(
            "Medals",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>("text", nullable: false),
                Description = table.Column<string>("text", nullable: false),
                Code = table.Column<string>("text", nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_Medals", x => x.Id); });

        migrationBuilder.CreateTable(
            "users",
            table => new
            {
                id = table.Column<Guid>("uuid", nullable: false),
                username = table.Column<string>("text", nullable: false),
                password_hash = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTime>("timestamp with time zone", nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_users", x => x.id); });

        migrationBuilder.CreateTable(
            "player_profiles",
            table => new
            {
                user_id = table.Column<Guid>("uuid", nullable: false),
                rank_points = table.Column<int>("integer", nullable: false),
                wins = table.Column<int>("integer", nullable: false),
                losses = table.Column<int>("integer", nullable: false),
                current_streak = table.Column<int>("integer", nullable: false),
                max_streak = table.Column<int>("integer", nullable: false),
                updated_at = table.Column<DateTime>("timestamp with time zone", nullable: false),
                medals_json = table.Column<string>("jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_player_profiles", x => x.user_id);
                table.ForeignKey(
                    "FK_player_profiles_users_user_id",
                    x => x.user_id,
                    "users",
                    "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "UserMedals",
            table => new
            {
                UserId = table.Column<Guid>("uuid", nullable: false),
                MedalId = table.Column<int>("integer", nullable: false),
                EarnedAt = table.Column<DateTime>("timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserMedals", x => new { x.UserId, x.MedalId });
                table.ForeignKey(
                    "FK_UserMedals_Medals_MedalId",
                    x => x.MedalId,
                    "Medals",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    "FK_UserMedals_users_UserId",
                    x => x.UserId,
                    "users",
                    "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            "IX_UserMedals_MedalId",
            "UserMedals",
            "MedalId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            "matches");

        migrationBuilder.DropTable(
            "player_profiles");

        migrationBuilder.DropTable(
            "UserMedals");

        migrationBuilder.DropTable(
            "Medals");

        migrationBuilder.DropTable(
            "users");
    }
}