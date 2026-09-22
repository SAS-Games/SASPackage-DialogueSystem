using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

public interface ITypewriterEffect
{
    void StartTyping(string text);
    void Skip();
    void Cancel();
    bool IsRevealing { get; }
    event Action CompleteTextRevealed;
    event Action<char> CharacterRevealed;
}

public interface ITypewriterAudioEffect
{
    void SetCurrentAudioInfo(string id);

    void SetDefaultAudioInfo();
}

[RequireComponent(typeof(TMP_Text))]
public class TypewriterEffect : MonoBehaviour, ITypewriterEffect, ITypewriterAudioEffect
{
    [Header("Typewriter Settings")] [SerializeField]
    private float m_CharactersPerSecond = 20;

    [SerializeField] private float m_InterpunctuationDelay = 0.5f;

    [Header("Completion")] [SerializeField] [Range(0.1f, 0.5f)]
    private float m_SendDoneDelay = 0.25f;

    [Header("Audio")] [SerializeField] private DialogueAudioInfoSO m_DefaultAudioInfo;

    [SerializeField] private DialogueAudioInfoSO[] m_AudioInfos;
    [SerializeField] private bool m_MakePredictable;
    [SerializeField] private bool m_AutoPlay;

    public bool IsRevealing { get; private set; }
    public event Action CompleteTextRevealed;
    public event Action<char> CharacterRevealed;

    private TMP_Text _textBox;
    private Coroutine _typewriterCoroutine;
    private int _presentationVersion;

    private DialogueAudioInfoSO _currentAudioInfo;
    private Dictionary<string, DialogueAudioInfoSO> _audioInfoDictionary;
    private AudioSource _audioSource;

    private void Awake()
    {
        _audioSource = this.gameObject.GetComponentInParent<AudioSource>();
        _currentAudioInfo = m_DefaultAudioInfo;
        _textBox = GetComponent<TMP_Text>();
        InitializeAudioInfoDictionary();
    }

    void Start()
    {
        if (m_AutoPlay)
            StartTyping(_textBox.text);
    }

    private void OnDisable()
    {
        Cancel();
    }

    private void InitializeAudioInfoDictionary()
    {
        _audioInfoDictionary = new Dictionary<string, DialogueAudioInfoSO>();
        if (m_DefaultAudioInfo && !string.IsNullOrEmpty(m_DefaultAudioInfo.id))
            _audioInfoDictionary[m_DefaultAudioInfo.id] = m_DefaultAudioInfo;
        foreach (DialogueAudioInfoSO audioInfo in m_AudioInfos ?? Array.Empty<DialogueAudioInfoSO>())
        {
            if (audioInfo == null || string.IsNullOrEmpty(audioInfo.id))
                continue;

            _audioInfoDictionary[audioInfo.id] = audioInfo;
        }
    }

    public void StartTyping(string text)
    {
        Cancel();

        var presentationVersion = _presentationVersion;
        _textBox.text = text ?? string.Empty;
        _textBox.maxVisibleCharacters = 0;
        _textBox.ForceMeshUpdate();
        IsRevealing = true;
        _typewriterCoroutine = StartCoroutine(Typewriter(presentationVersion));
    }

    public void Skip()
    {
        if (!IsRevealing)
            return;

        if (_typewriterCoroutine != null)
        {
            StopCoroutine(_typewriterCoroutine);
            _typewriterCoroutine = null;
        }

        _textBox.ForceMeshUpdate();
        _textBox.maxVisibleCharacters = _textBox.textInfo.characterCount;
        CompletePresentation(_presentationVersion);
    }

    public void Cancel()
    {
        _presentationVersion++;
        if (_typewriterCoroutine != null)
        {
            StopCoroutine(_typewriterCoroutine);
            _typewriterCoroutine = null;
        }

        IsRevealing = false;
    }

    private IEnumerator Typewriter(int presentationVersion)
    {
        yield return null;
        if (presentationVersion != _presentationVersion)
            yield break;

        _textBox.ForceMeshUpdate();
        var textInfo = _textBox.textInfo;
        var characterDelay = 1f / Mathf.Max(1f, m_CharactersPerSecond);

        for (var characterIndex = 0; characterIndex < textInfo.characterCount; characterIndex++)
        {
            if (presentationVersion != _presentationVersion)
                yield break;

            var character = textInfo.characterInfo[characterIndex].character;
            _textBox.maxVisibleCharacters = characterIndex + 1;
            CharacterRevealed?.Invoke(character);
            PlayDialogueSound(characterIndex, character);

            if (characterIndex + 1 >= textInfo.characterCount)
                continue;

            var delay = IsInterpunctuation(character)
                ? Mathf.Max(0f, m_InterpunctuationDelay)
                : characterDelay;
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);
        }

