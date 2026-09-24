using PesajeFaeneo.Infrastructure.Seguridad;
using Xunit;

namespace PesajeFaeneo.Tests;

public class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Hashear_GeneraHashYSaltNoVaciosYValidaCorrectamente()
    {
        var password = "Password123!";
        var (hash, salt) = _hasher.Hashear(password);

        Assert.False(string.IsNullOrWhiteSpace(hash));
        Assert.False(string.IsNullOrWhiteSpace(salt));

        var esValido = _hasher.Verificar(password, hash, salt);
        Assert.True(esValido);
    }

    [Fact]
    public void Hashear_DosVecesMismaPassword_GeneraDiferenteSaltYHashYAmbosVerifican()
    {
        var password = "SuperSecretPassword";
        var (hash1, salt1) = _hasher.Hashear(password);
        var (hash2, salt2) = _hasher.Hashear(password);

        Assert.NotEqual(salt1, salt2);
        Assert.NotEqual(hash1, hash2);

        Assert.True(_hasher.Verificar(password, hash1, salt1));
        Assert.True(_hasher.Verificar(password, hash2, salt2));
    }

    [Fact]
    public void Verificar_PasswordErronea_RetornaFalse()
    {
        var (hash, salt) = _hasher.Hashear("ClaveCorrecta");
        var resultado = _hasher.Verificar("ClaveIncorrecta", hash, salt);

        Assert.False(resultado);
    }
}
