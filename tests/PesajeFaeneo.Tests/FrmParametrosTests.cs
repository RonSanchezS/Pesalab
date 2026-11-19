using PesajeFaeneo.Application.Parametros;
using PesajeFaeneo.Application.Parametros.Dtos;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.WinForms.Formularios.Administracion;
using Xunit;

namespace PesajeFaeneo.Tests;

public class FrmParametrosTests
{
    private class FalsoServicioParametros : IServicioParametros
    {
        public ParametrosDto Dto { get; set; } = new(
            NombreEmpresa: "MATADERO CENTRAL",
            Direccion: "AV PRINCIPAL 100",
            Telefono: "0212-1234567",
            Ciudad: "CARACAS",
            RegistroSanitario: "RS-999",
            TelefonosReclamos: "0800-CARNE",
            ObservacionPorDefecto: "OBS PREDETERMINADA",
            SiguienteNumeroBoleta: 10,
            UltimoNumeroBoletaEmitido: 9,
            LogoArchivo: null,
            RutaLogoCompleta: null,
            ModificadoEn: DateTime.UtcNow);

        public bool LanzarErrorValidacion { get; set; }

        public Task<ParametrosDto> ObtenerAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Dto);

        public Task<IdentidadEmpresa> ObtenerIdentidadEmpresaAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new IdentidadEmpresa(Dto.NombreEmpresa, Dto.RutaLogoCompleta));

        public Task<PesajeFaeneo.Domain.Enums.ModoDesposte> ObtenerModoDesposteAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(PesajeFaeneo.Domain.Enums.ModoDesposte.Ambos);

        public Task<ConfiguracionDesposte> ObtenerConfiguracionDesposteAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ConfiguracionDesposte(PesajeFaeneo.Domain.Enums.ModoDesposte.Ambos, 0, 0));

        public Task CambiarModoDesposteAsync(PesajeFaeneo.Domain.Enums.ModoDesposte modo, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ActualizarAsync(ActualizarParametros dto, CancellationToken cancellationToken = default)
        {
            if (LanzarErrorValidacion)
            {
                throw new ValidacionException(CamposParametros.NombreEmpresa, "El nombre de la empresa es obligatorio.");
            }

            Dto = Dto with
            {
                NombreEmpresa = dto.NombreEmpresa,
                Direccion = dto.Direccion ?? string.Empty,
                Telefono = dto.Telefono ?? string.Empty,
                Ciudad = dto.Ciudad ?? string.Empty,
                RegistroSanitario = dto.RegistroSanitario ?? string.Empty,
                TelefonosReclamos = dto.TelefonosReclamos ?? string.Empty,
                ObservacionPorDefecto = dto.ObservacionPorDefecto ?? string.Empty,
                SiguienteNumeroBoleta = dto.SiguienteNumeroBoleta
            };
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void FrmParametros_CargarDatos_MuestraCamposYUltimoNumeroBoleta()
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeServicio = new FalsoServicioParametros();
            using var form = new FrmParametros(fakeServicio);
            form.Show();

            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal("MATADERO CENTRAL", form.TxtNombreEmpresa.Text);
            Assert.Equal("AV PRINCIPAL 100", form.TxtDireccion.Text);
            Assert.Equal("CARACAS", form.TxtCiudad.Text);
            Assert.Equal("0212-1234567", form.TxtTelefono.Text);
            Assert.Equal("RS-999", form.TxtRegistroSanitario.Text);
            Assert.Equal("0800-CARNE", form.TxtTelefonosReclamos.Text);
            Assert.Equal("OBS PREDETERMINADA", form.TxtObservacionPorDefecto.Text);
            Assert.Equal(10, form.NumSiguienteBoleta.Value);
            Assert.Contains("9", form.LblUltimaBoleta.Text);
        });
    }

    [Fact]
    public void FrmParametros_ConBoletasEmitidas_DeshabilitaElNumeroYExplicaQueEsAutomatico()
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeServicio = new FalsoServicioParametros(); // Último emitido = 9
            using var form = new FrmParametros(fakeServicio);
            form.Show();

            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.False(form.NumSiguienteBoleta.Enabled);
            Assert.Equal(10, form.NumSiguienteBoleta.Value);
            Assert.Contains("automáticamente", form.LblNotaNumeracion.Text);
        });
    }

    [Fact]
    public void FrmParametros_SinBoletasEmitidas_HabilitaElNumeroInicial()
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeServicio = new FalsoServicioParametros();
            fakeServicio.Dto = fakeServicio.Dto with { UltimoNumeroBoletaEmitido = null, SiguienteNumeroBoleta = 1 };
            using var form = new FrmParametros(fakeServicio);
            form.Show();

            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.True(form.NumSiguienteBoleta.Enabled);
            Assert.Contains("antes de emitir la primera boleta", form.LblNotaNumeracion.Text);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FrmParametros_Layout_ConLaNotaDeNumeracion_SinRecortesA96DpiYConEscalado(bool numeracionEditable)
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeServicio = new FalsoServicioParametros();
            if (numeracionEditable)
            {
                fakeServicio.Dto = fakeServicio.Dto with { UltimoNumeroBoletaEmitido = null, SiguienteNumeroBoleta = 1 };
            }

            using var form = new FrmParametros(fakeServicio);
            form.Show();
            form.CargarParaTestAsync().GetAwaiter().GetResult();
            System.Windows.Forms.Application.DoEvents();

            VerificadorLayout.Verificar(form);
            VerificadorLayout.VerificarEscalado(form);
        });
    }

    [Fact]
    public void FrmParametros_QuitarLogo_EstableceAccionEliminarYVaciaPreview()
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeServicio = new FalsoServicioParametros();
            using var form = new FrmParametros(fakeServicio);
            form.Show();

            form.CargarParaTestAsync().GetAwaiter().GetResult();
            form.BtnQuitarLogo.PerformClick();

            Assert.Equal(AccionLogo.Quitar, form.AccionLogoParaTest);
            Assert.Null(form.PicLogo.Image);
        });
    }

    [Fact]
    public void FrmParametros_GuardarConErrorValidacion_MuestraErrorEnControlYNoCierraFormulario()
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeServicio = new FalsoServicioParametros { LanzarErrorValidacion = true };
            using var form = new FrmParametros(fakeServicio);
            form.Show();

            form.CargarParaTestAsync().GetAwaiter().GetResult();
            form.GuardarSinConfirmarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(DialogResult.None, form.DialogResult);
            var error = form.ProveedorError.GetError(form.TxtNombreEmpresa);
            Assert.Equal("El nombre de la empresa es obligatorio.", error);
        });
    }
}
