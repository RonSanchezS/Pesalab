using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PesajeFaeneo.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class F4_Pesaje : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Pesajes_Secuencia",
                table: "Pesajes");

            migrationBuilder.AddColumn<int>(
                name: "OrigenPeso",
                table: "Pesajes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Pesajes_ColorRaza",
                table: "Pesajes",
                sql: "length(ColorRaza) <= 30");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Pesajes_Direccion",
                table: "Pesajes",
                sql: "length(Direccion) <= 150");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Pesajes_MotivoAnulacion",
                table: "Pesajes",
                sql: "MotivoAnulacion IS NULL OR length(MotivoAnulacion) BETWEEN 1 AND 250");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Pesajes_NumeroBoleta",
                table: "Pesajes",
                sql: "NumeroBoleta >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Pesajes_Observacion",
                table: "Pesajes",
                sql: "length(Observacion) <= 250");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Pesajes_OrigenPeso",
                table: "Pesajes",
                sql: "OrigenPeso IN (1, 2, 3)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Pesajes_PesoTotal",
                table: "Pesajes",
                sql: "abs(PesoTotal - (PesoIzquierdo + PesoDerecho)) < 0.05");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Pesajes_Secuencia",
                table: "Pesajes",
                sql: "Secuencia BETWEEN 1 AND 9999");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Pesajes_ColorRaza",
                table: "Pesajes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Pesajes_Direccion",
                table: "Pesajes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Pesajes_MotivoAnulacion",
                table: "Pesajes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Pesajes_NumeroBoleta",
                table: "Pesajes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Pesajes_Observacion",
                table: "Pesajes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Pesajes_OrigenPeso",
                table: "Pesajes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Pesajes_PesoTotal",
                table: "Pesajes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Pesajes_Secuencia",
                table: "Pesajes");

            migrationBuilder.DropColumn(
                name: "OrigenPeso",
                table: "Pesajes");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Pesajes_Secuencia",
                table: "Pesajes",
                sql: "Secuencia >= 1");
        }
    }
}
