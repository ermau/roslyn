﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

#nullable enable

using System.Collections.Generic;
using System.Collections.Immutable;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Options
{
    internal sealed partial class SerializableOptionSet : OptionSet
    {
        /// <summary>
        /// An implementation of <see cref="OptionSet"/> for non-serializable options that are defined in VS layers.
        /// It fetches values it doesn't know about to the workspace's option service. It ensures a contract
        /// that values are immutable from this instance once observed.
        /// TODO: Remove this type once we move all the options from the VS layers into Workspaces/Features, so the entire
        ///       option set is serializable and becomes pure data snapshot for options.
        /// </summary>
        private sealed class WorkspaceOptionSet : OptionSet
        {
            private ImmutableDictionary<OptionKey, object?> _values;

            internal WorkspaceOptionSet(IOptionService? service)
                : this(service, ImmutableDictionary<OptionKey, object?>.Empty)
            {
            }

            // Can be null in tests.
            public IOptionService? OptionService { get; }

            private WorkspaceOptionSet(IOptionService? service, ImmutableDictionary<OptionKey, object?> values)
            {
                OptionService = service;
                _values = values;
            }

            [PerformanceSensitive("https://github.com/dotnet/roslyn/issues/30819", AllowLocks = false)]
            public override object? GetOption(OptionKey optionKey)
            {
                if (_values.TryGetValue(optionKey, out var value))
                {
                    return value;
                }

                value = OptionService != null ? OptionService.GetOption(optionKey) : optionKey.Option.DefaultValue;
                return ImmutableInterlocked.GetOrAdd(ref _values, optionKey, value);
            }

            public override OptionSet WithChangedOption(OptionKey optionAndLanguage, object? value)
            {
                // make sure we first load this in current optionset
                this.GetOption(optionAndLanguage);

                return new WorkspaceOptionSet(OptionService, _values.SetItem(optionAndLanguage, value));
            }

            /// <summary>
            /// Gets a list of all the options that were changed.
            /// </summary>
            internal IEnumerable<OptionKey> GetChangedOptions()
            {
                var optionSet = OptionService?.GetOptions();
                return GetChangedOptions(optionSet);
            }

            internal override IEnumerable<OptionKey> GetChangedOptions(OptionSet? optionSet)
            {
                if (optionSet == this)
                {
                    yield break;
                }

                foreach (var kvp in _values)
                {
                    var currentValue = optionSet?.GetOption(kvp.Key);
                    if (!object.Equals(currentValue, kvp.Value))
                    {
                        yield return kvp.Key;
                    }
                }
            }
        }
    }
}
