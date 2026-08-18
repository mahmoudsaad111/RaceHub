using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaceHub.Infrastructure.Migrations
{
    /// <summary>
    /// Adds PlayerStatistics.RatingPoints. Hand-written rather than
    /// scaffolded: the model snapshot already includes the column (the
    /// AddUserAchievements generation brought the snapshot back in sync
    /// after the earlier hand-written migrations had drifted from it), so
    /// `dotnet ef migrations add` sees no diff — but databases migrated
    /// with the old hand-written AddMessagingAndStatsTables never got the
    /// column. Default 1000 matches PlayerStatistics' DefaultRating.
    /// </summary>
    public partial class AddPlayerStatisticsRatingPoints : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RatingPoints",
                table: "PlayerStatistics",
                type: "int",
                nullable: false,
                defaultValue: 1000);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RatingPoints",
                table: "PlayerStatistics");
        }
    }
}
