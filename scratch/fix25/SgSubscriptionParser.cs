namespace ServiceLib.Handler;

public sealed class SgSubscriptionConfigRecord
{
    public string SourceKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
}

public sealed class SgSubscriptionEnvelope
{
    public bool IsSgEnvelope { get; init; }
    public string UriPayload { get; init; } = string.Empty;
    public IReadOnlyList<SgSubscriptionConfigRecord> ConfigRecords { get; init; } = [];
}

public static class SgSubscriptionParser
{
    private const string Header = "# SG-SUBSCRIPTION/1";
    private const string ConfigPrefix = "# SG-CONFIG ";
    private const string ClientPrefix = "# client=";

    public static SgSubscriptionEnvelope Parse(string? content)
    {
        var text = (content ?? string.Empty)
            .Trim()
            .TrimStart('\uFEFF', '\u200B');
        if (text.IsNullOrEmpty())
        {
            return new SgSubscriptionEnvelope
            {
                IsSgEnvelope = false,
                UriPayload = string.Empty
            };
        }

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var isSgEnvelope = lines.Any(line =>
            string.Equals(line.Trim(), Header, StringComparison.OrdinalIgnoreCase));
        if (!isSgEnvelope)
        {
            return new SgSubscriptionEnvelope
            {
                IsSgEnvelope = false,
                UriPayload = text
            };
        }

        var clientLine = lines
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.StartsWith(ClientPrefix, StringComparison.OrdinalIgnoreCase));
        var clientName = clientLine.IsNotEmpty()
            ? clientLine![ClientPrefix.Length..].Trim()
            : string.Empty;

        var uriLines = new List<string>();
        var records = new List<SgSubscriptionConfigRecord>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.IsNullOrEmpty())
            {
                continue;
            }

            if (line.StartsWith(ConfigPrefix, StringComparison.Ordinal))
            {
                var json = line[ConfigPrefix.Length..].Trim();
                records.Add(ParseConfigRecord(json, clientName, keys));
                continue;
            }

            // SG metadata is commentary for the ordinary URI importer. Only
            // actual URI records are passed to the established batch importer.
            if (!line.StartsWith('#') && line.Contains("://", StringComparison.Ordinal))
            {
                uriLines.Add(line);
            }
        }

        return new SgSubscriptionEnvelope
        {
            IsSgEnvelope = true,
            UriPayload = string.Join(Environment.NewLine, uriLines),
            ConfigRecords = records
        };
    }

    private static SgSubscriptionConfigRecord ParseConfigRecord(
        string json,
        string clientName,
        HashSet<string> keys)
    {
        if (json.IsNullOrEmpty())
        {
            throw new InvalidDataException("SG-CONFIG не содержит метаданных.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (Exception ex)
        {
            throw new InvalidDataException("SG-CONFIG содержит некорректный JSON.", ex);
        }

        using (document)
        {
            var root = document.RootElement;
            var profileId = ReadString(root, "profile").ToLowerInvariant();
            var profileName = ReadString(root, "name");
            var deviceId = ReadScalar(root, "device_id");
            var deviceName = ReadString(root, "device");
            var encoding = ReadString(root, "encoding");
            var data = ReadString(root, "data");
            var primary = root.TryGetProperty("primary", out var primaryElement)
                && primaryElement.ValueKind is JsonValueKind.True;

            if (profileId != "amneziawg" && profileId != "amneziawg3" && profileId != "amneziawg31")
            {
                throw new InvalidDataException($"SG-CONFIG содержит неподдерживаемый профиль: {profileId}.");
            }
            if (deviceId.IsNullOrEmpty())
            {
                throw new InvalidDataException($"SG-CONFIG {profileId} не содержит device_id.");
            }
            if (!string.Equals(encoding, "base64url", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"SG-CONFIG {profileId} использует неподдерживаемое кодирование: {encoding}.");
            }
            if (data.IsNullOrEmpty() || !Utils.TryBase64Decode(data, out var decoded))
            {
                throw new InvalidDataException($"SG-CONFIG {profileId} не декодируется как base64url UTF-8.");
            }

            // Do not implement a second AWG parser here. The decoded complete
            // config is classified by AmneziaWgManager, the same component used
            // by the normal AmneziaWG import path. The content, not an old server
            // alias, determines the stable subscription key.
            var canonicalProfileId = AmneziaWgManager.GetCanonicalSubscriptionProfileId(profileId, decoded);
            var sourceKey = $"{deviceId}:{canonicalProfileId}";
            if (!keys.Add(sourceKey))
            {
                throw new InvalidDataException($"SG-CONFIG содержит дублирующую запись {sourceKey}.");
            }

            // The device segment is optional display metadata. Primary devices and
            // unnamed records must not acquire synthetic labels such as
            // "Основное устройство" or "Устройство". A real secondary-device
            // name remains visible.
            var deviceLabel = primary ? string.Empty : deviceName;
            var profileLabel = profileName.IsNotEmpty() ? profileName : profileId;
            var displayName = string.Join(" · ", new[] { clientName, deviceLabel, profileLabel }
                .Where(value => value.IsNotEmpty()));

            return new SgSubscriptionConfigRecord
            {
                SourceKey = sourceKey,
                DisplayName = displayName.IsNotEmpty() ? displayName : profileLabel,
                Content = decoded
            };
        }
    }

    private static string ReadString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return string.Empty;
        }
        return value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() ?? string.Empty
            : string.Empty;
    }

    private static string ReadScalar(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return string.Empty;
        }
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()?.Trim() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            _ => string.Empty,
        };
    }
}
