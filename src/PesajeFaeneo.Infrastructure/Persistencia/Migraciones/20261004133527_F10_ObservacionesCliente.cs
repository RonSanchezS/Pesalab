using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PesajeFaeneo.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class F10_ObservacionesCliente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // F10 (O5/O6): el código del fletero pasa a ser único entre TODOS (antes solo entre los activos: un inactivo
            // podía repetir el orden de otro) y el orden F1..F4 deja de acotarse a 1..4. Antes de crear el índice único
            // se renumeran los inactivos que choquen (el de menor Id y los activos conservan su valor). La CHECK vieja
            // (Orden BETWEEN 1 AND 4) sigue activa en este punto y los valores nuevos la superan: se ignora solo durante
            // este UPDATE.
            migrationBuilder.Sql("PRAGMA ignore_check_constraints = ON;");
            migrationBuilder.Sql(
                "UPDATE Fleteros SET Orden = (SELECT MAX(Orden) FROM Fleteros) + Id " +
                "WHERE Activo = 0 AND EXISTS (SELECT 1 FROM Fleteros o WHERE o.Orden = Fleteros.Orden AND o.Id <> Fleteros.Id " +
                "AND (o.Activo = 1 OR o.Id < Fleteros.Id));");
            migrationBuilder.Sql("PRAGMA ignore_check_constraints = OFF;");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Ganaderos_Marca",
                table: "Ganaderos");

            migrationBuilder.DropIndex(
                name: "IX_Fleteros_Orden",
                table: "Fleteros");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Fleteros_Orden",
                table: "Fleteros");

            migrationBuilder.RenameColumn(
                name: "Marca",
                table: "Ganaderos",
                newName: "Codigo");

            migrationBuilder.RenameIndex(
                name: "IX_Ganaderos_Marca",
                table: "Ganaderos",
                newName: "IX_Ganaderos_Codigo");

            migrationBuilder.RenameColumn(
                name: "Orden",
                table: "Fleteros",
                newName: "Codigo");

            migrationBuilder.AddColumn<int>(
                name: "FleteroId",
                table: "Pesajes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pesajes_FleteroId",
                table: "Pesajes",
                column: "FleteroId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Ganaderos_Codigo",
                table: "Ganaderos",
                sql: "length(Codigo) BETWEEN 1 AND 5");

            migrationBuilder.CreateIndex(
                name: "IX_Fleteros_Codigo",
                table: "Fleteros",
                column: "Codigo",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Fleteros_Codigo",
                table: "Fleteros",
                sql: "Codigo >= 1");

            migrationBuilder.AddForeignKey(
                name: "FK_Pesajes_Fleteros_FleteroId",
                table: "Pesajes",
                column: "FleteroId",
                principalTable: "Fleteros",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pesajes_Fleteros_FleteroId",
                table: "Pesajes");

            migrationBuilder.DropIndex(
                name: "IX_Pesajes_FleteroId",
                table: "Pesajes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Ganaderos_Codigo",
                table: "Ganaderos");

            migrationBuilder.DropIndex(
                name: "IX_Fleteros_Codigo",
                table: "Fleteros");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Fleteros_Codigo",
                table: "Fleteros");

            migrationBuilder.DropColumn(
                name: "FleteroId",
                table: "Pesajes");

            migrationBuilder.RenameColumn(
                name: "Codigo",
                table: "Ganaderos",
                newName: "Marca");

            migrationBuilder.RenameIndex(
                name: "IX_Ganaderos_Codigo",
                table: "Ganaderos",
                newName: "IX_Ganaderos_Marca");

            migrationBuilder.RenameColumn(
                name: "Codigo",
                table: "Fleteros",
                newName: "Orden");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Ganaderos_Marca",
                table: "Ganaderos",
                sql: "length(Marca) BETWEEN 1 AND 5");

            migrationBuilder.CreateIndex(
                name: "IX_Fleteros_Orden",
                table: "Fleteros",
                column: "Orden",
                unique: true,
                filter: "Activo = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Fleteros_Orden",
                table: "Fleteros",
                sql: "Orden BETWEEN 1 AND 4");
        }
    }
}
