using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PesajeFaeneo.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class F3_Balanza : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ConfiguracionBalanza_LecturasParaEstable",
                table: "ConfiguracionesBalanza");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConfiguracionBalanza_TimeoutMs",
                table: "ConfiguracionesBalanza");

            migrationBuilder.UpdateData(
                table: "ConfiguracionesBalanza",
                keyColumn: "Id",
                keyValue: 1,
                column: "PatronRegex",
                value: "^[^-+\\d]*(?<peso>[-+]?\\s*\\d+(?:[.,]\\d+)?)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConfiguracionBalanza_BaudRate",
                table: "ConfiguracionesBalanza",
                sql: "BaudRate BETWEEN 110 AND 921600");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConfiguracionBalanza_BitsParada",
                table: "ConfiguracionesBalanza",
                sql: "BitsParada IN (1, 2, 3)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConfiguracionBalanza_DataBits",
                table: "ConfiguracionesBalanza",
                sql: "DataBits BETWEEN 5 AND 8");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConfiguracionBalanza_HostTcp",
                table: "ConfiguracionesBalanza",
                sql: "length(HostTcp) BETWEEN 1 AND 100");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConfiguracionBalanza_LecturasParaEstable",
                table: "ConfiguracionesBalanza",
                sql: "LecturasParaEstable BETWEEN 1 AND 50");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConfiguracionBalanza_Paridad",
                table: "ConfiguracionesBalanza",
                sql: "Paridad IN (0, 1, 2, 3, 4)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConfiguracionBalanza_PatronRegex",
                table: "ConfiguracionesBalanza",
                sql: "length(PatronRegex) BETWEEN 1 AND 250");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConfiguracionBalanza_PuertoCom",
                table: "ConfiguracionesBalanza",
                sql: "length(PuertoCom) BETWEEN 1 AND 20");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConfiguracionBalanza_PuertoTcp",
                table: "ConfiguracionesBalanza",
                sql: "PuertoTcp BETWEEN 1 AND 65535");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConfiguracionBalanza_TimeoutMs",
                table: "ConfiguracionesBalanza",
                sql: "TimeoutMs BETWEEN 500 AND 60000");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConfiguracionBalanza_ToleranciaKg",
                table: "ConfiguracionesBalanza",
                sql: "ToleranciaKg >= 0 AND ToleranciaKg <= 10");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ConfiguracionBalanza_BaudRate",
                table: "ConfiguracionesBalanza");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConfiguracionBalanza_BitsParada",
                table: "ConfiguracionesBalanza");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConfiguracionBalanza_DataBits",
                table: "ConfiguracionesBalanza");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConfiguracionBalanza_HostTcp",
                table: "ConfiguracionesBalanza");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConfiguracionBalanza_LecturasParaEstable",
                table: "ConfiguracionesBalanza");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConfiguracionBalanza_Paridad",
                table: "ConfiguracionesBalanza");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConfiguracionBalanza_PatronRegex",
                table: "ConfiguracionesBalanza");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConfiguracionBalanza_PuertoCom",
                table: "ConfiguracionesBalanza");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConfiguracionBalanza_PuertoTcp",
                table: "ConfiguracionesBalanza");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConfiguracionBalanza_TimeoutMs",
                table: "ConfiguracionesBalanza");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConfiguracionBalanza_ToleranciaKg",
                table: "ConfiguracionesBalanza");

            migrationBuilder.UpdateData(
                table: "ConfiguracionesBalanza",
                keyColumn: "Id",
                keyValue: 1,
                column: "PatronRegex",
                value: "(?<peso>[-+]?\\d+(?:[.,]\\d+)?)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConfiguracionBalanza_LecturasParaEstable",
                table: "ConfiguracionesBalanza",
                sql: "LecturasParaEstable >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConfiguracionBalanza_TimeoutMs",
                table: "ConfiguracionesBalanza",
                sql: "TimeoutMs > 0");
        }
    }
}
