using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hospitality.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class NamedWallets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Wallets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsMain = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wallets", x => x.Id);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO "Wallets" ("Id", "Name", "IsMain", "CreatedAt")
                SELECT gen_random_uuid(), 'Main', TRUE, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM "Wallets" WHERE "IsMain");
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "WalletId",
                table: "WalletOpenings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WalletId",
                table: "LedgerTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "WalletOpenings"
                SET "WalletId" = (SELECT "Id" FROM "Wallets" WHERE "IsMain" LIMIT 1)
                WHERE "WalletId" IS NULL;

                UPDATE "LedgerTransactions"
                SET "WalletId" = (SELECT "Id" FROM "Wallets" WHERE "IsMain" LIMIT 1)
                WHERE "WalletId" IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "WalletId",
                table: "WalletOpenings",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "WalletId",
                table: "LedgerTransactions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "StaffWalletAssignments",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffWalletAssignments", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_StaffWalletAssignments_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO "StaffWalletAssignments" ("UserId", "WalletId")
                SELECT ur."UserId", (SELECT "Id" FROM "Wallets" WHERE "IsMain" LIMIT 1)
                FROM "AspNetUserRoles" ur
                INNER JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
                WHERE r."Name" = 'Staff'
                ON CONFLICT ("UserId") DO NOTHING;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_WalletOpenings_WalletId",
                table: "WalletOpenings",
                column: "WalletId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTransactions_WalletId",
                table: "LedgerTransactions",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffWalletAssignments_WalletId",
                table: "StaffWalletAssignments",
                column: "WalletId");

            migrationBuilder.AddForeignKey(
                name: "FK_LedgerTransactions_Wallets_WalletId",
                table: "LedgerTransactions",
                column: "WalletId",
                principalTable: "Wallets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WalletOpenings_Wallets_WalletId",
                table: "WalletOpenings",
                column: "WalletId",
                principalTable: "Wallets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LedgerTransactions_Wallets_WalletId",
                table: "LedgerTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_WalletOpenings_Wallets_WalletId",
                table: "WalletOpenings");

            migrationBuilder.DropTable(
                name: "StaffWalletAssignments");

            migrationBuilder.DropTable(
                name: "Wallets");

            migrationBuilder.DropIndex(
                name: "IX_WalletOpenings_WalletId",
                table: "WalletOpenings");

            migrationBuilder.DropIndex(
                name: "IX_LedgerTransactions_WalletId",
                table: "LedgerTransactions");

            migrationBuilder.DropColumn(
                name: "WalletId",
                table: "WalletOpenings");

            migrationBuilder.DropColumn(
                name: "WalletId",
                table: "LedgerTransactions");
        }
    }
}
