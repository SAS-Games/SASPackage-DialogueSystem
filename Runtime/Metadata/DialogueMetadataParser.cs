using System;
using System.Collections.Generic;
using System.Linq;

public static class DialogueMetadataParser
{
    public static DialogueLineContext ParseLine(string text, IEnumerable<string> rawTags, DialogueMetadataSchema schema)
    {
        if (schema == null)
            throw new ArgumentNullException(nameof(schema));

        var tags = rawTags?.ToList() ?? new List<string>();
        var lineContext = new DialogueLineContext(text, tags, schema.CurrentSpeakerRole, schema.ListenerRole);
        Apply(lineContext, tags, schema);
        return lineContext;
    }

    public static void Apply(DialogueLineContext lineContext, IEnumerable<string> rawTags, DialogueMetadataSchema schema)
    {
        if (lineContext == null)
            throw new ArgumentNullException(nameof(lineContext));
        if (schema == null)
            throw new ArgumentNullException(nameof(schema));

        foreach (var rawTag in rawTags ?? Enumerable.Empty<string>())
        {
            if (TryParseTag(rawTag, out var key, out var value))
                lineContext.AddTag(key, value);
        }

        ApplyScalarMetadata(lineContext, schema);
        ApplyParticipants(lineContext, schema);
    }

    private static void ApplyScalarMetadata(DialogueLineContext lineContext, DialogueMetadataSchema schema)
    {
        ApplyScalar(lineContext, schema.LineIdTag, lineContext.SetLineId, validateIdentifier: true);
        ApplyScalar(lineContext, schema.LocalizationTag, lineContext.SetLocale, validateIdentifier: true);
        ApplyScalar(lineContext, schema.LayoutTag, lineContext.SetLayoutAnim, validateIdentifier: false);
        ApplyScalar(lineContext, schema.AudioTag, lineContext.SetAudioInfo, validateIdentifier: false);
        ApplyStorySkipDirective(lineContext, schema.StorySkipTag);
    }

    private static void ApplyStorySkipDirective(DialogueLineContext lineContext, string key)
    {
        if (string.IsNullOrEmpty(key))
            return;

        var values = lineContext.GetTagValues(key);
        if (values.Count == 0)
            return;

        AddDuplicateDiagnostic(lineContext, key, values.Count);
        var value = values[values.Count - 1]?.Trim();
        if (string.Equals(value, "enable", StringComparison.OrdinalIgnoreCase))
        {
            lineContext.SetStorySkipDirective(DialogueStorySkipDirective.Enable);
            return;
        }

        if (string.Equals(value, "disable", StringComparison.OrdinalIgnoreCase))
        {
            lineContext.SetStorySkipDirective(DialogueStorySkipDirective.Disable);
            return;
        }

        lineContext.AddDiagnostic(new DialogueMetadataDiagnostic(DialogueMetadataSeverity.Warning, "invalid-skip-directive", $"Metadata '{key}' must be 'enable' or 'disable'.", key));
    }

    private static void ApplyScalar(DialogueLineContext lineContext, string key, Action<string> apply, bool validateIdentifier)
    {
        if (string.IsNullOrEmpty(key))
            return;

        var values = lineContext.GetTagValues(key);
        if (values.Count == 0)
            return;

        AddDuplicateDiagnostic(lineContext, key, values.Count);
        var value = values[values.Count - 1];
        if (string.IsNullOrWhiteSpace(value))
            lineContext.AddDiagnostic(new DialogueMetadataDiagnostic(DialogueMetadataSeverity.Warning, "empty-value", $"Metadata '{key}' has an empty value.", key));
        else if (validateIdentifier && !IsValidIdentifier(value))
            lineContext.AddDiagnostic(new DialogueMetadataDiagnostic(DialogueMetadataSeverity.Error, "invalid-identifier", $"Metadata '{key}' must start with a letter or number and contain only letters, numbers, dots, hyphens, or underscores.", key));

        apply(value);
    }

    private static void ApplyParticipants(DialogueLineContext lineContext, DialogueMetadataSchema schema)
    {
        var participants = new List<ParticipantDefinition>();
        var configuredRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var binding in schema.Participants)
        {
            configuredRoles.Add(binding.Role);
            RequireParticipantIdForDetails(lineContext, binding.IdTag, binding.NameTag, binding.PortraitTag, binding.AnimationTag);

            if (lineContext.HasTag(binding.IdTag))
                participants.Add(new ParticipantDefinition(binding));
        }

        var generic = schema.GenericParticipants;
        if (generic.Enabled)
        {
            foreach (var key in lineContext.Tags.Keys.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
            {
                if (!TryParseGenericParticipantKey(key, generic, out var role, out var field))
                {
                    if (key.StartsWith(generic.Prefix, StringComparison.OrdinalIgnoreCase))
                        lineContext.AddDiagnostic(new DialogueMetadataDiagnostic(DialogueMetadataSeverity.Error, "invalid-participant-field", $"Participant metadata '{key}' is not a supported participant field.", key));

                    continue;
                }

                if (!DialogueMetadataSchema.IsValidRole(role))
                {
                    lineContext.AddDiagnostic(new DialogueMetadataDiagnostic(DialogueMetadataSeverity.Error, "invalid-participant-role", $"Participant role '{role}' is invalid.", key));
                    continue;
                }

                if (configuredRoles.Contains(role))
                {
                    lineContext.AddDiagnostic(new DialogueMetadataDiagnostic(DialogueMetadataSeverity.Error, "reserved-participant-role", $"Participant role '{role}' has an explicit profile binding; use its configured tag fields.", key));
                    continue;
                }

                var baseKey = generic.Prefix + role;
                if (!string.IsNullOrEmpty(field))
                {
                    if (!lineContext.HasTag(baseKey))
                        lineContext.AddDiagnostic(new DialogueMetadataDiagnostic(DialogueMetadataSeverity.Error, "participant-id-missing", $"Participant role '{role}' has '{field}' metadata but no '{baseKey}' character ID.", key));

                    continue;
                }

                if (participants.Any(item => item.Role.Equals(role, StringComparison.OrdinalIgnoreCase)))
                    continue;

                participants.Add(new ParticipantDefinition(role, baseKey, baseKey + generic.NameSuffix, baseKey + generic.PortraitSuffix, baseKey + generic.AnimationSuffix));
            }
        }

        foreach (var participant in participants)
            ApplyParticipant(lineContext, participant);
    }

