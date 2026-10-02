using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HobbiesClub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberServiceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Rank",
                table: "Members",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RollRegNo",
                table: "Members",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "Members",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Rank",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "RollRegNo",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "Members");
        }
    }
}
