using System.Reflection;
using PesajeFaeneo.WinForms;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ProgramTests
{
    [Fact]
    public void Main_EsSincronoYSta()
    {
        var metodoMain = typeof(Program).GetMethod(
            "Main",
            BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(metodoMain);
        Assert.False(typeof(Task).IsAssignableFrom(metodoMain!.ReturnType),
            "Main no debe retornar Task: el punto de entrada STA debe ser síncrono.");
        Assert.True(metodoMain.ReturnType == typeof(int) || metodoMain.ReturnType == typeof(void),
            "Main debe retornar int o void.");

        var atributoSta = metodoMain.GetCustomAttribute<STAThreadAttribute>();
        Assert.NotNull(atributoSta);
    }
}
