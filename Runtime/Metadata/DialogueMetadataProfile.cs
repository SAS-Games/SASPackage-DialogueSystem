using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class DialogueParticipantTagBinding
{
    [SerializeField] private string m_Role;
    [SerializeField] private string m_IdTag;
    [SerializeField] private string m_NameTag;
    [SerializeField] private string m_PortraitTag;
    [SerializeField] private string m_AnimationTag;

    public DialogueParticipantTagBinding(string role, string idTag, string nameTag = null, string portraitTag = null, string animationTag = null)
    {
        m_Role = role;
        m_IdTag = idTag;
        m_NameTag = nameTag;
        m_PortraitTag = portraitTag;
        m_AnimationTag = animationTag;
    }

    internal DialogueParticipantTagSchema BuildSchema()
    {
        return new DialogueParticipantTagSchema(m_Role, m_IdTag, m_NameTag, m_PortraitTag, m_AnimationTag);
    }
}

[Serializable]
public sealed class DialogueGenericParticipantTagBinding
{
    [SerializeField] private bool m_Enabled = true;
    [SerializeField] private string m_Prefix = "participant.";
    [SerializeField] private string m_NameSuffix = ".name";
    [SerializeField] private string m_PortraitSuffix = ".portrait";
    [SerializeField] private string m_AnimationSuffix = ".animation";

    internal DialogueGenericParticipantTagSchema BuildSchema()
    {
        return new DialogueGenericParticipantTagSchema(m_Enabled, m_Prefix, m_NameSuffix, m_PortraitSuffix, m_AnimationSuffix);
    }
}

[CreateAssetMenu(fileName = "Dialogue Metadata Profile", menuName = "Dialogue/Metadata Profile")]
public sealed class DialogueMetadataProfile : ScriptableObject
{
    [Header("Scalar semantics")] [SerializeField]
    private string m_LineIdTag = "id";

    [SerializeField] private string m_LocalizationTag = "locale";
    [SerializeField] private string m_LayoutTag = "layout";
    [SerializeField] private string m_AudioTag = "audio";
    [SerializeField] private string m_StorySkipTag = "skip";

    [Header("Participant semantics")] [Tooltip("Participant role exposed as CurrentSpeakerId.")] [SerializeField]
    private string m_CurrentSpeakerRole = "speaker";

    [Tooltip("Participant role exposed as ListenerId.")] [SerializeField]
    private string m_ListenerRole = "listener";

    [SerializeField] private List<DialogueParticipantTagBinding> m_Participants = new()
    {
        new DialogueParticipantTagBinding("speaker", "speaker", "speaker_name", "portrait", "animation"),
        new DialogueParticipantTagBinding("listener", "listener", "listener_name", "listener_portrait", "listener_animation")
    };

    [Header("Dynamic participant roles")] [SerializeField]
    private DialogueGenericParticipantTagBinding m_GenericParticipants = new();

    [NonSerialized] private DialogueMetadataSchema _cachedSchema;

    public DialogueMetadataSchema GetSchema()
    {
        return _cachedSchema ??= new DialogueMetadataSchema(m_LineIdTag, m_LocalizationTag, m_LayoutTag, m_AudioTag, m_CurrentSpeakerRole, m_ListenerRole, (m_Participants ?? new List<DialogueParticipantTagBinding>())
            .Where(binding => binding != null)
            .Select(binding => binding.BuildSchema()), (m_GenericParticipants ?? new DialogueGenericParticipantTagBinding()).BuildSchema(), m_StorySkipTag);
    }

    private void OnValidate()
    {
        _cachedSchema = null;
    }
}

public sealed class DialogueParticipantTagSchema
{
    public DialogueParticipantTagSchema(string role, string idTag, string nameTag = null, string portraitTag = null, string animationTag = null)
    {
        Role = DialogueMetadataSchema.Trim(role);
        IdTag = DialogueMetadataSchema.Trim(idTag);
        NameTag = DialogueMetadataSchema.Trim(nameTag);
        PortraitTag = DialogueMetadataSchema.Trim(portraitTag);
        AnimationTag = DialogueMetadataSchema.Trim(animationTag);
    }

