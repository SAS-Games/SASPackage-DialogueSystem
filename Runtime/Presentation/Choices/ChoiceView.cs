using System;
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
    private LocalizedString _activeLocalizedString;
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
        SetLocalText(id, string.Empty);
    }

    public void SetLocalText(string id, string fallbackText)
    {
        if (string.IsNullOrEmpty(id))
        {
            SetText(fallbackText);
            return;
        }

        CancelLocalization();
        ApplyText(fallbackText);
        var version = _localizationVersion;
        _activeLocalizedString = new LocalizedString(m_LocalizedTableName, id);
        _localizedStringHandler = localizedText =>
        {
            if (version == _localizationVersion)
                ApplyText(localizedText);
        };
        _activeLocalizedString.StringChanged += _localizedStringHandler;
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
        if (_activeLocalizedString != null && _localizedStringHandler != null)
            _activeLocalizedString.StringChanged -= _localizedStringHandler;

        (_activeLocalizedString as IDisposable)?.Dispose();
        _activeLocalizedString = null;
        _localizedStringHandler = null;
    }
}