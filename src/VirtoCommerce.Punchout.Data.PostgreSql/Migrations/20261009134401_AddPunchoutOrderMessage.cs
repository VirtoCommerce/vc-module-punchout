using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtoCommerce.Punchout.Data.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddPunchoutOrderMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConfigurationId",
                table: "PunchoutSession",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierDomain",
                table: "PunchoutSession",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierIdentity",
                table: "PunchoutSession",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PunchoutOrderMessage",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SessionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Cxml = table.Column<string>(type: "text", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PunchoutOrderMessage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PunchoutOrderMessage_PunchoutSession_SessionId",
                        column: x => x.SessionId,
                        principalTable: "PunchoutSession",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PunchoutOrderMessage_SessionId",
                table: "PunchoutOrderMessage",
                column: "SessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PunchoutOrderMessage");

            migrationBuilder.DropColumn(
                name: "ConfigurationId",
                table: "PunchoutSession");

            migrationBuilder.DropColumn(
                name: "SupplierDomain",
                table: "PunchoutSession");

            migrationBuilder.DropColumn(
                name: "SupplierIdentity",
                table: "PunchoutSession");
        }
    }
}
