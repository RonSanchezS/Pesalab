using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PesajeFaeneo.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class F11_ModoDesposte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ModoDesposte",
                table: "ParametrosSistema",
                type: "INTEGER",
                nullable: true);

            // F11 (D2): las instalaciones ya puestas en marcha conservan el comportamiento anterior (Ambos) de forma
            // explicita; en las nuevas queda sin definir (null) y el asistente de puesta en marcha pide elegirlo.
            migrationBuilder.Sql("UPDATE ParametrosSistema SET ModoDesposte = 3 WHERE ConfiguracionInicialCompleta = 1;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ParametrosSistema_ModoDesposte",
                table: "ParametrosSistema",
                sql: "ModoDesposte IS NULL OR ModoDesposte IN (1, 2, 3)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ParametrosSistema_ModoDesposte",
                table: "ParametrosSistema");

            migrationBuilder.DropColumn(
                name: "ModoDesposte",
                table: "ParametrosSistema");
        }
    }
}
