using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtoCommerce.Punchout.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddInitialPunchoutEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PunchoutIntegration",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    StoreId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CredentialDomain = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SenderIdentity = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    OrganizationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SharedSecretHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AllowedReturnUrls = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PunchoutIntegration", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PunchoutSession",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    StoreId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CartId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SessionToken = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    BuyerCookie = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    BuyerIdentity = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    BuyerDomain = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ReturnUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StartPage = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    IntegrationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PunchoutSession", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PunchoutIntegration_OrganizationId",
                table: "PunchoutIntegration",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_PunchoutSession_SessionToken",
                table: "PunchoutSession",
                column: "SessionToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PunchoutIntegration");

            migrationBuilder.DropTable(
                name: "PunchoutSession");
        }
    }
}
