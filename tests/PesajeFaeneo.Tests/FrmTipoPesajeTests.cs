using PesajeFaeneo.Application.Parametros.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Pesaje;
using PesajeFaeneo.WinForms.Formularios.Administracion;
using Xunit;

namespace PesajeFaeneo.Tests;

/// <summary>F11 (D3-D5): formulario del tipo de pesaje.</summary>
public class FrmTipoPesajeTests
{
    [Fact]
    public void SinModoDefinido_NingunaOpcionMarcada_YGuardarDeshabilitado()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioParametrosFalso { Modo = null };
            using var form = new FrmTipoPesaje(servicio);
            form.Show();
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.False(form.rbAmbos.Checked);
            Assert.False(form.rbSoloResCompleta.Checked);
            Assert.False(form.rbSoloMediaRes.Checked);
            Assert.Null(form.ModoSeleccionado);
            Assert.False(form.btnGuardar.Enabled);

            form.rbSoloResCompleta.Checked = true;
            Assert.True(form.btnGuardar.Enabled);
        });
    }

    [Theory]
    [InlineData(ModoDesposte.Ambos)]
    [InlineData(ModoDesposte.SoloResCompleta)]
    [InlineData(ModoDesposte.SoloMediaRes)]
    public void ConModoDefinido_MarcaLaOpcionGuardada(ModoDesposte modo)
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioParametrosFalso { Modo = modo };
            using var form = new FrmTipoPesaje(servicio);
            form.Show();
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(modo, form.ModoSeleccionado);
            Assert.True(form.btnGuardar.Enabled);
        });
    }

    [Fact]
    public void Guardar_DesdeSinDefinir_NoPideConfirmacionYGuardaElModo()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioParametrosFalso { Modo = null };
            var confirmaciones = 0;
            using var form = new FrmTipoPesaje(servicio) { ConfirmarParaTest = _ => { confirmaciones++; return true; } };
            form.Show();
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            form.rbSoloMediaRes.Checked = true;
            form.GuardarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(0, confirmaciones);
            Assert.Equal(new[] { ModoDesposte.SoloMediaRes }, servicio.ModosGuardados);
            Assert.Equal(DialogResult.OK, form.DialogResult);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Guardar_CambiandoUnModoDefinido_PideConfirmacion(bool acepta)
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioParametrosFalso { Modo = ModoDesposte.Ambos };
            string? texto = null;
            using var form = new FrmTipoPesaje(servicio) { ConfirmarParaTest = t => { texto = t; return acepta; } };
            form.Show();
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            form.rbSoloResCompleta.Checked = true;
            form.GuardarParaTestAsync().GetAwaiter().GetResult();

            Assert.NotNull(texto);
            Assert.Contains("Res completa y media res", texto);
            Assert.Contains("Solo res completa", texto);
            Assert.Equal(acepta ? new[] { ModoDesposte.SoloResCompleta } : Array.Empty<ModoDesposte>(), servicio.ModosGuardados);
        });
    }

    [Fact]
    public void Guardar_MismoModo_NoPideConfirmacion()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioParametrosFalso { Modo = ModoDesposte.SoloResCompleta };
            var confirmaciones = 0;
            using var form = new FrmTipoPesaje(servicio) { ConfirmarParaTest = _ => { confirmaciones++; return true; } };
            form.Show();
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            form.GuardarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(0, confirmaciones);
        });
    }

    [Fact]
    public void AvisoDeBoletasExistentes_SegunElTipoQueElModoOculta()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioParametrosFalso { Modo = ModoDesposte.Ambos, BoletasMediaRes = 3, BoletasResCompleta = 1 };
            using var form = new FrmTipoPesaje(servicio);
            form.Show();
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(string.Empty, form.lblAviso.Text);

            form.rbSoloResCompleta.Checked = true;
            Assert.StartsWith("Hay 3 boletas de media res", form.lblAviso.Text);

            form.rbSoloMediaRes.Checked = true;
            Assert.StartsWith("Hay 1 boleta de res completa", form.lblAviso.Text);
        });
    }

    [Fact]
    public void TextoAviso_SinBoletasDelOtroTipo_Vacio()
    {
        Assert.Equal(string.Empty, FrmTipoPesaje.TextoAviso(ModoDesposte.SoloResCompleta, new ConfiguracionDesposte(null, 10, 0)));
        Assert.Equal(string.Empty, FrmTipoPesaje.TextoAviso(ModoDesposte.Ambos, new ConfiguracionDesposte(null, 10, 10)));
    }

    [Fact]
    public void Layout_SinRecortesA96DpiYConEscalado()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioParametrosFalso { Modo = ModoDesposte.Ambos, BoletasMediaRes = 12 };
            using var form = new FrmTipoPesaje(servicio);
            form.Show();
            form.CargarParaTestAsync().GetAwaiter().GetResult();
            form.rbSoloResCompleta.Checked = true;
            System.Windows.Forms.Application.DoEvents();

            VerificadorLayout.Verificar(form);
            VerificadorLayout.VerificarEscalado(form);
        });
    }
}
