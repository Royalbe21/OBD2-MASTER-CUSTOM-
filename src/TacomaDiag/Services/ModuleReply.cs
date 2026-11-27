namespace TacomaDiag.Services;

internal static class ModuleReply
{
    // Discovery enables headers and spaces. ELM supplies a CAN frame length,
    // while J2534 supplies the already assembled payload after the CAN ID.
    public static bool HasPositive(string raw, string expectedId, bool assembled, params byte[] prefix)
    {
        foreach (var line in raw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2 || !tokens[0].Equals(expectedId, StringComparison.OrdinalIgnoreCase))
                continue;

            var bytes = new List<byte>();
            foreach (var token in tokens.Skip(1))
            {
                if (token.Length != 2 || !byte.TryParse(token, System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out var value))
                    break;
                bytes.Add(value);
            }
            if (bytes.Count != tokens.Length - 1) continue;
            if (!assembled)
            {
                if (bytes.Count == 0 || bytes[0] > 7 || bytes[0] > bytes.Count - 1) continue;
                bytes = bytes.GetRange(1, bytes[0]);
            }

            if (bytes.Count >= (prefix.SequenceEqual(new byte[] { 0x41, 0 }) ? 6 : prefix.Length + 1)
                && bytes.Take(prefix.Length).SequenceEqual(prefix)) return true;
        }
        return false;
    }
}
