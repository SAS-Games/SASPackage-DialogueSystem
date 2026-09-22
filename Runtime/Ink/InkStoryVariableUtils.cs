using System;
using Ink.Runtime;
using Debug = UnityEngine.Debug;

namespace SAS.DialogueSystem
{
    public static class InkStoryVariableUtils
    {
        /// <summary>
        /// Safely sets a global variable in the Ink story.
        /// </summary>
        public static void SetVariable<T>(Story story, string variableName, T value)
        {
            if (story == null || string.IsNullOrEmpty(variableName))
            {
                Debug.LogWarning("SetVariable failed: story or variable name is null.");
                return;
            }

            try
            {
                story.variablesState[variableName] = value;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to set Ink variable '{variableName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Gets a variable of type T from the Ink story. Returns default(T) if not found or cast fails.
        /// </summary>
        public static T GetVariable<T>(Story story, string variableName)
        {
            if (story == null || string.IsNullOrEmpty(variableName))
            {
                Debug.LogWarning("GetVariable failed: story or variable name is null.");
                return default;
            }

            try
            {
                var value = story.variablesState[variableName];
                if (value is T typedValue)
                    return typedValue;

                // Handle type conversion if it's a number and T is a numeric type
                if (value is int intVal && typeof(T) == typeof(float))
                    return (T)(object)(float)intVal;
                if (value is float floatVal && typeof(T) == typeof(int))
                    return (T)(object)(int)floatVal;

                return (T)value;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to get Ink variable '{variableName}': {ex.Message}");
                return default;
            }
        }

        /// <summary>
        /// Returns true if the Ink variable exists.
        /// </summary>
        public static bool HasVariable(Story story, string variableName)
        {
            if (story == null || string.IsNullOrEmpty(variableName))
                return false;

            try
            {
                return story.variablesState.GetVariableWithName(variableName) != null;
            }
            catch
            {
                return false;
            }
        }
    }
}