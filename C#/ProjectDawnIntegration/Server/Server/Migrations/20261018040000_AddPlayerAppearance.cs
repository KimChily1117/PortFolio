using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Server.DB;
namespace Server.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20261018040000_AddPlayerAppearance")]
    public sealed class AddPlayerAppearance : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.AddColumn<string>("Appearance", "Player", type: "nvarchar(512)", maxLength: 512, nullable: true);
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.DropColumn("Appearance", "Player");
    }
}
