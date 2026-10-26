using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BinOcular.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFormattedOutputJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FormattedOutputJson",
                table: "ToolResults",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FormattedOutputJson",
                table: "ToolResults");
        }
    }
}
