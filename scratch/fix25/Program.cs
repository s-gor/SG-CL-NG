using ServiceLib.Handler;

const string config = "[Interface]\nPrivateKey = test\n[Peer]\nPublicKey = test\n";

static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
    .TrimEnd('=')
    .Replace('+', '-')
    .Replace('/', '_');

static string Record(string? device, bool? primary, string data)
{
    var metadata = new Dictionary<string, object?>
    {
        ["profile"] = "amneziawg",
        ["name"] = "AmneziaWG 2.0",
        ["device_id"] = 42,
        ["device"] = device,
        ["encoding"] = "base64url",
        ["data"] = data,
    };
    if (primary.HasValue)
    {
        metadata["primary"] = primary.Value;
    }
    return "# SG-CONFIG " + JsonSerializer.Serialize(metadata);
}

static void Verify(string name, string text, string expected)
{
    var parsed = SgSubscriptionParser.Parse(text);
    if (!parsed.IsSgEnvelope)
    {
        throw new InvalidOperationException($"{name}: SG envelope not detected");
    }
    if (parsed.ConfigRecords.Count != 1)
    {
        throw new InvalidOperationException($"{name}: expected one AWG record, got {parsed.ConfigRecords.Count}");
    }
    var actual = parsed.ConfigRecords[0].DisplayName;
    if (!string.Equals(actual, expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{name}: expected '{expected}', got '{actual}'");
    }
    if (actual.Contains("Основное устройство", StringComparison.Ordinal)
        || actual.EndsWith(" · Устройство", StringComparison.Ordinal)
        || actual.Contains(" · Устройство · ", StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{name}: synthetic device label survived: '{actual}'");
    }
    Console.WriteLine($"PASS {name}: {actual}");
}

var encoded = Encode(config);
Verify(
    "legacy-primary",
    "# SG-SUBSCRIPTION/1\n# client=Shany\n" + Record("Phone", true, encoded) + "\n",
    "Shany · AmneziaWG 2.0");
Verify(
    "current-empty-device",
    "# SG-SUBSCRIPTION/1\n# client=Shany\n" + Record(string.Empty, null, encoded) + "\n",
    "Shany · AmneziaWG 2.0");
Verify(
    "named-secondary",
    "# SG-SUBSCRIPTION/1\n# client=Shany\n" + Record("Phone", false, encoded) + "\n",
    "Shany · Phone · AmneziaWG 2.0");

Console.WriteLine("FIX25 parser verification completed.");
