using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brigade.Admin.Data.PostgreSQL.Migrations.AgentDb
{
    /// <inheritdoc />
    public partial class AddMiddlewares : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgentMiddleware_MiddlewareOptions_MiddlewareId",
                table: "AgentMiddleware");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MiddlewareOptions",
                table: "MiddlewareOptions");

            migrationBuilder.RenameTable(
                name: "MiddlewareOptions",
                newName: "Middlewares");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Middlewares",
                table: "Middlewares",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AgentMiddleware_Middlewares_MiddlewareId",
                table: "AgentMiddleware",
                column: "MiddlewareId",
                principalTable: "Middlewares",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgentMiddleware_Middlewares_MiddlewareId",
                table: "AgentMiddleware");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Middlewares",
                table: "Middlewares");

            migrationBuilder.RenameTable(
                name: "Middlewares",
                newName: "MiddlewareOptions");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MiddlewareOptions",
                table: "MiddlewareOptions",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AgentMiddleware_MiddlewareOptions_MiddlewareId",
                table: "AgentMiddleware",
                column: "MiddlewareId",
                principalTable: "MiddlewareOptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
