using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PesajeFaeneo.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Codigo = table.Column<int>(type: "INTEGER", nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    Direccion = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreadoEn = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfiguracionesBalanza",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    TipoConexion = table.Column<int>(type: "INTEGER", nullable: false),
                    PuertoCom = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    BaudRate = table.Column<int>(type: "INTEGER", nullable: false),
                    DataBits = table.Column<int>(type: "INTEGER", nullable: false),
                    Paridad = table.Column<int>(type: "INTEGER", nullable: false),
                    BitsParada = table.Column<int>(type: "INTEGER", nullable: false),
                    HostTcp = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PuertoTcp = table.Column<int>(type: "INTEGER", nullable: false),
                    PatronRegex = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    LecturasParaEstable = table.Column<int>(type: "INTEGER", nullable: false),
                    ToleranciaKg = table.Column<double>(type: "REAL", nullable: false),
                    TimeoutMs = table.Column<int>(type: "INTEGER", nullable: false),
                    ModificadoEn = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionesBalanza", x => x.Id);
                    table.CheckConstraint("CK_ConfiguracionBalanza_Id", "Id = 1");
                    table.CheckConstraint("CK_ConfiguracionBalanza_LecturasParaEstable", "LecturasParaEstable >= 1");
                    table.CheckConstraint("CK_ConfiguracionBalanza_TimeoutMs", "TimeoutMs > 0");
                    table.CheckConstraint("CK_ConfiguracionBalanza_TipoConexion", "TipoConexion IN (1, 2, 3)");
                });

            migrationBuilder.CreateTable(
                name: "Fleteros",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Orden = table.Column<int>(type: "INTEGER", nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    Celular = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreadoEn = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fleteros", x => x.Id);
                    table.CheckConstraint("CK_Fleteros_Orden", "Orden BETWEEN 1 AND 4");
                });

            migrationBuilder.CreateTable(
                name: "Ganaderos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Marca = table.Column<string>(type: "TEXT", maxLength: 5, nullable: false, collation: "NOCASE"),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreadoEn = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ganaderos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ParametrosSistema",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    NombreEmpresa = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Direccion = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Telefono = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Ciudad = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    RegistroSanitario = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TelefonosReclamos = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LogoPath = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    SiguienteNumeroBoleta = table.Column<long>(type: "INTEGER", nullable: false),
                    ObservacionPorDefecto = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    ConfiguracionInicialCompleta = table.Column<bool>(type: "INTEGER", nullable: false),
                    ModificadoEn = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParametrosSistema", x => x.Id);
                    table.CheckConstraint("CK_ParametrosSistema_Id", "Id = 1");
                    table.CheckConstraint("CK_ParametrosSistema_SiguienteNumeroBoleta", "SiguienteNumeroBoleta >= 1");
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NombreUsuario = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false, collation: "NOCASE"),
                    Hash = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Salt = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    NombreCompleto = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Rol = table.Column<int>(type: "INTEGER", nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    DebeCambiarPassword = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    UltimoAcceso = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreadoEn = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                    table.CheckConstraint("CK_Usuarios_Rol", "Rol IN (1, 2, 3)");
                });

            migrationBuilder.CreateTable(
                name: "Auditorias",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FechaHora = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UsuarioId = table.Column<int>(type: "INTEGER", nullable: true),
                    Accion = table.Column<int>(type: "INTEGER", nullable: false),
                    Entidad = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EntidadId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DatosAntesJson = table.Column<string>(type: "TEXT", nullable: true),
                    DatosDespuesJson = table.Column<string>(type: "TEXT", nullable: true),
                    Motivo = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auditorias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Auditorias_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pesajes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NumeroBoleta = table.Column<long>(type: "INTEGER", nullable: false),
                    FechaHora = table.Column<DateTime>(type: "TEXT", nullable: false),
                    GanaderoId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClienteId = table.Column<int>(type: "INTEGER", nullable: false),
                    Direccion = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Secuencia = table.Column<int>(type: "INTEGER", nullable: false),
                    ColorRaza = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Sexo = table.Column<char>(type: "TEXT", nullable: false),
                    TipoDesposte = table.Column<int>(type: "INTEGER", nullable: false),
                    PesoIzquierdo = table.Column<double>(type: "REAL", nullable: false),
                    PesoDerecho = table.Column<double>(type: "REAL", nullable: false),
                    PesoTotal = table.Column<double>(type: "REAL", nullable: false),
                    Observacion = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    Estado = table.Column<int>(type: "INTEGER", nullable: false),
                    MotivoAnulacion = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    AnuladoPorId = table.Column<int>(type: "INTEGER", nullable: true),
                    AnuladoEn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UsuarioId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModificadoEn = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pesajes", x => x.Id);
                    table.CheckConstraint("CK_Pesajes_Anulacion", "(Estado = 1 AND MotivoAnulacion IS NULL AND AnuladoPorId IS NULL AND AnuladoEn IS NULL) OR (Estado = 2 AND MotivoAnulacion IS NOT NULL AND AnuladoPorId IS NOT NULL AND AnuladoEn IS NOT NULL)");
                    table.CheckConstraint("CK_Pesajes_Estado", "Estado IN (1, 2)");
                    table.CheckConstraint("CK_Pesajes_Pesos", "PesoIzquierdo >= 0 AND PesoDerecho >= 0 AND PesoTotal >= 0");
                    table.CheckConstraint("CK_Pesajes_Secuencia", "Secuencia >= 1");
                    table.CheckConstraint("CK_Pesajes_Sexo", "Sexo IN ('M', 'H')");
                    table.CheckConstraint("CK_Pesajes_TipoDesposte", "TipoDesposte IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_Pesajes_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pesajes_Ganaderos_GanaderoId",
                        column: x => x.GanaderoId,
                        principalTable: "Ganaderos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pesajes_Usuarios_AnuladoPorId",
                        column: x => x.AnuladoPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pesajes_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "ConfiguracionesBalanza",
                columns: new[] { "Id", "BaudRate", "BitsParada", "DataBits", "HostTcp", "LecturasParaEstable", "ModificadoEn", "Paridad", "PatronRegex", "PuertoCom", "PuertoTcp", "TimeoutMs", "TipoConexion", "ToleranciaKg" },
                values: new object[] { 1, 9600, 1, 8, "192.168.1.100", 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0, "(?<peso>[-+]?\\d+(?:[.,]\\d+)?)", "COM1", 4001, 3000, 3, 0.5 });

            migrationBuilder.InsertData(
                table: "ParametrosSistema",
                columns: new[] { "Id", "Ciudad", "ConfiguracionInicialCompleta", "Direccion", "LogoPath", "ModificadoEn", "NombreEmpresa", "ObservacionPorDefecto", "RegistroSanitario", "SiguienteNumeroBoleta", "Telefono", "TelefonosReclamos" },
                values: new object[] { 1, "Santa Cruz - Bolivia", false, "", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "NOMBRE DE EMPRESA", "SIN NOVEDAD", "", 1L, "", "" });

            migrationBuilder.CreateIndex(
                name: "IX_Auditorias_Entidad_EntidadId",
                table: "Auditorias",
                columns: new[] { "Entidad", "EntidadId" });

            migrationBuilder.CreateIndex(
                name: "IX_Auditorias_FechaHora",
                table: "Auditorias",
                column: "FechaHora");

            migrationBuilder.CreateIndex(
                name: "IX_Auditorias_UsuarioId",
                table: "Auditorias",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_Codigo",
                table: "Clientes",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Fleteros_Orden",
                table: "Fleteros",
                column: "Orden",
                unique: true,
                filter: "Activo = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Ganaderos_Marca",
                table: "Ganaderos",
                column: "Marca",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pesajes_AnuladoPorId",
                table: "Pesajes",
                column: "AnuladoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_Pesajes_ClienteId_FechaHora",
                table: "Pesajes",
                columns: new[] { "ClienteId", "FechaHora" });

            migrationBuilder.CreateIndex(
                name: "IX_Pesajes_Estado_FechaHora",
                table: "Pesajes",
                columns: new[] { "Estado", "FechaHora" });

            migrationBuilder.CreateIndex(
                name: "IX_Pesajes_FechaHora",
                table: "Pesajes",
                column: "FechaHora");

            migrationBuilder.CreateIndex(
                name: "IX_Pesajes_GanaderoId_FechaHora",
                table: "Pesajes",
                columns: new[] { "GanaderoId", "FechaHora" });

            migrationBuilder.CreateIndex(
                name: "IX_Pesajes_NumeroBoleta",
                table: "Pesajes",
                column: "NumeroBoleta",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pesajes_UsuarioId",
                table: "Pesajes",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_NombreUsuario",
                table: "Usuarios",
                column: "NombreUsuario",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Auditorias");

            migrationBuilder.DropTable(
                name: "ConfiguracionesBalanza");

            migrationBuilder.DropTable(
                name: "Fleteros");

            migrationBuilder.DropTable(
                name: "ParametrosSistema");

            migrationBuilder.DropTable(
                name: "Pesajes");

            migrationBuilder.DropTable(
                name: "Clientes");

            migrationBuilder.DropTable(
                name: "Ganaderos");

            migrationBuilder.DropTable(
                name: "Usuarios");
        }
    }
}
