using System;
using System.Collections.Generic;
using System.Linq;

public enum DialogueMetadataSeverity
{
    Warning,
    Error
}

public enum DialogueStorySkipDirective
{
    Unchanged,
    Enable,
    Disable
}

public enum DialoguePlacementMode
{
    FollowSpeaker,
    FixedCharacter
}

public enum DialoguePlacementDirective
{
    Unchanged,
    FollowSpeaker,
    FixedCharacter
}

public enum DialogueLocalizationArgumentType
{
    Integer,
    Float,
    Boolean,
    String,
    Localized
}

public sealed class DialogueLocalizationArgument
{
    internal DialogueLocalizationArgument(string name, DialogueLocalizationArgumentType type, object value = null,
        string tableName = null, string entryKey = null)
    {
        Name = name ?? string.Empty;
        Type = type;
        Value = value;
        TableName = tableName ?? string.Empty;
        EntryKey = entryKey ?? string.Empty;
    }

    public string Name { get; }
    public DialogueLocalizationArgumentType Type { get; }
    public object Value { get; }
    public string TableName { get; }
    public string EntryKey { get; }
}

public sealed class DialogueMetadataDiagnostic
{
    public DialogueMetadataDiagnostic(DialogueMetadataSeverity severity, string code, string message, string key = null)
    {
        Severity = severity;
        Code = code ?? string.Empty;
        Message = message ?? string.Empty;
        Key = key ?? string.Empty;
    }

    public DialogueMetadataSeverity Severity { get; }
    public string Code { get; }
    public string Message { get; }
    public string Key { get; }
}

public sealed class DialogueParticipant
{
    public DialogueParticipant(string role, string characterId, string displayName = null, string portraitKey = null, string animationKey = null)
    {
        Role = role?.Trim() ?? string.Empty;
        CharacterId = characterId?.Trim() ?? string.Empty;
        DisplayName = displayName?.Trim() ?? string.Empty;
        PortraitKey = portraitKey?.Trim() ?? string.Empty;
        AnimationKey = animationKey?.Trim() ?? string.Empty;
    }

    public string Role { get; }
    public string CharacterId { get; }
    public string DisplayName { get; }
    public string PortraitKey { get; }
    public string AnimationKey { get; }
}

public sealed class DialoguePresentationParticipant
{
    public DialoguePresentationParticipant(string slotId, string characterId, bool isCurrentSpeaker,
        string displayName = null, string portraitKey = null, string animationKey = null,
        string sourceRole = null)
    {
        SlotId = slotId?.Trim() ?? string.Empty;
        CharacterId = characterId?.Trim() ?? string.Empty;
        IsCurrentSpeaker = isCurrentSpeaker;
        DisplayName = displayName?.Trim() ?? string.Empty;
        PortraitKey = portraitKey?.Trim() ?? string.Empty;
        AnimationKey = animationKey?.Trim() ?? string.Empty;
        SourceRole = sourceRole?.Trim() ?? string.Empty;
    }

    public string SlotId { get; }
    public string CharacterId { get; }
    public bool IsCurrentSpeaker { get; }
    public string DisplayName { get; }
    public string PortraitKey { get; }
    public string AnimationKey { get; }
    public string SourceRole { get; }
}

public sealed class DialogueLineContext
{
    private readonly List<string> _rawTags;
    private readonly Dictionary<string, List<string>> _tags = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<DialogueParticipant> _participants = new();
    private readonly Dictionary<string, DialogueParticipant> _participantsByRole = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _slotAssignments = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<DialoguePresentationParticipant> _presentationParticipants = new();
    private readonly List<DialogueLocalizationArgument> _localizationArguments = new();
    private readonly List<DialogueMetadataDiagnostic> _diagnostics = new();
    private readonly string _currentSpeakerRole;
    private readonly string _listenerRole;

    internal DialogueLineContext(string text, IEnumerable<string> rawTags, string currentSpeakerRole, string listenerRole)
    {
        Text = text ?? string.Empty;
        _rawTags = rawTags?.ToList() ?? new List<string>();
        _currentSpeakerRole = currentSpeakerRole?.Trim() ?? string.Empty;
        _listenerRole = listenerRole?.Trim() ?? string.Empty;
    }

