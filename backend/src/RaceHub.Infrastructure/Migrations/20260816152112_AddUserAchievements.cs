using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaceHub.Infrastructure.Migrations
{
    /// <summary>
    /// Adds the UserAchievements table (one-time badge unlocks, written by
    /// RaceHub.AchievementsWorker). Only creates this table: the outbox/
    /// stats/history/ownership tables were already created by
    /// AddMessagingAndStatsTables and AddCarPriceAndOwnership, which were
    /// hand-written without a model-snapshot update — this migration's
    /// Designer/snapshot files bring the snapshot back in sync with the
    /// full model, so `dotnet ef migrations add` won't try to re-create
    /// those tables again in future migrations.
    /// </summary>
    public partial class AddUserAchievements : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserAchievements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UnlockedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAchievements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAchievements_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_UserId_Key",
                table: "UserAchievements",
                columns: new[] { "UserId", "Key" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserAchievements");
        }
    }
}
