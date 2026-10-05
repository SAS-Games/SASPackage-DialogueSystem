using System;
using System.Collections.Generic;
using SAS.Core.TagSystem;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

public class SpeakerView : MonoBehaviour
{
    [SerializeField] private TMP_Text m_DisplayNameText;
    [SerializeField] private Animator m_PortraitAnimator;
    [SerializeField] private Image m_Image;
    [SerializeField] private DialogueCharacterCatalog m_CharacterCatalog;
    [SerializeField] private string m_DefaultAnimationState = "Idle";
    [FieldRequiresSelf] private IDialogueAnimationTarget _animationTarget;

    private readonly HashSet<string> _reportedMissingCharacters = new(StringComparer.OrdinalIgnoreCase);
    private LocalizedString _activeLocalizedDisplayName;
    private LocalizedString.ChangeHandler _localizedDisplayNameHandler;
    private int _displayNameVersion;

    void Awake()
    {
        this.Initialize();
    }

    private void OnDisable()
    {
        CancelDisplayNameLocalization();
    }

    private void OnDestroy()
    {
        CancelDisplayNameLocalization();
    }

    public void SetName(string name)
    {
        if (m_DisplayNameText != null)
            m_DisplayNameText.text = name;
    }

    public void SetImage(Sprite sprite)
    {
        if (m_Image != null)
            m_Image.sprite = sprite;
    }

    public void SetAnimationState(string stateName)
    {
        if (!string.IsNullOrEmpty(stateName))
        {
            if (m_PortraitAnimator)
                m_PortraitAnimator.Play(stateName);
            _animationTarget?.Process(stateName);
        }
    }

    public void SetParticipant(DialogueParticipant participant)
    {
        if (participant == null)
            return;

        ApplyParticipant(participant.CharacterId, participant.DisplayName, participant.PortraitKey, participant.AnimationKey);
    }

    public void SetParticipant(DialoguePresentationParticipant participant)
    {
        if (participant == null)
            return;

        ApplyParticipant(participant.CharacterId, participant.DisplayName, participant.PortraitKey,
            participant.AnimationKey);
    }

    private void ApplyParticipant(string characterId, string displayName, string portraitKey,
        string animationKey)
    {
        CancelDisplayNameLocalization();

        DialogueCharacterDefinition character = null;
        if (m_CharacterCatalog != null && !m_CharacterCatalog.TryGetCharacter(characterId, out character) &&
            !string.IsNullOrWhiteSpace(characterId) && _reportedMissingCharacters.Add(characterId))
        {
            Debug.LogWarning($"Dialogue character '{characterId}' is not present in the assigned character catalog.", this);
        }

        if (!string.IsNullOrWhiteSpace(displayName))
            SetName(displayName.Trim());
        else if (character != null && character.HasLocalizedDisplayName)
            BeginDisplayNameLocalization(character, characterId);
        else
            SetName(character != null && !string.IsNullOrEmpty(character.DisplayName)
                ? character.DisplayName
                : characterId);

        SetImage(m_CharacterCatalog != null
            ? m_CharacterCatalog.ResolvePortrait(characterId, portraitKey)
            : null);

        var resolvedAnimation = m_CharacterCatalog != null
            ? m_CharacterCatalog.ResolveAnimation(characterId, animationKey)
            : animationKey;
        SetAnimationState(string.IsNullOrEmpty(resolvedAnimation)
            ? m_DefaultAnimationState
            : resolvedAnimation);
    }

    private void BeginDisplayNameLocalization(DialogueCharacterDefinition character, string characterId)
    {
        SetName(!string.IsNullOrEmpty(character.DisplayName) ? character.DisplayName : characterId);
        var version = _displayNameVersion;
        _activeLocalizedDisplayName = character.LocalizedDisplayName;
        _localizedDisplayNameHandler = localizedName =>
        {
            if (version == _displayNameVersion && !string.IsNullOrWhiteSpace(localizedName))
                SetName(localizedName);
        };
        _activeLocalizedDisplayName.StringChanged += _localizedDisplayNameHandler;
    }

    private void CancelDisplayNameLocalization()
    {
        _displayNameVersion++;
        if (_activeLocalizedDisplayName != null && _localizedDisplayNameHandler != null)
            _activeLocalizedDisplayName.StringChanged -= _localizedDisplayNameHandler;

        _activeLocalizedDisplayName = null;
        _localizedDisplayNameHandler = null;
    }
}
