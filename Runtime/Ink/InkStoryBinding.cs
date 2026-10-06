using System;
using System.Collections.Generic;
using System.Reflection;
using Ink.Runtime;
using UnityEngine;

namespace SAS.DialogueSystem
{
    /// <summary>
    /// Marks a method as an implementation of an Ink EXTERNAL function.
    /// When no name is supplied, the C# method name is used unchanged.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public sealed class InkExternalAttribute : Attribute
    {
        public string Name { get; }

        public InkExternalAttribute(string name = null)
        {
            Name = name;
        }
    }

    /// <summary>
    /// Marks a field or readable property whose current value is copied into an
    /// Ink global before the story evaluates its first line.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
    public sealed class InkVariableAttribute : Attribute
    {
        public string Name { get; }

        public InkVariableAttribute(string name = null)
        {
            Name = name;
        }
    }

    /// <summary>
    /// Contract used by DialogueTrigger. Custom implementations are supported,
    /// while InkStoryBinding provides the normal attribute-driven implementation.
    /// </summary>
    public interface IInkStoryBinding
    {
        void RegisterExternalMethods(InkExternalMethodRegistry registry);
        void BindVariables(Story story);
    }

    /// <summary>
    /// Attribute-driven base for game-specific Ink integration. Derived classes
    /// contain gameplay logic only; registration and variable injection are handled here.
    /// </summary>
    public abstract class InkStoryBinding : MonoBehaviour, IInkStoryBinding
    {
        private sealed class VariableBinding
        {
            public string Name;
            public Func<object> Read;
        }

        private readonly List<KeyValuePair<string, Delegate>> _externalMethods = new();
        private readonly List<VariableBinding> _variables = new();
        private bool _isCached;

        public void RegisterExternalMethods(InkExternalMethodRegistry registry)
        {
            if (registry == null)
            {
                Debug.LogError("Cannot register Ink external methods because the registry is null.", this);
                return;
            }

            EnsureBindingsCached();
            foreach (var binding in _externalMethods)
                registry.Register(binding.Key, binding.Value);
        }

        public void BindVariables(Story story)
        {
            if (story == null)
            {
                Debug.LogError("Cannot bind Ink variables because the story is null.", this);
                return;
            }

            EnsureBindingsCached();
            foreach (var binding in _variables)
                InkStoryVariableUtils.SetVariable(story, binding.Name, binding.Read());
        }

        private void EnsureBindingsCached()
        {
            if (_isCached)
                return;

            _isCached = true;
            var externalNames = new HashSet<string>(StringComparer.Ordinal);
            var variableNames = new HashSet<string>(StringComparer.Ordinal);

            for (var type = GetType(); type != null && type != typeof(InkStoryBinding); type = type.BaseType)
            {
                CacheExternalMethods(type, externalNames);
                CacheFields(type, variableNames);
                CacheProperties(type, variableNames);
            }
        }

        private void CacheExternalMethods(Type type, ISet<string> names)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static |
                                       BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            foreach (var method in type.GetMethods(flags))
            {
                var attribute = method.GetCustomAttribute<InkExternalAttribute>(true);
                if (attribute == null)
                    continue;

                var inkName = ResolveName(attribute.Name, method.Name);
                if (!names.Add(inkName))
                {
                    Debug.LogError($"Duplicate Ink external binding '{inkName}' on {GetType().Name}.", this);
                    continue;
                }

                if (!TryCreateDelegate(method, out var methodDelegate, out var error))
                {
                    Debug.LogError($"Cannot bind Ink external '{inkName}' to {method.Name}: {error}", this);
                    continue;
                }

                _externalMethods.Add(new KeyValuePair<string, Delegate>(inkName, methodDelegate));
            }
        }

        private void CacheFields(Type type, ISet<string> names)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static |
                                       BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            foreach (var field in type.GetFields(flags))
            {
                var attribute = field.GetCustomAttribute<InkVariableAttribute>(true);
                if (attribute == null)
                    continue;

                var inkName = ResolveName(attribute.Name, field.Name);
                if (!TryAddVariableName(inkName, names))
                    continue;

                _variables.Add(new VariableBinding
                {
                    Name = inkName,
                    Read = () => field.GetValue(field.IsStatic ? null : this)
                });
            }
        }

        private void CacheProperties(Type type, ISet<string> names)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static |
                                       BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            foreach (var property in type.GetProperties(flags))
            {
                var attribute = property.GetCustomAttribute<InkVariableAttribute>(true);
                if (attribute == null)
                    continue;

                var getter = property.GetGetMethod(true);
                if (getter == null || property.GetIndexParameters().Length != 0)
                {
                    Debug.LogError($"Ink variable property '{property.Name}' must have a parameterless getter.", this);
                    continue;
                }

                var inkName = ResolveName(attribute.Name, property.Name);
                if (!TryAddVariableName(inkName, names))
                    continue;

                _variables.Add(new VariableBinding
                {
                    Name = inkName,
                    Read = () => property.GetValue(getter.IsStatic ? null : this)
                });
            }
        }

        private bool TryAddVariableName(string inkName, ISet<string> names)
        {
            if (names.Add(inkName))
                return true;

            Debug.LogError($"Duplicate Ink variable binding '{inkName}' on {GetType().Name}.", this);
            return false;
        }

        private bool TryCreateDelegate(MethodInfo method, out Delegate methodDelegate, out string error)
        {
            methodDelegate = null;
            error = null;

            if (method.ContainsGenericParameters)
            {
                error = "generic methods are not supported";
                return false;
            }

            var parameters = method.GetParameters();
            if (parameters.Length > 4)
            {
                error = "only zero to four parameters are supported";
                return false;
            }

            var parameterTypes = new Type[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType.IsByRef || parameters[i].IsOut)
                {
                    error = "ref and out parameters are not supported";
                    return false;
                }

                parameterTypes[i] = parameters[i].ParameterType;
            }

            try
            {
                var delegateType = GetDelegateType(parameterTypes, method.ReturnType);
                methodDelegate = Delegate.CreateDelegate(delegateType, method.IsStatic ? null : this, method, true);
                return methodDelegate != null;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static Type GetDelegateType(IReadOnlyList<Type> parameters, Type returnType)
        {
            if (returnType == typeof(void))
            {
                return parameters.Count switch
                {
                    0 => typeof(Action),
                    1 => typeof(Action<>).MakeGenericType(parameters[0]),
                    2 => typeof(Action<,>).MakeGenericType(parameters[0], parameters[1]),
                    3 => typeof(Action<,,>).MakeGenericType(parameters[0], parameters[1], parameters[2]),
                    4 => typeof(Action<,,,>).MakeGenericType(parameters[0], parameters[1], parameters[2],
                        parameters[3]),
                    _ => throw new ArgumentOutOfRangeException(nameof(parameters))
                };
            }

            var types = new Type[parameters.Count + 1];
            for (var i = 0; i < parameters.Count; i++)
                types[i] = parameters[i];
            types[types.Length - 1] = returnType;

            return parameters.Count switch
            {
                0 => typeof(Func<>).MakeGenericType(types),
                1 => typeof(Func<,>).MakeGenericType(types),
                2 => typeof(Func<,,>).MakeGenericType(types),
                3 => typeof(Func<,,,>).MakeGenericType(types),
                4 => typeof(Func<,,,,>).MakeGenericType(types),
                _ => throw new ArgumentOutOfRangeException(nameof(parameters))
            };
        }

        private static string ResolveName(string configuredName, string memberName)
        {
            return string.IsNullOrWhiteSpace(configuredName) ? memberName : configuredName.Trim();
        }
    }
}