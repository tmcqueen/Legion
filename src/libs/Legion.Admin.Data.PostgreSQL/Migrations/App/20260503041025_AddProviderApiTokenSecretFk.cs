using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Legion.Admin.Data.PostgreSQL.Migrations.App
{
    /// <inheritdoc />
    public partial class AddProviderApiTokenSecretFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApiToken",
                schema: "agents",
                table: "Providers");

            migrationBuilder.AddColumn<Guid>(
                name: "ApiTokenSecretId",
                schema: "agents",
                table: "Providers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Providers_ApiTokenSecretId",
                schema: "agents",
                table: "Providers",
                column: "ApiTokenSecretId");

            migrationBuilder.AddForeignKey(
                name: "FK_Providers_Secrets_ApiTokenSecretId",
                schema: "agents",
                table: "Providers",
                column: "ApiTokenSecretId",
                principalTable: "Secrets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Providers_Secrets_ApiTokenSecretId",
                schema: "agents",
                table: "Providers");

            migrationBuilder.DropIndex(
                name: "IX_Providers_ApiTokenSecretId",
                schema: "agents",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "ApiTokenSecretId",
                schema: "agents",
                table: "Providers");

            migrationBuilder.AddColumn<string>(
                name: "ApiToken",
                schema: "agents",
                table: "Providers",
                type: "text",
                nullable: true);
        }
    }
}
