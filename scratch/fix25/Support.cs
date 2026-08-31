global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Text;
global using System.Text.Json;

namespace ServiceLib.Handler;

public static class StringExtensions
{
    public static bool IsNullOrEmpty(this string? value) => string.IsNullOrEmpty(value);
    public static bool IsNotEmpty(this string? value) => !string.IsNullOrEmpty(value);
}

public static class Utils
{
    public static bool TryBase64Decode(string input, out string decoded)
    {
        decoded = string.Empty;
        try
        {
            var normalized = input.Replace('-', '+').Replace('_', '/');
            normalized += new string('=', (4 - normalized.Length % 4) % 4);
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(normalized));
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public static class AmneziaWgManager
{
    public static string GetCanonicalSubscriptionProfileId(string profileId, string content)
    {
        _ = content;
        return profileId;
    }
}
