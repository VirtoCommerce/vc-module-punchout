using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtoCommerce.Punchout.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddPunchoutOrderRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PunchoutOrderRequest",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ConfigurationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    StoreId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    PayloadId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    SenderIdentity = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    BuyerIdentity = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    OrderType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    PurchaseOrderNumber = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PurchaseOrderDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Total = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    CustomerOrderId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CartId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Cxml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PunchoutOrderRequest", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PunchoutOrderRequest_ConfigurationId_PayloadId",
                table: "PunchoutOrderRequest",
                columns: new[] { "ConfigurationId", "PayloadId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PunchoutOrderRequest_ConfigurationId_PurchaseOrderNumber",
                table: "PunchoutOrderRequest",
                columns: new[] { "ConfigurationId", "PurchaseOrderNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_PunchoutOrderRequest_CustomerOrderId",
                table: "PunchoutOrderRequest",
                column: "CustomerOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PunchoutOrderRequest");
        }
    }
}
