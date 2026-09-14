using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtoCommerce.Punchout.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddPunchoutIntegrationOrganization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PunchoutIntegrationOrganization",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OrganizationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IntegrationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PunchoutIntegrationOrganization", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PunchoutIntegrationOrganization_PunchoutIntegration_IntegrationId",
                        column: x => x.IntegrationId,
                        principalTable: "PunchoutIntegration",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PunchoutIntegrationOrganization_IntegrationId",
                table: "PunchoutIntegrationOrganization",
                column: "IntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_PunchoutIntegrationOrganization_OrganizationId_IntegrationId",
                table: "PunchoutIntegrationOrganization",
                columns: new[] { "OrganizationId", "IntegrationId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PunchoutIntegrationOrganization");
        }
    }
}
