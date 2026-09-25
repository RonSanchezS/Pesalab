using System.Diagnostics;
using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Balanza.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Balanza;
using PesajeFaeneo.WinForms.Formularios.Administracion;
using Xunit;

namespace PesajeFaeneo.Tests.Balanza;

public class FrmConfiguracionBalanzaTests
{
    private const string PatronP0 = @"(?<peso>[-+]?\s*\d+(?:[.,]\d+)?)";
    private const string PatronP1 = @"^(?<estable>ST)?[^-+\d]*(?<peso>[-+]?\s*\d+(?:[.,]\d+)?)";

    private class FalsoServicioConfiguracionBalanza : IServicioConfiguracionBalanza
    {
        public ConfiguracionBalanzaDto Dto { get; set; } = new(
            TipoConexion: TipoConexion.Serial,
            PuertoCom: "COM1",
            BaudRate: 9600,
            DataBits: 8,
            Paridad: ParidadSerial.None,
            BitsParada: BitsParada.One,
            HostTcp: "127.0.0.1",
            PuertoTcp: 4001,
            PatronRegex: PatronP0,
            LecturasParaEstable: 5,
            ToleranciaKg: 0.5m,
            TimeoutMs: 2000);

        public DateTime ModificadoEn { get; set; } = DateTime.UtcNow;
        public List<string> Puertos { get; set; } = new() { "COM1", "COM3" };
        public bool LanzarErrorValidacion { get; set; }
        public ValidacionException? ErrorValidacion { get; set; }
        public ConfiguracionBalanzaDto? DtoRecibidoEnActualizar { get; private set; }