    public string Text { get; }
    public IReadOnlyList<string> RawTags => _rawTags;
    public IReadOnlyDictionary<string, List<string>> Tags => _tags;
    public IReadOnlyList<DialogueParticipant> Participants => _participants;
    public IReadOnlyDictionary<string, string> SlotAssignments => _slotAssignments;
    public IReadOnlyList<DialoguePresentationParticipant> PresentationParticipants => _presentationParticipants;
    public IReadOnlyList<DialogueLocalizationArgument> LocalizationArguments => _localizationArguments;
    public IReadOnlyList<DialogueMetadataDiagnostic> Diagnostics => _diagnostics;
    public bool HasErrors => _diagnostics.Any(item => item.Severity == DialogueMetadataSeverity.Error);
    public string LineId { get; private set; } = string.Empty;
    public string CurrentSpeakerRole => _currentSpeakerRole;
    public string ListenerRole => _listenerRole;
    public string CurrentSpeakerId => GetParticipantId(_currentSpeakerRole);
    public string ListenerId => GetParticipantId(_listenerRole);
    public string Locale { get; private set; } = string.Empty;
    public string LayoutAnim { get; private set; } = string.Empty;
    public string AudioInfoId { get; private set; } = string.Empty;
    public DialogueStorySkipDirective StorySkipDirective { get; private set; }
    public DialoguePlacementDirective PlacementDirective { get; private set; }
    public DialoguePlacementMode PlacementMode { get; private set; } = DialoguePlacementMode.FollowSpeaker;

    internal void SetParticipant(DialogueParticipant participant)
    {
        if (participant == null || string.IsNullOrWhiteSpace(participant.Role) || string.IsNullOrWhiteSpace(participant.CharacterId))
            return;

        if (_participantsByRole.TryGetValue(participant.Role, out var existing))
        {
            var index = _participants.IndexOf(existing);
            if (index >= 0)
                _participants[index] = participant;
        }
        else
            _participants.Add(participant);

        _participantsByRole[participant.Role] = participant;
    }

    internal void SetLineId(string lineId) => LineId = lineId?.Trim() ?? string.Empty;
    internal void SetLocale(string locale) => Locale = locale?.Trim() ?? string.Empty;
    internal void SetLayoutAnim(string layoutAnim) => LayoutAnim = layoutAnim?.Trim() ?? string.Empty;
    internal void SetAudioInfo(string audioInfoId) => AudioInfoId = audioInfoId?.Trim() ?? string.Empty;
    internal void SetStorySkipDirective(DialogueStorySkipDirective directive) => StorySkipDirective = directive;
    internal void SetPlacementDirective(DialoguePlacementDirective directive) => PlacementDirective = directive;

    internal void SetSlotAssignment(string slotId, string characterId)
    {
        if (!string.IsNullOrWhiteSpace(slotId))
            _slotAssignments[slotId.Trim()] = characterId?.Trim() ?? string.Empty;
    }

    internal void SetPresentation(DialoguePlacementMode placementMode,
        IEnumerable<DialoguePresentationParticipant> participants)
    {
        PlacementMode = placementMode;
        _presentationParticipants.Clear();
        if (participants != null)
            _presentationParticipants.AddRange(participants.Where(item => item != null));
    }

    internal void ResolveRoleBasedPresentation()
    {
        SetPresentation(DialoguePlacementMode.FollowSpeaker, _participants.Select(participant =>
            new DialoguePresentationParticipant(participant.Role, participant.CharacterId,
                participant.Role.Equals(_currentSpeakerRole, StringComparison.OrdinalIgnoreCase),
                participant.DisplayName, participant.PortraitKey, participant.AnimationKey, participant.Role)));
    }

    internal void AddTag(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        key = key.Trim();
        if (!_tags.TryGetValue(key, out var values))
        {
            values = new List<string>();
            _tags[key] = values;
        }

        values.Add(value?.Trim() ?? string.Empty);
    }

    internal void AddDiagnostic(DialogueMetadataDiagnostic diagnostic)
    {
        if (diagnostic != null)
            _diagnostics.Add(diagnostic);
    }

    internal bool SetLocalizationArgument(DialogueLocalizationArgument argument)
    {
        if (argument == null)
            return true;

        var index = _localizationArguments.FindIndex(item =>
            item.Name.Equals(argument.Name, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            _localizationArguments.Add(argument);
            return true;
        }

        _localizationArguments[index] = argument;
        return false;
    }

    public bool HasTag(string key)
    {
        return !string.IsNullOrWhiteSpace(key) && _tags.ContainsKey(key.Trim());
    }

    public bool TryGetTagValue(string key, out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(key) || !_tags.TryGetValue(key.Trim(), out var values) || values.Count == 0)
            return false;

        value = values[values.Count - 1];
        return true;
    }

    public IReadOnlyList<string> GetTagValues(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || !_tags.TryGetValue(key.Trim(), out var values))
            return Array.Empty<string>();

        return values;
    }

    public string GetParticipantId(string role)
    {
        return TryGetParticipant(role, out var participant) ? participant.CharacterId : string.Empty;
    }

    public bool TryGetParticipant(string role, out DialogueParticipant participant)
    {
        participant = null;
        return !string.IsNullOrWhiteSpace(role) && _participantsByRole.TryGetValue(role.Trim(), out participant);
    }
}