    public string Role { get; }
    public string IdTag { get; }
    public string NameTag { get; }
    public string PortraitTag { get; }
    public string AnimationTag { get; }
}

public sealed class DialogueGenericParticipantTagSchema
{
    public DialogueGenericParticipantTagSchema(bool enabled, string prefix, string nameSuffix, string portraitSuffix, string animationSuffix)
    {
        Enabled = enabled;
        Prefix = DialogueMetadataSchema.Trim(prefix);
        NameSuffix = DialogueMetadataSchema.Trim(nameSuffix);
        PortraitSuffix = DialogueMetadataSchema.Trim(portraitSuffix);
        AnimationSuffix = DialogueMetadataSchema.Trim(animationSuffix);
    }

    public bool Enabled { get; }
    public string Prefix { get; }
    public string NameSuffix { get; }
    public string PortraitSuffix { get; }
    public string AnimationSuffix { get; }
}

/// <summary>
/// Immutable, Unity-object-free metadata mapping used by a dialogue session.
/// </summary>
public sealed class DialogueMetadataSchema
{
    private static readonly Lazy<DialogueMetadataSchema> CanonicalSchema = new(CreateCanonical);
    private readonly IReadOnlyList<DialogueParticipantTagSchema> _participants;

    public DialogueMetadataSchema(string lineIdTag, string localizationTag, string layoutTag, string audioTag, string currentSpeakerRole, string listenerRole, IEnumerable<DialogueParticipantTagSchema> participants, DialogueGenericParticipantTagSchema genericParticipants = null, string storySkipTag = "skip")
    {
        LineIdTag = Trim(lineIdTag);
        LocalizationTag = Trim(localizationTag);
        LayoutTag = Trim(layoutTag);
        AudioTag = Trim(audioTag);
        StorySkipTag = Trim(storySkipTag);
        CurrentSpeakerRole = Trim(currentSpeakerRole);
        ListenerRole = Trim(listenerRole);
        _participants = (participants ?? Enumerable.Empty<DialogueParticipantTagSchema>())
            .Where(binding => binding != null)
            .ToList()
            .AsReadOnly();
        GenericParticipants =
            genericParticipants ?? new DialogueGenericParticipantTagSchema(false, null, null, null, null);

        var errors = Validate().ToArray();
        if (errors.Length > 0)
            throw new ArgumentException("Invalid dialogue metadata profile:\n- " + string.Join("\n- ", errors));
    }

    public static DialogueMetadataSchema Canonical => CanonicalSchema.Value;
    public string LineIdTag { get; }
    public string LocalizationTag { get; }
    public string LayoutTag { get; }
    public string AudioTag { get; }
    public string StorySkipTag { get; }
    public string CurrentSpeakerRole { get; }
    public string ListenerRole { get; }
    public IReadOnlyList<DialogueParticipantTagSchema> Participants => _participants;
    public DialogueGenericParticipantTagSchema GenericParticipants { get; }

    internal static string Trim(string value) => value?.Trim() ?? string.Empty;

    internal static bool IsValidRole(string role)
    {
        return !string.IsNullOrWhiteSpace(role) &&
               char.IsLetter(role[0]) &&
               role.All(character => char.IsLetterOrDigit(character) || character == '_' || character == '-');
    }

    internal static bool IsValidTagKey(string key)
    {
        return !string.IsNullOrWhiteSpace(key) && char.IsLetter(key[0]) &&
               key.All(character => char.IsLetterOrDigit(character) || character == '_' || character == '-' || character == '.');
    }

