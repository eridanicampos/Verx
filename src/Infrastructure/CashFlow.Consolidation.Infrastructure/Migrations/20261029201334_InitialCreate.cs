using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CashFlow.Consolidation.Infrastructure.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "consolidation");

            migrationBuilder.CreateTable(
                name: "accounts",
                schema: "consolidation",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_entry_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_accounts", x => x.account_id);
                });

            migrationBuilder.CreateTable(
                name: "daily_balances",
                schema: "consolidation",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    total_credits = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    total_debits = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    entry_count = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_daily_balances", x => new { x.account_id, x.date });
                });

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "consolidation",
                columns: table => new
                {
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_type = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inbox_messages", x => x.message_id);
                });

            migrationBuilder.CreateTable(
                name: "ledger_entries",
                schema: "consolidation",
                columns: table => new
                {
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    signed_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    business_date = table.Column<DateOnly>(type: "date", nullable: false),
                    projected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ledger_entries", x => x.transaction_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_inbox_messages_processed_at",
                schema: "consolidation",
                table: "inbox_messages",
                column: "processed_at");

            migrationBuilder.CreateIndex(
                name: "ix_ledger_entries_account_id_business_date_occurred_at",
                schema: "consolidation",
                table: "ledger_entries",
                columns: new[] { "account_id", "business_date", "occurred_at" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accounts",
                schema: "consolidation");

            migrationBuilder.DropTable(
                name: "daily_balances",
                schema: "consolidation");

            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "consolidation");

            migrationBuilder.DropTable(
                name: "ledger_entries",
                schema: "consolidation");
        }
    }
}
