namespace CEBAS.Domain.Common;

/// <summary>
/// Universal UUIDv7 (RFC 9562) Generator & Validator.
/// Provides 128-bit global uniqueness with monotonic time-ordered prefixes
/// to prevent B-Tree index fragmentation and natively support cursor pagination.
/// </summary>
public static class Uuid7
{
    /// <summary>
    /// Generates a new RFC 9562 compliant UUIDv7 identifier.
    /// </summary>
    public static Guid New() => Guid.CreateVersion7();

    /// <summary>
    /// Generates a new RFC 9562 compliant UUIDv7 identifier with a specified timestamp.
    /// </summary>
    public static Guid New(DateTimeOffset timestamp) => Guid.CreateVersion7(timestamp);

    /// <summary>
    /// Validates whether a given Guid has the UUIDv7 version nibble (0111) and RFC 4122/9562 variant.
    /// </summary>
    public static bool IsVersion7(Guid guid)
    {
        if (guid == Guid.Empty) return false;

        Span<byte> bytes = stackalloc byte[16];
        if (!guid.TryWriteBytes(bytes, bigEndian: true, out _)) return false;

        // In big-endian UUID layout:
        // Byte 6: high 4 bits are version (must be 0x7 = 7)
        // Byte 8: high 2 bits are variant (must be 0b10 = 0x80..0xBF)
        int version = (bytes[6] >> 4) & 0x0F;
        int variant = (bytes[8] >> 6) & 0x03;

        return version == 7 && variant == 2;
    }

    /// <summary>
    /// Extracts the Unix millisecond timestamp embedded within a UUIDv7 identifier.
    /// </summary>
    public static DateTimeOffset ExtractTimestamp(Guid guid)
    {
        Span<byte> bytes = stackalloc byte[16];
        if (!guid.TryWriteBytes(bytes, bigEndian: true, out _))
        {
            throw new ArgumentException("Invalid Guid", nameof(guid));
        }

        // First 48 bits (6 bytes) represent the Unix timestamp in milliseconds
        long ms = ((long)bytes[0] << 40)
                | ((long)bytes[1] << 32)
                | ((long)bytes[2] << 24)
                | ((long)bytes[3] << 16)
                | ((long)bytes[4] << 8)
                | bytes[5];

        return DateTimeOffset.FromUnixTimeMilliseconds(ms);
    }
}
