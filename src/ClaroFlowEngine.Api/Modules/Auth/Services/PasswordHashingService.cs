using ClaroFlowEngine.Api.Data.Entities;
using Microsoft.AspNetCore.Identity;

namespace ClaroFlowEngine.Api.Modules.Auth.Services;

/// <summary>
/// Usa só o <see cref="PasswordHasher{TUser}"/> do ASP.NET Core Identity (PBKDF2 com salt),
/// sem adotar o Identity completo (seria peso desnecessário para o MVP — A.2 da spec).
/// </summary>
public class PasswordHashingService : IPasswordHashingService
{
    private readonly PasswordHasher<PanelUser> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(null!, password);

    public bool Verify(string passwordHash, string password) =>
        _hasher.VerifyHashedPassword(null!, passwordHash, password) != PasswordVerificationResult.Failed;
}
