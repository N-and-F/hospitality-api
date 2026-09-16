using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hospitality.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class FeloganSettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AdditionalProfit",
                table: "FeloganServings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ExcessPaid",
                table: "FeloganServings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PaidCook",
                table: "FeloganServings",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "no");

            migrationBuilder.AddColumn<string>(
                name: "PaidSham",
                table: "FeloganServings",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "na");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdditionalProfit",
                table: "FeloganServings");

            migrationBuilder.DropColumn(
                name: "ExcessPaid",
                table: "FeloganServings");

            migrationBuilder.DropColumn(
                name: "PaidCook",
                table: "FeloganServings");

            migrationBuilder.DropColumn(
                name: "PaidSham",
                table: "FeloganServings");
        }
    }
}
