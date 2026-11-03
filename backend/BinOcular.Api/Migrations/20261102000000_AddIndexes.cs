using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BinOcular.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Analyses_CreatedAtUtc",
                table: "Analyses",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Analyses_Status",
                table: "Analyses",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ToolResults_AnalysisRecordId",
                table: "ToolResults",
                column: "AnalysisRecordId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Analyses_CreatedAtUtc",
                table: "Analyses");

            migrationBuilder.DropIndex(
                name: "IX_Analyses_Status",
                table: "Analyses");

            migrationBuilder.DropIndex(
                name: "IX_ToolResults_AnalysisRecordId",
                table: "ToolResults");
        }
    }
}