    private IEnumerable<string> Validate()
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var error in ValidateOptionalTag(LineIdTag, "Line ID", tags)) yield return error;
        foreach (var error in ValidateOptionalTag(LocalizationTag, "Localization", tags)) yield return error;
        foreach (var error in ValidateOptionalTag(LayoutTag, "Layout", tags)) yield return error;
        foreach (var error in ValidateOptionalTag(AudioTag, "Audio", tags)) yield return error;
        foreach (var error in ValidateOptionalTag(StorySkipTag, "Story skip", tags)) yield return error;

        if (!string.IsNullOrEmpty(CurrentSpeakerRole) && !IsValidRole(CurrentSpeakerRole))
            yield return $"Current speaker role '{CurrentSpeakerRole}' is invalid.";
        if (!string.IsNullOrEmpty(ListenerRole) && !IsValidRole(ListenerRole))
            yield return $"Listener role '{ListenerRole}' is invalid.";

        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var participant in Participants)
        {
            if (!IsValidRole(participant.Role))
                yield return $"Participant role '{participant.Role}' is invalid.";
            else if (!roles.Add(participant.Role))
                yield return $"Participant role '{participant.Role}' is configured more than once.";

            if (!IsValidTagKey(participant.IdTag))
                yield return $"Participant '{participant.Role}' requires a valid ID tag.";
            else
            {
                foreach (var error in RegisterTag(participant.IdTag, $"participant '{participant.Role}' ID", tags))
                    yield return error;
            }

            foreach (var error in ValidateOptionalTag(participant.NameTag, $"participant '{participant.Role}' name", tags))
                yield return error;
            foreach (var error in ValidateOptionalTag(participant.PortraitTag, $"participant '{participant.Role}' portrait", tags))
                yield return error;
            foreach (var error in ValidateOptionalTag(participant.AnimationTag, $"participant '{participant.Role}' animation", tags))
                yield return error;
        }

        if (!GenericParticipants.Enabled)
            yield break;

        if (!IsValidGenericPrefix(GenericParticipants.Prefix))
            yield return "Generic participant prefix must be a valid tag prefix ending in '.'.";

        var suffixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in new[]
                 {
                     (GenericParticipants.NameSuffix, "name"),
                     (GenericParticipants.PortraitSuffix, "portrait"),
                     (GenericParticipants.AnimationSuffix, "animation")
                 })
        {
            if (!IsValidSuffix(pair.Item1))
                yield return $"Generic participant {pair.Item2} suffix '{pair.Item1}' is invalid.";
            else if (!suffixes.Add(pair.Item1))
                yield return $"Generic participant suffix '{pair.Item1}' is configured more than once.";
        }
    }

    private static IEnumerable<string> ValidateOptionalTag(string tag, string owner, IDictionary<string, string> registeredTags)
    {
        if (string.IsNullOrEmpty(tag))
            yield break;
        if (!IsValidTagKey(tag))
        {
            yield return $"{owner} tag '{tag}' is invalid.";
            yield break;
        }

        foreach (var error in RegisterTag(tag, owner, registeredTags))
            yield return error;
    }

    private static IEnumerable<string> RegisterTag(string tag, string owner, IDictionary<string, string> registeredTags)
    {
        if (registeredTags.TryGetValue(tag, out var existingOwner))
            yield return $"Tag '{tag}' is assigned to both {existingOwner} and {owner}.";
        else
            registeredTags[tag] = owner;
    }

    private static bool IsValidGenericPrefix(string prefix)
    {
        return !string.IsNullOrEmpty(prefix) && prefix.EndsWith(".", StringComparison.Ordinal) && IsValidTagKey(prefix.Substring(0, prefix.Length - 1));
    }

    private static bool IsValidSuffix(string suffix)
    {
        return !string.IsNullOrEmpty(suffix) && suffix.StartsWith(".", StringComparison.Ordinal) && IsValidTagKey("role" + suffix);
    }

    private static DialogueMetadataSchema CreateCanonical()
    {
        return new DialogueMetadataSchema("id", "locale", "layout", "audio", "speaker", "listener",
            new[]
            {
                new DialogueParticipantTagSchema("speaker", "speaker", "speaker_name", "portrait", "animation"),
                new DialogueParticipantTagSchema("listener", "listener", "listener_name", "listener_portrait", "listener_animation")
            },
            new DialogueGenericParticipantTagSchema(true, "participant.", ".name", ".portrait", ".animation"), "skip");
    }
}