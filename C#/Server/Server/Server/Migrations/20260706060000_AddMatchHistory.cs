using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Server.Migrations
{
    public partial class AddMatchHistory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchHistory",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartyId = table.Column<int>(nullable: false),
                    QueueKey = table.Column<string>(nullable: true),
                    TargetRoomType = table.Column<string>(nullable: true),
                    TargetRoomId = table.Column<int>(nullable: true),
                    TransferId = table.Column<int>(nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(nullable: false),
                    TransferStartedAtUtc = table.Column<DateTime>(nullable: true),
                    DungeonEnteredAtUtc = table.Column<DateTime>(nullable: true),
                    ResultStatus = table.Column<string>(nullable: true),
                    FailureReason = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchHistoryMember",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchHistoryId = table.Column<int>(nullable: false),
                    PlayerId = table.Column<int>(nullable: false),
                    PlayerName = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchHistoryMember", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchHistoryMember_MatchHistory_MatchHistoryId",
                        column: x => x.MatchHistoryId,
                        principalTable: "MatchHistory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchHistory_PartyId",
                table: "MatchHistory",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchHistoryMember_MatchHistoryId",
                table: "MatchHistoryMember",
                column: "MatchHistoryId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchHistoryMember");

            migrationBuilder.DropTable(
                name: "MatchHistory");
        }
    }
}