        public Task<ConfiguracionBalanzaLeida> ObtenerAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ConfiguracionBalanzaLeida(Dto, ModificadoEn));
        }

        public IReadOnlyList<string> ListarPuertosSerie() => Puertos.ToList();

        public Task<bool> ActualizarAsync(ConfiguracionBalanzaDto dto, CancellationToken cancellationToken = default)
        {
            DtoRecibidoEnActualizar = dto;
            if (LanzarErrorValidacion)
            {
                throw ErrorValidacion ?? new ValidacionException(CamposBalanza.HostTcp, "Host TCP inválido.");
            }

            bool cambiado = dto != Dto;
            Dto = dto;
            return Task.FromResult(cambiado);
        }
    }

    private class SimuladorBalanzaFalso : ISimuladorBalanza
    {
        public decimal PesoObjetivoKg { get; set; }
        public bool DesconexionSimulada { get; set; }

        public void EstablecerPesoObjetivo(decimal pesoKg)
        {
            PesoObjetivoKg = Math.Clamp(Math.Round(pesoKg, 1, MidpointRounding.AwayFromZero), 0m, ReglasBalanza.PesoMaximoSimuladorKg);
        }
    }

    [Fact]
    public void Cargar_VuelcaTodosLosCampos_YConservaPuertoGuardadoNoListado()
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeConfig = new FalsoServicioConfiguracionBalanza
            {
                Dto = new ConfiguracionBalanzaDto(
                    TipoConexion: TipoConexion.Serial,
                    PuertoCom: "COM7",
                    BaudRate: 19200,
                    DataBits: 7,
                    Paridad: ParidadSerial.Even,
                    BitsParada: BitsParada.Two,
                    HostTcp: "192.168.1.50",
                    PuertoTcp: 5000,
                    PatronRegex: @"^(?<peso>\d+)",
                    LecturasParaEstable: 8,
                    ToleranciaKg: 1.5m,
                    TimeoutMs: 3500),
                Puertos = new() { "COM1", "COM3" }
            };
            var fakeBalanza = new ServicioBalanzaFalso();
            var fakeSimulador = new SimuladorBalanzaFalso();

            using var form = new FrmConfiguracionBalanza(fakeConfig, fakeBalanza, fakeSimulador);
            form.Show();

            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal("COM7", form.cmbPuerto.Text);
            Assert.Contains("COM7", form.cmbPuerto.Items.Cast<string>());
            Assert.Equal(19200, form.cmbBaudios.SelectedItem);
            Assert.Equal(7, form.cmbBitsDatos.SelectedItem);
            Assert.Equal("192.168.1.50", form.txtHost.Text);
            Assert.Equal(5000, form.numPuertoTcp.Value);
            Assert.Equal(@"^(?<peso>\d+)", form.txtPatron.Text);
            Assert.Equal(8, form.numLecturas.Value);
            Assert.Equal(1.5m, form.numTolerancia.Value);
            Assert.Equal(3500, form.numTimeout.Value);
        });
    }

    [Fact]
    public void DelimitadorYUnidad_SeCarganYSeEnvianAlGuardar()
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeConfig = new FalsoServicioConfiguracionBalanza();
            fakeConfig.Dto = fakeConfig.Dto with
            {
                DelimitadorTrama = DelimitadorTrama.Espacio,
                UnidadPeso = UnidadPesoTrama.Gramos
            };

            using var form = new FrmConfiguracionBalanza(fakeConfig, new ServicioBalanzaFalso(), new SimuladorBalanzaFalso());
            form.Show();
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal("Espacio", form.cmbDelimitador.Text);
            Assert.Equal("Gramos (g)", form.cmbUnidad.Text);

            var dto = form.LeerDtoDeControles();
            Assert.Equal(DelimitadorTrama.Espacio, dto.DelimitadorTrama);
            Assert.Equal(UnidadPesoTrama.Gramos, dto.UnidadPeso);

            // Probar patrón con trama RX8 real: 12345 g se muestran como 12,3 kg.
            form.txtTramaEjemplo.Text = "=012345";
            form.ProbarPatronParaTest();
            Assert.Contains("12", form.lblResultadoPatron.Text);
            Assert.DoesNotContain("12345", form.lblResultadoPatron.Text);
        });
    }

    [Fact]
    public void Layout_NingunControlQuedaFueraDeSuContenedor_NiConAnchoColapsado()
    {
        HiloSta.Ejecutar(() =>
        {
            using var form = new FrmConfiguracionBalanza(new FalsoServicioConfiguracionBalanza(), new ServicioBalanzaFalso(), new SimuladorBalanzaFalso());
            form.Show();
            form.CargarParaTestAsync().GetAwaiter().GetResult();
            form.lblResultadoPatron.Text = "Trama no reconocida: La trama no contiene un peso reconocible. Revise el patrón de lectura configurado.";
            System.Windows.Forms.Application.DoEvents();

            var problemas = new List<string>();
            foreach (var c in Descendientes(form).Where(c => c.Visible))
            {
                if (c.Parent is { } p and not Form && !(p is ScrollableControl { AutoScroll: true }))
                {
                    var r = p.ClientRectangle;
                    if (c.Right > r.Right + 1 || c.Bottom > r.Bottom + 1)
                    {
                        problemas.Add($"{c.GetType().Name} '{c.Text}' {c.Bounds} desborda {p.GetType().Name} {r}");
                    }
                }

                if (c is NumericUpDown { Width: < 60 } n)
                {
                    problemas.Add($"NumericUpDown '{n.Text}' colapsado a {n.Width} px");
                }
            }

            Assert.True(problemas.Count == 0, string.Join(Environment.NewLine, problemas));
        });
    }

    private static IEnumerable<Control> Descendientes(Control raiz)
    {
        foreach (Control hijo in raiz.Controls)
        {
            yield return hijo;
            foreach (var nieto in Descendientes(hijo))
            {
                yield return nieto;
            }
        }
    }

    [Fact]
    public void TipoConexion_AlternaHabilitacionSubgrupos_ManteniendoAmbosVisibles()
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeConfig = new FalsoServicioConfiguracionBalanza();
            var fakeBalanza = new ServicioBalanzaFalso();
            var fakeSimulador = new SimuladorBalanzaFalso();

            using var form = new FrmConfiguracionBalanza(fakeConfig, fakeBalanza, fakeSimulador);
            form.Show();
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            // Tipo inicial: Serial
            Assert.True(form.grpSerie.Enabled);
            Assert.False(form.grpRed.Enabled);
            Assert.True(form.grpSerie.Visible);
            Assert.True(form.grpRed.Visible);

            // Cambiar a TCP (índice 1: "Red (TCP/IP)")
            form.cmbTipoConexion.SelectedIndex = 1;

            Assert.False(form.grpSerie.Enabled);
            Assert.True(form.grpRed.Enabled);
            Assert.True(form.grpSerie.Visible);
            Assert.True(form.grpRed.Visible);
        });
    }

    [Fact]
    public void GrupoSimulador_SiempreVisible_HabilitadoSoloSiGuardadoEsSimulador_YCambioComboNoLoAltera()
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeConfig = new FalsoServicioConfiguracionBalanza
            {
                Dto = new ConfiguracionBalanzaDto(
                    TipoConexion: TipoConexion.Serial,
                    PuertoCom: "COM1",
                    BaudRate: 9600,
                    DataBits: 8,
                    Paridad: ParidadSerial.None,
                    BitsParada: BitsParada.One,
                    HostTcp: "127.0.0.1",
                    PuertoTcp: 4001,
                    PatronRegex: PatronP0,
                    LecturasParaEstable: 5,
                    ToleranciaKg: 0.5m,
                    TimeoutMs: 2000)
            };
            var fakeBalanza = new ServicioBalanzaFalso();
            var fakeSimulador = new SimuladorBalanzaFalso();

            using var form = new FrmConfiguracionBalanza(fakeConfig, fakeBalanza, fakeSimulador);
            form.Show();
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            // D16: el subgrupo nunca se oculta, solo se deshabilita.
            Assert.True(form.grpSimulador.Visible);
            Assert.False(form.grpSimulador.Enabled);

            // Cambiar el combo a Simulador sin guardar: no altera la habilitación (depende de lo guardado)
            form.cmbTipoConexion.SelectedIndex = 2;
            Assert.True(form.grpSimulador.Visible);
            Assert.False(form.grpSimulador.Enabled);

            // Ahora con configuración guardada como Simulador
            var fakeConfigSim = new FalsoServicioConfiguracionBalanza
            {
                Dto = fakeConfig.Dto with { TipoConexion = TipoConexion.Simulador }
            };
            using var formSim = new FrmConfiguracionBalanza(fakeConfigSim, fakeBalanza, fakeSimulador);
            formSim.Show();
            formSim.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.True(formSim.grpSimulador.Visible);
            Assert.True(formSim.grpSimulador.Enabled);

            // Cambiar el combo a Serial sin guardar: sigue habilitado y visible
            formSim.cmbTipoConexion.SelectedIndex = 0;
            Assert.True(formSim.grpSimulador.Visible);
            Assert.True(formSim.grpSimulador.Enabled);
        });
    }

    [Fact]
    public void Guardar_EnviaDtoDeControles_YErrorValidacionMarcaControl()
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeConfig = new FalsoServicioConfiguracionBalanza
            {
                LanzarErrorValidacion = true,
                ErrorValidacion = new ValidacionException(CamposBalanza.HostTcp, "Host TCP inválido.")
            };
            var fakeBalanza = new ServicioBalanzaFalso();
            var fakeSimulador = new SimuladorBalanzaFalso();

            using var form = new FrmConfiguracionBalanza(fakeConfig, fakeBalanza, fakeSimulador);
            form.Show();
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            form.txtHost.Text = "host con espacios";
            form.GuardarParaTestAsync().GetAwaiter().GetResult();

            Assert.NotNull(fakeConfig.DtoRecibidoEnActualizar);
            Assert.Equal("host con espacios", fakeConfig.DtoRecibidoEnActualizar.HostTcp);
            Assert.Equal("Host TCP inválido.", form.ProveedorError.GetError(form.txtHost));
        });
    }

    [Fact]
    public void ProbarPatron_P1ConTramaEjemplo_MuestraPesoYEstabilidad()
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeConfig = new FalsoServicioConfiguracionBalanza();
            var fakeBalanza = new ServicioBalanzaFalso();
            var fakeSimulador = new SimuladorBalanzaFalso();

            using var form = new FrmConfiguracionBalanza(fakeConfig, fakeBalanza, fakeSimulador);
            form.Show();
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            form.txtPatron.Text = PatronP1;
            form.txtTramaEjemplo.Text = "US,GS,+  98.6kg";

            form.ProbarPatronParaTest();

            Assert.Contains("98", form.lblResultadoPatron.Text);
            Assert.Contains("no", form.lblResultadoPatron.Text, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void ProbarConexion_ControlaEstadosDeBotones_PermiteCancelar_YActualizaResultado()
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeConfig = new FalsoServicioConfiguracionBalanza();
            var fakeBalanza = new ServicioBalanzaFalso();
            var fakeSimulador = new SimuladorBalanzaFalso();

            var tcs = new TaskCompletionSource();
            fakeBalanza.TcsPrueba = tcs;

            using var form = new FrmConfiguracionBalanza(fakeConfig, fakeBalanza, fakeSimulador);
            form.Show();
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            var tareaPrueba = form.ProbarConexionParaTestAsync();

            var sw = Stopwatch.StartNew();
            while (!form.EstaPruebaEnCurso && sw.ElapsedMilliseconds < 5000)
            {
                System.Windows.Forms.Application.DoEvents();
                Thread.Sleep(10);
            }

            Assert.True(form.EstaPruebaEnCurso);
            Assert.False(form.btnGuardar.Enabled);
            Assert.True(form.btnCancelarPrueba.Enabled);

            // Cancelar la prueba
            form.CancelarPruebaParaTest();
            Assert.True(fakeBalanza.TokenPruebaRecibido.IsCancellationRequested);

            // Completar la tarea de la prueba simulada
            tcs.SetResult();

            while (!tareaPrueba.IsCompleted && sw.ElapsedMilliseconds < 5000)
            {
                System.Windows.Forms.Application.DoEvents();
                Thread.Sleep(10);
            }

            tareaPrueba.GetAwaiter().GetResult();

            Assert.False(form.EstaPruebaEnCurso);
            Assert.True(form.btnGuardar.Enabled);
            Assert.False(form.btnCancelarPrueba.Enabled);
            Assert.Contains("cancelada", form.lblEstadoPrueba.Text, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Cerrar_MientrasPruebaEnCurso_DifiereElCierre_YCierraSoloAlTerminarLaPrueba()
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeConfig = new FalsoServicioConfiguracionBalanza();
            var fakeBalanza = new ServicioBalanzaFalso();
            var fakeSimulador = new SimuladorBalanzaFalso();

            var tcs = new TaskCompletionSource();
            fakeBalanza.TcsPrueba = tcs;

            var form = new FrmConfiguracionBalanza(fakeConfig, fakeBalanza, fakeSimulador)
            {
                ConfirmarCierre = false
            };
            form.Show();
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            var tareaPrueba = form.ProbarConexionParaTestAsync();

            var sw = Stopwatch.StartNew();
            while (!form.EstaPruebaEnCurso && sw.ElapsedMilliseconds < 5000)
            {
                System.Windows.Forms.Application.DoEvents();
                Thread.Sleep(10);
            }
            Assert.True(form.EstaPruebaEnCurso);

            // D17: el usuario intenta cerrar mientras la prueba sigue en curso.
            form.Close();

            // El cierre debe diferirse: el formulario sigue abierto y la prueba se cancela.
            Assert.False(form.IsDisposed);
            Assert.True(fakeBalanza.TokenPruebaRecibido.IsCancellationRequested);

            // Al completar la prueba (simulada), el formulario debe cerrarse solo, sin intervención adicional.
            tcs.SetResult();

            while (!form.IsDisposed && sw.ElapsedMilliseconds < 5000)
            {
                System.Windows.Forms.Application.DoEvents();
                Thread.Sleep(10);
            }

            tareaPrueba.GetAwaiter().GetResult();
            Assert.True(form.IsDisposed);
        });
    }

    [Fact]
    public void MostrarAvance_DespuesDeTerminarPrueba_NoAgregaItems()
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeConfig = new FalsoServicioConfiguracionBalanza();
            var fakeBalanza = new ServicioBalanzaFalso();
            var fakeSimulador = new SimuladorBalanzaFalso();

            using var form = new FrmConfiguracionBalanza(fakeConfig, fakeBalanza, fakeSimulador);
            form.Show();
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.False(form.EstaPruebaEnCurso);
            int countAntes = form.lstTramas.Items.Count;

            form.MostrarAvanceParaTest(new AvancePruebaBalanza(
                "ST,GS,100.0kg",
                new ResultadoTrama(true, 100.0m, true, null),
                true));

            Assert.Equal(countAntes, form.lstTramas.Items.Count);
        });
    }

    [Fact]
    public void MostrarAvance_LstTramasNuncaSupera50Items()
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeConfig = new FalsoServicioConfiguracionBalanza();
            var fakeBalanza = new ServicioBalanzaFalso();
            var fakeSimulador = new SimuladorBalanzaFalso();

            using var form = new FrmConfiguracionBalanza(fakeConfig, fakeBalanza, fakeSimulador);
            form.Show();
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            form.PruebaEnCursoParaTest = true;

            for (int i = 1; i <= 55; i++)
            {
                form.MostrarAvanceParaTest(new AvancePruebaBalanza(
                    $"Trama {i}",
                    new ResultadoTrama(true, 100.0m, true, null),
                    true));
            }

            Assert.Equal(50, form.lstTramas.Items.Count);
            Assert.Equal("Trama 55", form.lstTramas.Items[49]);
        });
    }
}
