using PesajeFaeneo.Application.Boletas;
using PesajeFaeneo.Application.Boletas.Dtos;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Boletas;
using PesajeFaeneo.WinForms.Formularios.Administracion;
using PesajeFaeneo.WinForms.Formularios.Boletas;
using Xunit;

namespace PesajeFaeneo.Tests.Formularios;

public class FrmConfiguracionImpresionTests
{
    private readonly ServicioConfiguracionImpresionFalso _config = new();
    private readonly ServicioBoletasFalso _boletas = new();
    private readonly ImpresorBoletasFalso _impresor = new();

    private FrmConfiguracionImpresion CrearFormulario()
    {
        return new FrmConfiguracionImpresion(_config, _boletas, _impresor);
    }

    [Fact]
    public void Cargar_SinConfiguracion_UsaPorDefectoYMuestraSinConfigurar()
    {
        HiloSta.Ejecutar(() =>
        {
            _config.EquipoRetorno = new ConfiguracionImpresionEquipo(ReglasImpresion.PorDefecto, Configurada: false, NombreEquipo: "PC-TEST");

            using var form = CrearFormulario();
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(0, form.cmbImpresora.SelectedIndex);
            Assert.Equal(0, form.cmbPapel.SelectedIndex);
            Assert.Equal(1m, form.numCopias.Value);
            Assert.False(form.chkImprimirAlGrabar.Checked);
            Assert.Contains("Sin configurar", form.lblEstadoConfig.Text);
        });
    }

    [Fact]
    public void Cargar_ConImpresoraNoInstalada_AgregaOpcionYMuestraAdvertencia()
    {
        HiloSta.Ejecutar(() =>
        {
            var config = new ConfiguracionImpresion("IMPRESORA-FANTASMA", FormatoBoleta.MediaCarta, null, 1, false, 0m, 0m);
            _config.EquipoRetorno = new ConfiguracionImpresionEquipo(config, Configurada: true, NombreEquipo: "PC-TEST");

            using var form = CrearFormulario();
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            var seleccionado = form.cmbImpresora.SelectedItem as FrmConfiguracionImpresion.ItemTexto;
            Assert.NotNull(seleccionado);
            Assert.Contains("(NO INSTALADA)", seleccionado.Texto);
            Assert.Equal("IMPRESORA-FANTASMA", seleccionado.Valor);
            Assert.Contains("no está instalada", form.lblAdvertencia.Text);
        });
    }

    [Fact]
    public void CambiarImpresora_RecargaListaDePapeles()
    {
        HiloSta.Ejecutar(() =>
        {
            _impresor.ImpresorasInstaladas = new() { "IMP1", "IMP2" };
            _impresor.PapelesPorImpresora = new()
            {
                ["IMP1"] = new() { "Papel1A", "Papel1B" },
                ["IMP2"] = new() { "Papel2A" }
            };

            using var form = CrearFormulario();
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            // Seleccionar IMP1
            form.cmbImpresora.SelectedIndex = 1; // 0 es predeterminada, 1 es IMP1
            Assert.Contains(form.cmbPapel.Items.Cast<FrmConfiguracionImpresion.ItemTexto>(), p => p.Valor == "Papel1A");
            Assert.DoesNotContain(form.cmbPapel.Items.Cast<FrmConfiguracionImpresion.ItemTexto>(), p => p.Valor == "Papel2A");

            // Cambiar a IMP2
            form.cmbImpresora.SelectedIndex = 2; // 2 es IMP2
            Assert.Contains(form.cmbPapel.Items.Cast<FrmConfiguracionImpresion.ItemTexto>(), p => p.Valor == "Papel2A");
            Assert.DoesNotContain(form.cmbPapel.Items.Cast<FrmConfiguracionImpresion.ItemTexto>(), p => p.Valor == "Papel1A");
        });
    }

