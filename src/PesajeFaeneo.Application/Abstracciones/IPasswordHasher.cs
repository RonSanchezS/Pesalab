namespace PesajeFaeneo.Application.Abstracciones;

public interface IPasswordHasher
{
    (string Hash, string Salt) Hashear(string password);
    bool Verificar(string password, string hash, string salt);
}
