using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hospitality.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Felogan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeloganCooks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeloganCooks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeloganMenuItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SellPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeloganMenuItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeloganRooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeloganRooms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeloganServings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    CookId = table.Column<Guid>(type: "uuid", nullable: false),
                    Note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeloganServings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeloganServings_FeloganCooks_CookId",
                        column: x => x.CookId,
                        principalTable: "FeloganCooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeloganServings_FeloganRooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "FeloganRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FeloganServingLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServingId = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SnapshotKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SnapshotSellPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SnapshotCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Qty = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeloganServingLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeloganServingLines_FeloganMenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "FeloganMenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeloganServingLines_FeloganServings_ServingId",
                        column: x => x.ServingId,
                        principalTable: "FeloganServings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeloganServingLines_MenuItemId",
                table: "FeloganServingLines",
                column: "MenuItemId");

            migrationBuilder.CreateIndex(
                name: "IX_FeloganServingLines_ServingId",
                table: "FeloganServingLines",
                column: "ServingId");

            migrationBuilder.CreateIndex(
                name: "IX_FeloganServings_ClientRequestId",
                table: "FeloganServings",
                column: "ClientRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeloganServings_CookId",
                table: "FeloganServings",
                column: "CookId");

            migrationBuilder.CreateIndex(
                name: "IX_FeloganServings_RoomId",
                table: "FeloganServings",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_FeloganServings_ServedOn",
                table: "FeloganServings",
                column: "ServedOn");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeloganServingLines");

            migrationBuilder.DropTable(
                name: "FeloganMenuItems");

            migrationBuilder.DropTable(
                name: "FeloganServings");

            migrationBuilder.DropTable(
                name: "FeloganCooks");

            migrationBuilder.DropTable(
                name: "FeloganRooms");
        }
    }
}
