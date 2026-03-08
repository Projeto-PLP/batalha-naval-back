using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BatalhaNaval.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "campaign_stage",
                table: "matches",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_moved_this_turn",
                table: "matches",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_campaign_match",
                table: "matches",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "player1_consecutive_hits",
                table: "matches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "player1_misses",
                table: "matches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "player2_consecutive_hits",
                table: "matches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "player2_misses",
                table: "matches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "campaign_progress",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_stage = table.Column<int>(type: "integer", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaign_progress", x => x.id);
                    table.ForeignKey(
                        name: "FK_campaign_progress_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_campaign_progress_user_id",
                table: "campaign_progress",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "campaign_progress");

            migrationBuilder.DropColumn(
                name: "campaign_stage",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "has_moved_this_turn",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "is_campaign_match",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "player1_consecutive_hits",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "player1_misses",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "player2_consecutive_hits",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "player2_misses",
                table: "matches");
        }
    }
}
