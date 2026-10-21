using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaBooking.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class PaymentEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GatewayReceiptJson",
                table: "Payments",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReceiptReceivedAtUtc",
                table: "Payments",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GatewayReceiptJson",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ReceiptReceivedAtUtc",
                table: "Payments");
        }
    }
}
