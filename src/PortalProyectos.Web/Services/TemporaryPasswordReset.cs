using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using PortalProyectos.Infrastructure.Identity;

namespace PortalProyectos.Web.Services;

internal static class TemporaryPasswordReset
{
    public static async Task<(string? Password, IEnumerable<IdentityError> Errors)> ApplyAsync(
        UserManager<ApplicationUser> users,
        ApplicationUser account)
    {
        var previousMustChangePassword = account.MustChangePassword;
        account.MustChangePassword = true;
        account.UpdatedAtUtc = DateTimeOffset.UtcNow;
        var update = await users.UpdateAsync(account);
        if (!update.Succeeded) return (null, update.Errors);

        var password = GenerateTemporaryPassword();
        var token = await users.GeneratePasswordResetTokenAsync(account);
        var reset = await users.ResetPasswordAsync(account, token, password);
        if (!reset.Succeeded)
        {
            account.MustChangePassword = previousMustChangePassword;
            await users.UpdateAsync(account);
            return (null, reset.Errors);
        }

        await users.UpdateSecurityStampAsync(account);
        return (password, []);
    }

    public static string GenerateTemporaryPassword()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@$%*?-_";
        while (true)
        {
            var bytes = RandomNumberGenerator.GetBytes(20);
            var password = new StringBuilder(bytes.Length);
            foreach (var value in bytes) password.Append(alphabet[value % alphabet.Length]);
            var candidate = password.ToString();
            if (candidate.Any(char.IsUpper) && candidate.Any(char.IsLower) && candidate.Any(char.IsDigit)
                && candidate.Any(c => !char.IsLetterOrDigit(c))) return candidate;
        }
    }
}