    private static void RequireParticipantIdForDetails(DialogueLineContext lineContext, string idKey, params string[] detailKeys)
    {
        if (lineContext.HasTag(idKey))
            return;

        foreach (var detailKey in detailKeys)
        {
            if (string.IsNullOrEmpty(detailKey) || !lineContext.HasTag(detailKey))
                continue;

            lineContext.AddDiagnostic(new DialogueMetadataDiagnostic(DialogueMetadataSeverity.Error, "participant-id-missing", $"Participant metadata '{detailKey}' requires a '{idKey}' character ID.", detailKey));
        }
    }

    private static void ApplyParticipant(DialogueLineContext lineContext, ParticipantDefinition definition)
    {
        var ids = lineContext.GetTagValues(definition.IdKey);
        AddDuplicateDiagnostic(lineContext, definition.IdKey, ids.Count);
        if (ids.Count == 0)
            return;

        var participantId = ids[ids.Count - 1]?.Trim();
        if (string.IsNullOrWhiteSpace(participantId))
        {
            lineContext.AddDiagnostic(new DialogueMetadataDiagnostic(DialogueMetadataSeverity.Error, "participant-id-empty", $"Participant role '{definition.Role}' requires a character ID.", definition.IdKey));
            return;
        }

        if (!IsValidIdentifier(participantId))
        {
            lineContext.AddDiagnostic(new DialogueMetadataDiagnostic(DialogueMetadataSeverity.Error, "invalid-participant-id", $"Character ID '{participantId}' for role '{definition.Role}' is invalid.", definition.IdKey));
            return;
        }

        lineContext.SetParticipant(new DialogueParticipant(definition.Role, participantId, ReadOptionalField(lineContext, definition.NameKey), ReadOptionalField(lineContext, definition.PortraitKey), ReadOptionalField(lineContext, definition.AnimationKey)));
    }

    private static string ReadOptionalField(DialogueLineContext lineContext, string key)
    {
        if (string.IsNullOrEmpty(key))
            return string.Empty;

        var values = lineContext.GetTagValues(key);
        AddDuplicateDiagnostic(lineContext, key, values.Count);
        return values.Count > 0 ? values[values.Count - 1] : string.Empty;
    }

    private static void AddDuplicateDiagnostic(DialogueLineContext lineContext, string key, int count)
    {
        if (count <= 1)
            return;

        lineContext.AddDiagnostic(new DialogueMetadataDiagnostic(DialogueMetadataSeverity.Warning, "duplicate-field", $"Metadata '{key}' occurs {count} times; the final value is used.", key));
    }

    private static bool TryParseGenericParticipantKey(string key, DialogueGenericParticipantTagSchema schema, out string role, out string field)
    {
        role = string.Empty;
        field = string.Empty;
        if (string.IsNullOrWhiteSpace(key) || !key.StartsWith(schema.Prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var remainder = key.Substring(schema.Prefix.Length);
        var suffixes = new[]
            {
                (schema.NameSuffix, "name"),
                (schema.PortraitSuffix, "portrait"),
                (schema.AnimationSuffix, "animation")
            }
            .OrderByDescending(item => item.Item1.Length);

        foreach (var suffix in suffixes)
        {
            if (!remainder.EndsWith(suffix.Item1, StringComparison.OrdinalIgnoreCase))
                continue;

            role = remainder.Substring(0, remainder.Length - suffix.Item1.Length);
            field = suffix.Item2;
            return !string.IsNullOrWhiteSpace(role);
        }

        if (remainder.IndexOf('.') >= 0)
            return false;

        role = remainder;
        return !string.IsNullOrWhiteSpace(role);
    }

    private static bool IsValidIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !char.IsLetterOrDigit(value[0]))
            return false;

        return value.All(character => char.IsLetterOrDigit(character) || character == '_' || character == '-' || character == '.');
    }

    private static bool TryParseTag(string rawTag, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(rawTag))
            return false;

        var tag = rawTag.Trim();
        if (tag.StartsWith("#", StringComparison.Ordinal))
            tag = tag.Substring(1).Trim();
        if (tag.Length == 0)
            return false;

        var separatorIndex = tag.IndexOf(':');
        key = (separatorIndex < 0 ? tag : tag.Substring(0, separatorIndex)).Trim();
        if (key.Length == 0)
            return false;

        if (separatorIndex >= 0)
            value = tag.Substring(separatorIndex + 1).Trim();
        return true;
    }

    private readonly struct ParticipantDefinition
    {
        public ParticipantDefinition(DialogueParticipantTagSchema schema) : this(schema.Role, schema.IdTag, schema.NameTag, schema.PortraitTag, schema.AnimationTag) { }

        public ParticipantDefinition(string role, string idKey, string nameKey, string portraitKey, string animationKey)
        {
            Role = role;
            IdKey = idKey;
            NameKey = nameKey;
            PortraitKey = portraitKey;
            AnimationKey = animationKey;
        }

        public string Role { get; }
        public string IdKey { get; }
        public string NameKey { get; }
        public string PortraitKey { get; }
        public string AnimationKey { get; }
    }
}