        if (m_SendDoneDelay > 0f)
            yield return new WaitForSecondsRealtime(m_SendDoneDelay);

        CompletePresentation(presentationVersion);
    }

    private void CompletePresentation(int presentationVersion)
    {
        if (!IsRevealing || presentationVersion != _presentationVersion)
            return;

        IsRevealing = false;
        _typewriterCoroutine = null;
        CompleteTextRevealed?.Invoke();
    }

    private static bool IsInterpunctuation(char character)
    {
        return character == '?' || character == '.' || character == ',' || character == ':' ||
               character == ';' || character == '!' || character == '-';
    }

    private void PlayDialogueSound(int currentDisplayedCharacterCount, char currentCharacter)
    {
        // set variables for the below based on our config
        if (_currentAudioInfo == null)
            return;

        AudioClip[] dialogueTypingSoundClips = _currentAudioInfo.dialogueTypingSoundClips;
        int frequencyLevel = _currentAudioInfo.frequencyLevel;
        float minPitch = _currentAudioInfo.minPitch;
        float maxPitch = _currentAudioInfo.maxPitch;
        bool stopAudioSource = _currentAudioInfo.stopAudioSource;

        if (_audioSource == null || dialogueTypingSoundClips == null || dialogueTypingSoundClips.Length == 0 ||
            frequencyLevel <= 0)
            return;

        // play the sound based on the config
        if (currentDisplayedCharacterCount % frequencyLevel == 0)
        {
            if (stopAudioSource)
                _audioSource.Stop();

            AudioClip soundClip = null;
            // create predictable audio from hashing
            if (m_MakePredictable)
            {
                int hashCode = currentCharacter.GetHashCode();
                // sound clip
                int predictableIndex = hashCode % dialogueTypingSoundClips.Length;
                soundClip = dialogueTypingSoundClips[predictableIndex];
                // pitch
                int minPitchInt = (int)(minPitch * 100);
                int maxPitchInt = (int)(maxPitch * 100);
                int pitchRangeInt = maxPitchInt - minPitchInt;
                // cannot divide by 0, so if there is no range then skip the selection
                if (pitchRangeInt != 0)
                {
                    int predictablePitchInt = (hashCode % pitchRangeInt) + minPitchInt;
                    float predictablePitch = predictablePitchInt / 100f;
                    _audioSource.pitch = predictablePitch;
                }
                else
                    _audioSource.pitch = minPitch;
            }
            // otherwise, randomize the audio
            else
            {
                // sound clip
                int randomIndex = Random.Range(0, dialogueTypingSoundClips.Length);
                soundClip = dialogueTypingSoundClips[randomIndex];
                // pitch
                _audioSource.pitch = Random.Range(minPitch, maxPitch);
            }

            // play sound
            _audioSource.PlayOneShot(soundClip);
        }
    }

    void ITypewriterAudioEffect.SetCurrentAudioInfo(string id)
    {
        DialogueAudioInfoSO audioInfo = null;
        _audioInfoDictionary?.TryGetValue(id, out audioInfo);
        if (audioInfo != null)
            this._currentAudioInfo = audioInfo;
        else
        {
            _currentAudioInfo = m_DefaultAudioInfo;
            Debug.LogWarning("Failed to find audio info for id: " + id);
        }
    }

    void ITypewriterAudioEffect.SetDefaultAudioInfo()
    {
        if (m_DefaultAudioInfo == null)
        {
            _currentAudioInfo = null;
            return;
        }

        (this as ITypewriterAudioEffect)?.SetCurrentAudioInfo(m_DefaultAudioInfo.id);
    }

    private void OnValidate()
    {
        m_CharactersPerSecond = Mathf.Max(1f, m_CharactersPerSecond);
        m_InterpunctuationDelay = Mathf.Max(0f, m_InterpunctuationDelay);
    }
}