    [Fact]
    public void Guardar_ValoresValidos_EnviaAlServicioConfiguracion()
    {
        HiloSta.Ejecutar(() =>
        {
            _impresor.ImpresorasInstaladas = new() { "HP 1" };
            _impresor.PapelesPorImpresora = new() { ["HP 1"] = new() { "Letter" } };

            using var form = CrearFormulario();
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            form.cmbImpresora.SelectedIndex = 1; // HP 1
            form.cmbPapel.SelectedIndex = 0; // (Automático) -> null
            form.numCopias.Value = 2;
            form.numDesplX.Value = -2.5m;
            form.numDesplY.Value = 1.0m;

            form.GuardarParaTestAsync().GetAwaiter().GetResult();

            Assert.Single(_config.ConfiguracionesGuardadas);
            var guardada = _config.ConfiguracionesGuardadas[0];
            Assert.Equal("HP 1", guardada.NombreImpresora);
            Assert.Null(guardada.NombrePapel);
            Assert.Equal(2, guardada.Copias);
            Assert.Equal(-2.5m, guardada.DesplazamientoXMm);
            Assert.Equal(1.0m, guardada.DesplazamientoYMm);
            Assert.Equal(DialogResult.OK, form.DialogResult);
        });
    }

    [Fact]
    public void Guardar_ServicioLanzaValidacion_MuestraErrorYFormularioSigueAbierto()
    {
        HiloSta.Ejecutar(() =>
        {
            _config.ExcepcionGuardar = new ValidacionException(CamposImpresion.Copias, "El número de copias es inválido.");

            using var form = CrearFormulario();
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            form.GuardarParaTestAsync().GetAwaiter().GetResult();

            var errorProvider = (ErrorProvider)typeof(FrmConfiguracionImpresion)
                .GetField("_errorProvider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .GetValue(form)!;

            var error = errorProvider.GetError(form.numCopias);
            Assert.NotEmpty(error);
            Assert.Contains("inválido", error);
            Assert.NotEqual(DialogResult.OK, form.DialogResult);
        });
    }

    [Theory]
    [InlineData("Microsoft Print to PDF", true)]
    [InlineData("Microsoft XPS Document Writer", true)]
    [InlineData("Send to OneNote 2016", true)]
    [InlineData("Fax", true)]
    [InlineData("EPSON LX-350", false)]
    [InlineData("HP LaserJet Pro", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void EsImpresoraVirtual_DetectaCorrectamente(string? nombre, bool esperado)
    {
        var resultado = FrmConfiguracionImpresion.EsImpresoraVirtual(nombre);
        Assert.Equal(esperado, resultado);
    }

    [Fact]
    public void Calibrar_InvocaImpresorConConfiguracionEnEdicion()
    {
        HiloSta.Ejecutar(() =>
        {
            using var form = CrearFormulario();
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            form.numDesplX.Value = 3.5m;
            form.CalibrarParaTest();

            Assert.Equal("Imprimir página de calibración", form.btnCalibracion.Text);
            Assert.Single(_impresor.Calibraciones);
            Assert.Equal(3.5m, _impresor.Calibraciones[0].DesplazamientoXMm);
        });
    }

    [Fact]
    public void VistaPrevia_InvocaCrearMuestraYAbreFrmVistaPrevia()
    {
        HiloSta.Ejecutar(() =>
        {
            FrmVistaPrevia? formVistaPreviaRecibido = null;
            using var form = CrearFormulario();
            form.MostrarDialogoParaTest = frm =>
            {
                formVistaPreviaRecibido = Assert.IsType<FrmVistaPrevia>(frm);
                return DialogResult.OK;
            };

            form.CargarParaTestAsync().GetAwaiter().GetResult();
            form.VistaPreviaParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(1, _boletas.ConteoCrearMuestra);
            Assert.NotNull(formVistaPreviaRecibido);
            formVistaPreviaRecibido.Dispose();
        });
    }
}
