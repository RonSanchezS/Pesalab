using System.Drawing.Printing;
using PesajeFaeneo.Application.Boletas.Dtos;

namespace PesajeFaeneo.WinForms.Impresion;

public interface IImpresorBoletas
{
    IReadOnlyList<string> ListarImpresoras();
    string? ObtenerImpresoraPredeterminada();
    IReadOnlyList<string> ListarPapeles(string? nombreImpresora);
    void Imprimir(DatosBoleta datos, ConfiguracionImpresion configuracion);
    PrintDocument CrearVistaPrevia(DatosBoleta datos, ConfiguracionImpresion configuracion);
    void ImprimirCalibracion(ConfiguracionImpresion configuracion);
}
