using System.Security.Cryptography;

namespace Quizmester.Services;

// Wachtwoordbeveiliging: een hash is éénrichtingsverkeer (je kunt het origineel niet teruglezen).
// We gebruiken PBKDF2-SHA256 plus een unieke, willekeurige salt per account.
public static class PasswordHasher
{
    // Iterations maakt raden van wachtwoorden rekenintensief; salt voorkomt identieke hashes.
    // Deze instellingen zijn geschikt als demonstratie, maar security-eisen veranderen met de tijd.
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    // Alleen bij accountaanmaak: salt maken, PBKDF2 berekenen en resultaat opslaan.
    public static string HashPassword(string password)
    {
        // Cryptografisch veilige random, dus NIET de normale Random.Shared.
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        // Opslagvorm: aantal iteraties . salt . hash (Base64); GEEN plaintext wachtwoord.
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    // Tijdens login: haal de originele salt/iteraties uit de DB-string,
    // hash het ingevoerde wachtwoord opnieuw en vergelijk de resultaten.
    public static bool VerifyPassword(string password, string storedHash)
    {
        string[] parts = storedHash.Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[0], out int iterations))
            return false;

        byte[] salt;
        byte[] expectedHash;

        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expectedHash = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expectedHash.Length);

        // FixedTimeEquals vergelijkt in vaste tijd en verkleint timing side-channelrisico.
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
