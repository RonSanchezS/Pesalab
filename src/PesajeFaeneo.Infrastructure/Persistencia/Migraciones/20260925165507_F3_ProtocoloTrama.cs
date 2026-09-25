using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PesajeFaeneo.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class F3_ProtocoloTrama : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DelimitadorTrama",
                table: "ConfiguracionesBalanza",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "UnidadPeso",
                table: "ConfiguracionesBalanza",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.UpdateData(
                table: "ConfiguracionesBalanza",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "DelimitadorTrama", "UnidadPeso" },
                values: new object[] { 1, 1 });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConfiguracionBalanza_DelimitadorTrama",
                table: "ConfiguracionesBalanza",
                sql: "DelimitadorTrama IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConfiguracionBalanza_UnidadPeso",
                table: "ConfiguracionesBalanza",
                sql: "UnidadPeso IN (1, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ConfiguracionBalanza_DelimitadorTrama",
                table: "ConfiguracionesBalanza");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConfiguracionBalanza_UnidadPeso",
                table: "ConfiguracionesBalanza");

            migrationBuilder.DropColumn(
                name: "DelimitadorTrama",
                table: "ConfiguracionesBalanza");

            migrationBuilder.DropColumn(
                name: "UnidadPeso",
                table: "ConfiguracionesBalanza");
        }
    }
}
