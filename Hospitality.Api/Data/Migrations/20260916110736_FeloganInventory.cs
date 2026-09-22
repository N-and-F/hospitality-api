using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hospitality.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class FeloganInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OpeningPacks",
                table: "FeloganMenuItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "FeloganPurchases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchasedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    MenuItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Qty = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeloganPurchases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeloganPurchases_FeloganMenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "FeloganMenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeloganPurchases_ClientRequestId",
                table: "FeloganPurchases",
                column: "ClientRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeloganPurchases_MenuItemId",
                table: "FeloganPurchases",
                column: "MenuItemId");

            migrationBuilder.CreateIndex(
                name: "IX_FeloganPurchases_PurchasedOn",
                table: "FeloganPurchases",
                column: "PurchasedOn");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeloganPurchases");

            migrationBuilder.DropColumn(
                name: "OpeningPacks",
                table: "FeloganMenuItems");
        }
    }
}
