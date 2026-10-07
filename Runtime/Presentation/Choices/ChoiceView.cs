using System;
using System.Collections.Generic;
using SAS.DialogueSystem;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.UI;

public class ChoiceView : MonoBehaviour
{
    [SerializeField] private Button m_Button;
    [SerializeField] private TMP_Text m_Text;
    [SerializeField] private string m_LocalizedTableName = "DialogueTextTable";
    private UnityAction _boundSelectedAction;
    private DialogueLocalizedStringHandle _activeLocalization;
    private LocalizedString.ChangeHandler _localizedStringHandler;
    private int _localizationVersion;

    private void OnDisable() => CancelLocalization();

    private void OnDestroy()
    {
        CancelLocalization();
        ClearSelectedEvents();
    }

    public void SetText(string text)
    {
        CancelLocalization();
        ApplyText(text);
    }

    private void ApplyText(string text)
    {
        if (m_Text != null)
            m_Text.text = text;
    }

    public void SetLocalText(string id)
    {
        SetLocalText(id, string.Empty, null);
    }

    public void SetLocalText(string id, string fallbackText)
    {
        SetLocalText(id, fallbackText, null);
    }

    public void SetLocalText(string id, string fallbackText,
        IReadOnlyList<DialogueLocalizationArgument> localizationArguments)
    {
        if (string.IsNullOrEmpty(id))
        {
            SetText(fallbackText);
            return;
        }

        CancelLocalization();
        ApplyText(fallbackText);
        var version = _localizationVersion;
        _activeLocalization = new DialogueLocalizedStringHandle(m_LocalizedTableName, id,
            localizationArguments);
        _localizedStringHandler = localizedText =>
        {
            if (version == _localizationVersion)
                ApplyText(localizedText);
        };
        _activeLocalization.Reference.StringChanged += _localizedStringHandler;
    }

    public void BindSelectedEvent(UnityAction<int> action, int parameter)
    {
        if (m_Button == null)
            return;

        ClearSelectedEvents();
        if (action == null)
            return;

        _boundSelectedAction = () => action(parameter);
        m_Button.onClick.AddListener(_boundSelectedAction);
    }

    public void ClearSelectedEvents()
    {
        if (m_Button != null && _boundSelectedAction != null)
            m_Button.onClick.RemoveListener(_boundSelectedAction);
        _boundSelectedAction = null;
    }

    private void CancelLocalization()
    {
        _localizationVersion++;
        if (_activeLocalization != null && _localizedStringHandler != null)
            _activeLocalization.Reference.StringChanged -= _localizedStringHandler;

        _activeLocalization?.Dispose();
        _activeLocalization = null;
        _localizedStringHandler = null;
    }
}
