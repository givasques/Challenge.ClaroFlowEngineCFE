namespace ClaroFlowEngine.Api.Modules.Auth.Services;

public interface IPasswordHashingService
{
    string Hash(string password);

    /// <summary>True se a senha em texto puro corresponde ao hash armazenado.</summary>
    bool Verify(string passwordHash, string password);
}
