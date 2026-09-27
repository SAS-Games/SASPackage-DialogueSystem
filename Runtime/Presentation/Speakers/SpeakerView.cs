using SAS.Core.TagSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpeakerView : MonoBehaviour
{
    [SerializeField] private TMP_Text m_DisplayNameText;
    [SerializeField] private Animator m_PortraitAnimator;
    [SerializeField] private Image m_Image;
    [SerializeField] private ImageKeyMapConfig m_ImageKeyMapConfig;
    [SerializeField] private string m_DefaultAnimationState = "Idle";
    [FieldRequiresSelf] private IDialogueAnimationTarget _animationTarget;

    void Awake()
    {
        this.Initialize();
    }

    public void SetName(string name)
    {
        if (m_DisplayNameText != null)
            m_DisplayNameText.text = name;
    }

    public void SetImage(string spriteName)
    {
        if (string.IsNullOrWhiteSpace(spriteName) || m_ImageKeyMapConfig == null)
        {
            SetImage((Sprite)null);
            return;
        }

        SetImage(m_ImageKeyMapConfig.GetImage(spriteName));
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

        ApplyParticipant(participant.CharacterId, participant.DisplayName, participant.PortraitKey,
            participant.AnimationKey);
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
        SetName(string.IsNullOrEmpty(displayName)
            ? characterId
            : displayName);
        SetImage(string.IsNullOrEmpty(portraitKey)
            ? characterId
            : portraitKey);
        SetAnimationState(string.IsNullOrEmpty(animationKey)
            ? m_DefaultAnimationState
            : animationKey);
    }

    internal void SetDisplayValues(string speakerTag, string animationState)
    {
        SetName(speakerTag);
        SetImage(speakerTag);
        SetAnimationState(animationState);
    }
}