using System;
using System.Collections.Generic;
using UnityEngine.Localization;
using UnityEngine.Localization.SmartFormat.PersistentVariables;

namespace SAS.DialogueSystem
{
    internal sealed class DialogueLocalizedStringHandle : IDisposable
    {
        private readonly List<LocalizedString> _ownedLocalizedArguments = new();
        private bool _disposed;

        public DialogueLocalizedStringHandle(string tableName, string entryKey,
            IReadOnlyList<DialogueLocalizationArgument> arguments)
        {
            Reference = new LocalizedString(tableName, entryKey);
            if (arguments == null)
                return;

            foreach (var argument in arguments)
                Bind(argument);
        }

        public LocalizedString Reference { get; }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            (Reference as IDisposable)?.Dispose();
            foreach (var localizedArgument in _ownedLocalizedArguments)
                (localizedArgument as IDisposable)?.Dispose();
            _ownedLocalizedArguments.Clear();
        }

        private void Bind(DialogueLocalizationArgument argument)
        {
            if (argument == null)
                return;

            switch (argument.Type)
            {
                case DialogueLocalizationArgumentType.Integer:
                    Reference[argument.Name] = new IntVariable { Value = (int)argument.Value };
                    break;
                case DialogueLocalizationArgumentType.Float:
                    Reference[argument.Name] = new FloatVariable { Value = (float)argument.Value };
                    break;
                case DialogueLocalizationArgumentType.Boolean:
                    Reference[argument.Name] = new BoolVariable { Value = (bool)argument.Value };
                    break;
                case DialogueLocalizationArgumentType.String:
                    Reference[argument.Name] = new StringVariable { Value = (string)argument.Value };
                    break;
                case DialogueLocalizationArgumentType.Localized:
                    var localizedArgument = new LocalizedString(argument.TableName, argument.EntryKey);
                    _ownedLocalizedArguments.Add(localizedArgument);
                    Reference[argument.Name] = localizedArgument;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
