using ClaroFlowEngine.Api.Common.Contracts;
using ClaroFlowEngine.Api.Common.Errors;
using ClaroFlowEngine.Api.Configuration;
using ClaroFlowEngine.Api.Data;
using ClaroFlowEngine.Api.Data.Entities;
using ClaroFlowEngine.Api.Modules.Auth.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClaroFlowEngine.Api.Modules.Auth.Services;

public class AuthService : IAuthService
{
    private readonly CfeDbContext _db;
    private readonly IPasswordHashingService _passwordHashing;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly PanelAuthOptions _panelAuth;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        CfeDbContext db,
        IPasswordHashingService passwordHashing,
        IJwtTokenService jwtTokenService,
        IOptions<PanelAuthOptions> panelAuthOptions,
        ILogger<AuthService> logger)
    {
        _db = db;
        _passwordHashing = passwordHashing;
        _jwtTokenService = jwtTokenService;
        _panelAuth = panelAuthOptions.Value;
        _logger = logger;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _db.PanelUsers.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        // Usuário inexistente, inativo ou senha errada devolvem a mesma resposta — nunca revelar qual
        // dos dois estava errado (A.6). Por isso o e-mail mascarado é logado antes de qualquer outra checagem.
        if (user is null || !user.IsActive)
        {
            _logger.LogWarning("Login failed for {MaskedEmail}: user not found or inactive", MaskEmail(email));
            throw InvalidCredentials();
        }

        var now = DateTime.UtcNow;

        // Bloqueio expirado: reseta antes de continuar, como um recomeço limpo.
        if (user.LockedUntil is not null && user.LockedUntil <= now)
        {
            user.LockedUntil = null;
            user.FailedLoginAttempts = 0;
        }

        if (user.LockedUntil is not null && user.LockedUntil > now)
        {
            _logger.LogWarning("Login blocked for {PanelUserId}: account locked until {LockedUntil}", user.Id, user.LockedUntil);
            throw new AccountLockedException("account_locked", "Muitas tentativas. Tente novamente em alguns minutos.");
        }

        if (!_passwordHashing.Verify(user.PasswordHash, request.Password))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= _panelAuth.MaxFailedAttempts)
            {
                user.LockedUntil = now.AddMinutes(_panelAuth.LockoutMinutes);
                _logger.LogWarning("Account locked for {PanelUserId} after {Attempts} failed attempts", user.Id, user.FailedLoginAttempts);
            }
            user.UpdatedAt = now;
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogWarning("Login failed for {MaskedEmail}: wrong password", MaskEmail(email));
            throw InvalidCredentials();
        }

        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.LastLoginAt = now;
        user.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Login succeeded for {PanelUserId} with role {Role}", user.Id, user.Role);

        var (accessToken, expiresAt) = _jwtTokenService.GenerateToken(user);
        return new LoginResponse(accessToken, expiresAt, ToDto(user));
    }

    public async Task<PanelUserDto> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _db.PanelUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new UnauthorizedException("invalid_or_expired_session", "Sessão inválida ou expirada. Entre novamente.");

        return ToDto(user);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await _db.PanelUsers.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new UnauthorizedException("invalid_or_expired_session", "Sessão inválida ou expirada. Entre novamente.");

        if (!_passwordHashing.Verify(user.PasswordHash, request.CurrentPassword))
            throw new ValidationException("invalid_current_password", "Senha atual incorreta.");

        if (!IsStrongPassword(request.NewPassword))
            throw new ValidationException("weak_password", "A nova senha deve ter no mínimo 8 caracteres, com letras e números.");

        if (_passwordHashing.Verify(user.PasswordHash, request.NewPassword))
            throw new ValidationException("password_unchanged", "A nova senha deve ser diferente da atual.");

        user.PasswordHash = _passwordHashing.Hash(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static bool IsStrongPassword(string password) =>
        password.Length >= 8 && password.Any(char.IsLetter) && password.Any(char.IsDigit);

    private static UnauthorizedException InvalidCredentials() =>
        new("invalid_credentials", "E-mail ou senha inválidos.");

    // "ju***@cfe.demo" — mantém só o primeiro par de caracteres do nome do e-mail (Parte I §12 do padrão:
    // e-mail de atendente também é dado pessoal, nunca logado em texto puro).
    private static string MaskEmail(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 1) return "***" + email[atIndex..];
        return $"{email[..2]}***{email[atIndex..]}";
    }

    private static PanelUserDto ToDto(PanelUser user) => new(
        user.Id, user.FullName, user.Email, user.Role, PanelRole.Label(user.Role), user.LastLoginAt);
}
