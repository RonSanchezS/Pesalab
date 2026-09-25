using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PesajeFaeneo.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class F3_DelimitadorSignoIgual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ConfiguracionBalanza_DelimitadorTrama",
                table: "ConfiguracionesBalanza");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConfiguracionBalanza_DelimitadorTrama",
                table: "ConfiguracionesBalanza",
                sql: "DelimitadorTrama IN (1, 2, 3)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ConfiguracionBalanza_DelimitadorTrama",
                table: "ConfiguracionesBalanza");

            // SignoIgual (3) no existe en la versión anterior; su equivalente más cercano es Espacio (2).
            migrationBuilder.Sql("UPDATE ConfiguracionesBalanza SET DelimitadorTrama = 2 WHERE DelimitadorTrama = 3;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConfiguracionBalanza_DelimitadorTrama",
                table: "ConfiguracionesBalanza",
                sql: "DelimitadorTrama IN (1, 2)");
        }
    }
}
