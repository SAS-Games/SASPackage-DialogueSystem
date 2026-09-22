using System;
using System.Collections.Generic;
using Ink.Runtime;
using UnityEngine;
using Object = Ink.Runtime.Object;

public class DialogueGlobalVariables
{
    public Dictionary<string, Object> GlobalVariables { get; private set; }

    private Story _globalVariablesStory;
    private const string DefaultSaveVariablesKey = "INK_VARIABLES";
    private readonly string _saveVariablesKey;

    public DialogueGlobalVariables(TextAsset loadGlobalsJSON, string saveVariablesKey = DefaultSaveVariablesKey)
    {
        _saveVariablesKey = string.IsNullOrWhiteSpace(saveVariablesKey) ? DefaultSaveVariablesKey : saveVariablesKey.Trim();
        GlobalVariables = new Dictionary<string, Object>();

        if (loadGlobalsJSON == null || string.IsNullOrEmpty(loadGlobalsJSON.text))
        {
            Debug.LogWarning("No Ink globals JSON assigned. Dialogue globals will not be persisted.");
            return;
        }

        // create the story
        _globalVariablesStory = new Story(loadGlobalsJSON.text);
        // if we have saved data, load it
        if (PlayerPrefs.HasKey(_saveVariablesKey))
        {
            string jsonState = PlayerPrefs.GetString(_saveVariablesKey);
            try
            {
                _globalVariablesStory.state.LoadJson(jsonState);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to load dialogue globals from PlayerPrefs: {ex.Message}");
            }
        }

        // initialize the dictionary
        foreach (string name in _globalVariablesStory.variablesState)
        {
            Object value = _globalVariablesStory.variablesState.GetVariableWithName(name);
            GlobalVariables[name] = value;
            Debug.Log("Initialized global dialogue variable: " + name + " = " + value);
        }
    }

    public void SaveVariables()
    {
        if (_globalVariablesStory != null)
        {
            // Load the current state of all of our variables to the globals story
            VariablesToStory(_globalVariablesStory);
            // NOTE: eventually, you'd want to replace this with an actual save/load method
            // rather than using PlayerPrefs.
            PlayerPrefs.SetString(_saveVariablesKey, _globalVariablesStory.state.ToJson());
            PlayerPrefs.Save();
        }
    }

    public void StartListening(Story story)
    {
        if (story == null)
            return;

        // it's important that VariablesToStory is before assigning the listener!
        VariablesToStory(story);
        story.variablesState.variableChangedEvent += VariableChanged;
    }

    public void StopListening(Story story)
    {
        if (story == null)
            return;

        story.variablesState.variableChangedEvent -= VariableChanged;
    }

    private void VariableChanged(string name, Object value)
    {
        // only maintain variables that were initialized from the globals ink file
        if (GlobalVariables.ContainsKey(name))
            GlobalVariables[name] = value;
    }

    private void VariablesToStory(Story story)
    {
        if (story == null)
            return;

        foreach (KeyValuePair<string, Object> variable in GlobalVariables)
        {
            if (story.variablesState.GetVariableWithName(variable.Key) == null)
                continue;

            try
            {
                story.variablesState.SetGlobal(variable.Key, variable.Value);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to copy dialogue global '{variable.Key}' into story: {ex.Message}");
            }
        }
    }
}