using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PesajeFaeneo.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class F2_Catalogos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Ganaderos_Marca",
                table: "Ganaderos",
                sql: "length(Marca) BETWEEN 1 AND 5");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Clientes_Codigo",
                table: "Clientes",
                sql: "Codigo BETWEEN 1 AND 999999");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Ganaderos_Marca",
                table: "Ganaderos");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Clientes_Codigo",
                table: "Clientes");
        }
    }
}
