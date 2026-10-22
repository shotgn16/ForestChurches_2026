using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ForestChurches.Components.Security
{
    /// <summary>
    /// Encrypts sensitive database columns (ChurchSuite tokens and API keys) with ASP.NET Data Protection,
    /// so they're stored as ciphertext instead of plain text.
    ///
    /// - Encrypted values are stored as "dp1:" + ciphertext.
    /// - Values without the prefix are treated as legacy plain text and read as they are; they're
    ///   encrypted the next time the row is saved.
    /// - Null and empty values are left as they are, so "has a token?" checks still work in SQL.
    /// - Because ciphertext changes on every write, never filter or join on these columns.
    ///
    /// The protector is set once at start-up (Program.cs). Before that (for example in EF design-time
    /// tools) values pass through unchanged.
    /// </summary>
    public static class ProtectedColumn
    {
        private const string Prefix = "dp1:";
        private const string Purpose = "fc.data.db.column.v1";
        private static IDataProtector? _protector;

        public static void Initialise(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector(Purpose);
        }

        public static string? Protect(string? value)
        {
            if (string.IsNullOrEmpty(value) || _protector == null || value.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return value;
            }
            return Prefix + _protector.Protect(value);
        }

        public static string? Unprotect(string? value)
        {
            if (string.IsNullOrEmpty(value) || !value.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return value; // empty, or legacy plain text
            }
            if (_protector == null)
            {
                throw new InvalidOperationException("ProtectedColumn has not been initialised; cannot decrypt a protected database value.");
            }
            return _protector.Unprotect(value.Substring(Prefix.Length));
        }

        /// <summary>EF Core converter for a protected string column.</summary>
        public static ValueConverter<string?, string?> Converter { get; } =
            new ValueConverter<string?, string?>(v => Protect(v), v => Unprotect(v));
    }
}
