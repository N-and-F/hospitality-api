using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hospitality.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class FeloganCookPercent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CookPercent",
                table: "FeloganServings",
                type: "integer",
                nullable: false,
                defaultValue: 40);

            migrationBuilder.CreateTable(
                name: "FeloganSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CookPercent = table.Column<int>(type: "integer", nullable: false, defaultValue: 40)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeloganSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeloganSettings");

            migrationBuilder.DropColumn(
                name: "CookPercent",
                table: "FeloganServings");
        }
    }
}
