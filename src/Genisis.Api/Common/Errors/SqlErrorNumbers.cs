namespace Genisis.Api.Common.Errors;

/// <summary>SQL Server error numbers the API maps to user-facing responses.</summary>
public static class SqlErrorNumbers
{
    /// <summary>String or binary data would be truncated (pre-2019 message).</summary>
    public const int StringTruncated = 8152;

    /// <summary>String or binary data would be truncated in a named column (2019+ message).</summary>
    public const int StringTruncatedInColumn = 2628;

    /// <summary>The database master key is not open, so the encryption triggers cannot run.</summary>
    public const int MasterKeyNotOpen = 15581;

    /// <summary>An error occurred during decryption (encryption key missing or wrong).</summary>
    public const int DecryptionFailed = 15466;

    /// <summary>Command timeout raised by SqlClient.</summary>
    public const int Timeout = -2;

    /// <summary>Could not find stored procedure: the SQL scripts of this version have not been run.</summary>
    public const int MissingProcedure = 2812;

    /// <summary>Violation of a primary key or unique constraint.</summary>
    public const int DuplicateKey = 2627;

    /// <summary>Cannot insert a duplicate key row in a unique index.</summary>
    public const int DuplicateIndexKey = 2601;

    public static bool IsTruncation(int number) => number is StringTruncated or StringTruncatedInColumn;

    public static bool IsDuplicateKey(int number) => number is DuplicateKey or DuplicateIndexKey;

    public static bool IsEncryptionKeyMissing(int number) => number is MasterKeyNotOpen or DecryptionFailed;
}
