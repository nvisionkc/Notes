// Enhanced C# completions for the scripting environment
// Loads completion data from .NET CompletionService via JSInterop
// Uses Roslyn for real-time semantic completions

(function () {
    'use strict';

    // Completion data loaded from .NET
    let completionData = null;
    let dotNetRef = null;

    // Roslyn completion cache and debounce
    let roslynCache = { code: '', position: -1, results: [] };
    let roslynPending = null;
    const ROSLYN_DEBOUNCE_MS = 150;

    // Guard to prevent multiple provider registrations
    let completionProviderRegistered = false;

    // Kind mapping from string to Monaco enum
    const kindMap = {
        'Property': 9,      // monaco.languages.CompletionItemKind.Property
        'Method': 0,        // monaco.languages.CompletionItemKind.Method
        'Field': 4,         // monaco.languages.CompletionItemKind.Field
        'Class': 5,         // monaco.languages.CompletionItemKind.Class
        'Struct': 21,       // monaco.languages.CompletionItemKind.Struct
        'Interface': 7,     // monaco.languages.CompletionItemKind.Interface
        'Constructor': 3,   // monaco.languages.CompletionItemKind.Constructor
        'Snippet': 14,      // monaco.languages.CompletionItemKind.Snippet
        'Module': 8,        // monaco.languages.CompletionItemKind.Module
        'Keyword': 13,      // monaco.languages.CompletionItemKind.Keyword
        'Variable': 5,      // monaco.languages.CompletionItemKind.Variable
        'Enum': 12,         // monaco.languages.CompletionItemKind.Enum
        'EnumMember': 19,   // monaco.languages.CompletionItemKind.EnumMember
        'Event': 22,        // monaco.languages.CompletionItemKind.Event
        'Function': 1,      // monaco.languages.CompletionItemKind.Function
        'Text': 0,          // monaco.languages.CompletionItemKind.Text
    };

    // Build type index for fast lookup
    let typeIndex = {};

    function buildTypeIndex() {
        typeIndex = {};
        if (!completionData?.types) return;

        for (const type of completionData.types) {
            typeIndex[type.name.toLowerCase()] = type;
            if (type.namespace) {
                typeIndex[`${type.namespace.toLowerCase()}.${type.name.toLowerCase()}`] = type;
            }
        }
    }

    // Initialize completions with data from .NET
    window.initializeCompletions = function (jsonData, netRef) {
        try {
            completionData = JSON.parse(jsonData);
            dotNetRef = netRef;
            buildTypeIndex();
            registerCompletionProvider();
            console.log('Completions initialized with', completionData.types?.length || 0, 'types');
        } catch (e) {
            console.error('Failed to initialize completions:', e);
        }
    };

    // Update completions (when modules change)
    window.updateCompletions = function (jsonData) {
        try {
            completionData = JSON.parse(jsonData);
            buildTypeIndex();
            console.log('Completions updated');
        } catch (e) {
            console.error('Failed to update completions:', e);
        }
    };

    // Legacy registration function (fallback)
    window.registerScriptCompletions = function () {
        if (completionData) {
            console.log('Completions already initialized via JSInterop');
            return;
        }
        // Fallback to basic completions if not initialized via JSInterop
        registerCompletionProvider();
        console.log('Script completions registered (basic mode)');
    };

    function registerCompletionProvider() {
        if (typeof monaco === 'undefined') {
            console.error('Monaco not loaded yet');
            return;
        }

        // Prevent duplicate registrations
        if (completionProviderRegistered) {
            console.log('Completion provider already registered, skipping');
            return;
        }
        completionProviderRegistered = true;

        monaco.languages.registerCompletionItemProvider('csharp', {
            triggerCharacters: ['.', ' '],
            provideCompletionItems: async function (model, position, context, token) {
                const word = model.getWordUntilPosition(position);
                const range = {
                    startLineNumber: position.lineNumber,
                    endLineNumber: position.lineNumber,
                    startColumn: word.startColumn,
                    endColumn: word.endColumn
                };

                const code = model.getValue();
                const offset = model.getOffsetAt(position);

                const textUntilPosition = model.getValueInRange({
                    startLineNumber: 1,
                    startColumn: 1,
                    endLineNumber: position.lineNumber,
                    endColumn: position.column
                });

                const lineText = model.getLineContent(position.lineNumber);
                const textBeforeCursor = lineText.substring(0, position.column - 1);

                const suggestions = [];

                // Try Roslyn completions first (async)
                const roslynSuggestions = await getRoslynCompletions(code, offset, range);
                if (roslynSuggestions.length > 0) {
                    suggestions.push(...roslynSuggestions);
                }

                // Determine context for fallback/supplemental completions
                const analysisContext = analyzeContext(textUntilPosition, textBeforeCursor);

                // If Roslyn returned results, we still add snippets and module extensions
                // If Roslyn failed or returned nothing, use full manual completions
                if (roslynSuggestions.length === 0) {
                    // Full fallback to manual completions
                    if (analysisContext.afterDot) {
                        addMemberCompletions(suggestions, analysisContext, range);
                    } else if (analysisContext.afterNew) {
                        addConstructorCompletions(suggestions, range);
                    } else {
                        addTopLevelCompletions(suggestions, range, analysisContext);
                    }
                } else {
                    // Roslyn worked - add supplemental items (snippets, extensions)
                    if (!analysisContext.afterDot && !analysisContext.afterNew) {
                        // Add snippets for top-level context
                        addSnippetCompletions(suggestions, range);
                        // Add extension access
                        addExtensionCompletions(suggestions, range, analysisContext);
                    }
                }

                // Deduplicate by label (Roslyn may return duplicates for overloads)
                const seen = new Set();
                const uniqueSuggestions = suggestions.filter(s => {
                    if (seen.has(s.label)) return false;
                    seen.add(s.label);
                    return true;
                });

                return { suggestions: uniqueSuggestions };
            }
        });
    }

    // Get Roslyn completions via .NET interop
    async function getRoslynCompletions(code, position, range) {
        if (!dotNetRef) {
            return [];
        }

        try {
            // Check cache
            if (roslynCache.code === code && roslynCache.position === position) {
                return roslynCache.results.map(item => convertRoslynItem(item, range));
            }

            // Call .NET for Roslyn completions
            const jsonResult = await dotNetRef.invokeMethodAsync('GetRoslynCompletionsAsync', code, position);
            const items = JSON.parse(jsonResult);

            // Update cache
            roslynCache = { code, position, results: items };

            return items.map(item => convertRoslynItem(item, range));
        } catch (e) {
            console.error('Roslyn completion error:', e);
            return [];
        }
    }

    // Convert Roslyn completion item to Monaco format
    function convertRoslynItem(item, range) {
        const kind = kindMap[item.kind] || kindMap['Text'];

        return {
            label: item.label,
            kind: kind,
            detail: item.detail,
            documentation: item.documentation,
            insertText: item.insertText || item.label,
            insertTextRules: item.isSnippet ?
                monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet : undefined,
            range: range,
            sortText: '0' + item.label // Prioritize Roslyn results
        };
    }

    // Add snippet completions (for supplementing Roslyn)
    function addSnippetCompletions(suggestions, range) {
        if (!completionData?.snippets) return;

        for (const snippet of completionData.snippets) {
            suggestions.push({
                label: snippet.label,
                kind: kindMap['Snippet'],
                detail: snippet.detail,
                documentation: snippet.documentation,
                insertText: snippet.insertText,
                insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                range: range,
                sortText: '1' + snippet.label // After Roslyn results
            });
        }
    }

    // Add extension completions (for supplementing Roslyn)
    function addExtensionCompletions(suggestions, range, context) {
        if (context.isExtensions) {
            if (context.extensionPrefix) {
                addExtensionMethodCompletions(suggestions, context.extensionPrefix, range);
            } else {
                addExtensionPrefixCompletions(suggestions, range);
            }
        }
    }

    function analyzeContext(fullText, lineTextBeforeCursor) {
        const context = {
            afterDot: false,
            afterNew: false,
            beforeIdentifier: '',
            isExtensions: false,
            extensionPrefix: null,
            isMethodChain: false,
            isIndexerAccess: false,
        };

        // Check for dot access - handle various patterns
        // Pattern 1: identifier.  (e.g., myDict.)
        const identifierDotMatch = lineTextBeforeCursor.match(/(\w+)\s*\.\s*$/);
        // Pattern 2: ).  (e.g., new Dictionary<string, string>().)
        const methodCallDotMatch = lineTextBeforeCursor.match(/\)\s*\.\s*$/);
        // Pattern 3: ].  (e.g., myArray[0].)
        const indexerDotMatch = lineTextBeforeCursor.match(/\]\s*\.\s*$/);

        if (identifierDotMatch) {
            context.afterDot = true;
            context.beforeIdentifier = identifierDotMatch[1];

            // Check if accessing Extensions
            if (context.beforeIdentifier.toLowerCase() === 'extensions') {
                context.isExtensions = true;
            }

            // Check for Extensions.Prefix.
            const extMatch = lineTextBeforeCursor.match(/Extensions\s*\.\s*(\w+)\s*\.\s*$/i);
            if (extMatch) {
                context.isExtensions = true;
                context.extensionPrefix = extMatch[1];
            }
        } else if (methodCallDotMatch) {
            context.afterDot = true;
            context.isMethodChain = true;
            // Try to extract the type from new Type<...>() pattern
            const newTypeMatch = lineTextBeforeCursor.match(/new\s+(\w+)/);
            if (newTypeMatch) {
                context.beforeIdentifier = newTypeMatch[1];
            }
        } else if (indexerDotMatch) {
            context.afterDot = true;
            context.isIndexerAccess = true;
        }

        // Check for new keyword
        const newMatch = lineTextBeforeCursor.match(/new\s+$/i);
        if (newMatch) {
            context.afterNew = true;
        }

        return context;
    }

    function addMemberCompletions(suggestions, context, range) {
        if (context.isExtensions && context.extensionPrefix) {
            // Show methods for specific extension prefix
            addExtensionMethodCompletions(suggestions, context.extensionPrefix, range);
            return;
        }

        if (context.isExtensions) {
            // Show available extension prefixes
            addExtensionPrefixCompletions(suggestions, range);
            return;
        }

        // Try to find type for the identifier
        const identifier = context.beforeIdentifier.toLowerCase();
        const type = typeIndex[identifier];

        if (type) {
            if (context.isMethodChain) {
                // After new Type().  - show instance members
                addTypeMembers(suggestions, type.instanceMembers, range);
            } else {
                // After TypeName. - show static members
                addTypeMembers(suggestions, type.staticMembers, range);
            }
        }

        // Also check if it might be an instance - show common instance members
        addInstanceMemberCompletions(suggestions, context.beforeIdentifier, range);

        // Always show LINQ methods for collections
        addLinqCompletions(suggestions, range);
    }

    function addInstanceMemberCompletions(suggestions, identifier, range) {
        // Heuristic: if identifier looks like a string variable, show string members
        // This is a simplified type inference
        const lowerName = identifier.toLowerCase();

        // String-like variables
        if (lowerName.includes('text') || lowerName.includes('str') ||
            lowerName.includes('name') || lowerName.includes('content') ||
            lowerName.includes('html') || lowerName === 'notecontent' ||
            lowerName === 'noteplaintext' || lowerName === 'notetitle' ||
            lowerName === 'clipboardtext') {
            const stringType = typeIndex['string'];
            if (stringType) {
                addTypeMembers(suggestions, stringType.instanceMembers, range);
            }
        }

        // List-like variables
        if (lowerName.includes('list') || lowerName.includes('items') ||
            lowerName.includes('collection') || lowerName.includes('array') ||
            lowerName === 'clipboardhistory') {
            const listType = typeIndex['list'];
            if (listType) {
                addTypeMembers(suggestions, listType.instanceMembers, range);
            }
        }

        // Dictionary-like variables
        if (lowerName.includes('dict') || lowerName.includes('map') ||
            lowerName.includes('lookup') || lowerName.includes('cache')) {
            const dictType = typeIndex['dictionary'];
            if (dictType) {
                addTypeMembers(suggestions, dictType.instanceMembers, range);
            }
        }

        // HashSet-like variables
        if (lowerName.includes('set') || lowerName.includes('unique') ||
            lowerName.includes('distinct')) {
            const hashSetType = typeIndex['hashset'];
            if (hashSetType) {
                addTypeMembers(suggestions, hashSetType.instanceMembers, range);
            }
        }

        // DateTime variables
        if (lowerName.includes('date') || lowerName.includes('time') ||
            lowerName.includes('timestamp') || lowerName.includes('created') ||
            lowerName.includes('modified') || lowerName.includes('when')) {
            const dateType = typeIndex['datetime'];
            if (dateType) {
                addTypeMembers(suggestions, dateType.instanceMembers, range);
            }
        }

        // StringBuilder-like variables
        if (lowerName.includes('builder') || lowerName.includes('sb') ||
            lowerName.includes('buffer')) {
            const sbType = typeIndex['stringbuilder'];
            if (sbType) {
                addTypeMembers(suggestions, sbType.instanceMembers, range);
            }
        }

        // HttpClient-like variables
        if (lowerName.includes('http') || lowerName.includes('client') ||
            lowerName.includes('api')) {
            const httpType = typeIndex['httpclient'];
            if (httpType) {
                addTypeMembers(suggestions, httpType.instanceMembers, range);
            }
        }

        // Regex-like variables
        if (lowerName.includes('regex') || lowerName.includes('pattern') ||
            lowerName.includes('expression')) {
            const regexType = typeIndex['regex'];
            if (regexType) {
                addTypeMembers(suggestions, regexType.instanceMembers, range);
            }
        }

        // Always add common collection/object members as fallback
        // These are commonly used and helpful even without type inference
        addCommonInstanceMembers(suggestions, range);
    }

    function addCommonInstanceMembers(suggestions, range) {
        // Common members that appear on many types
        const commonMembers = [
            // Collection members
            { label: 'Count', kind: 'Property', detail: 'int', documentation: 'Gets the number of elements' },
            { label: 'Add', kind: 'Method', detail: 'void Add(T item)', insertText: 'Add(${1:item})', isSnippet: true },
            { label: 'Remove', kind: 'Method', detail: 'bool Remove(T item)', insertText: 'Remove(${1:item})', isSnippet: true },
            { label: 'Clear', kind: 'Method', detail: 'void Clear()', insertText: 'Clear()' },
            { label: 'Contains', kind: 'Method', detail: 'bool Contains(T item)', insertText: 'Contains(${1:item})', isSnippet: true },

            // Dictionary-specific
            { label: 'Keys', kind: 'Property', detail: 'KeyCollection', documentation: 'Gets a collection of keys' },
            { label: 'Values', kind: 'Property', detail: 'ValueCollection', documentation: 'Gets a collection of values' },
            { label: 'ContainsKey', kind: 'Method', detail: 'bool ContainsKey(TKey key)', insertText: 'ContainsKey(${1:key})', isSnippet: true },
            { label: 'ContainsValue', kind: 'Method', detail: 'bool ContainsValue(TValue value)', insertText: 'ContainsValue(${1:value})', isSnippet: true },
            { label: 'TryGetValue', kind: 'Method', detail: 'bool TryGetValue(TKey key, out TValue value)', insertText: 'TryGetValue(${1:key}, out var ${2:value})', isSnippet: true },

            // Common object members
            { label: 'ToString', kind: 'Method', detail: 'string ToString()', insertText: 'ToString()' },
            { label: 'GetType', kind: 'Method', detail: 'Type GetType()', insertText: 'GetType()' },
            { label: 'Equals', kind: 'Method', detail: 'bool Equals(object obj)', insertText: 'Equals(${1:obj})', isSnippet: true },
            { label: 'GetHashCode', kind: 'Method', detail: 'int GetHashCode()', insertText: 'GetHashCode()' },
        ];

        for (const member of commonMembers) {
            // Avoid duplicates by checking if label already exists
            if (!suggestions.some(s => s.label === member.label)) {
                suggestions.push(createCompletionItem(member, range));
            }
        }
    }

    function addTypeMembers(suggestions, members, range) {
        if (!members) return;

        for (const member of members) {
            suggestions.push(createCompletionItem(member, range));
        }
    }

    function addExtensionPrefixCompletions(suggestions, range) {
        if (!completionData?.extensions) return;

        for (const ext of completionData.extensions) {
            suggestions.push({
                label: ext.prefix,
                kind: kindMap['Module'],
                detail: ext.description || `Module extension: ${ext.prefix}`,
                documentation: ext.description,
                insertText: ext.prefix,
                range: range
            });
        }
    }

    function addExtensionMethodCompletions(suggestions, prefix, range) {
        if (!completionData?.extensions) return;

        const extension = completionData.extensions.find(e =>
            e.prefix.toLowerCase() === prefix.toLowerCase());

        if (extension?.methods) {
            for (const method of extension.methods) {
                suggestions.push(createCompletionItem(method, range));
            }
        }
    }

    function addConstructorCompletions(suggestions, range) {
        if (!completionData?.types) return;

        for (const type of completionData.types) {
            if (type.constructors?.length > 0) {
                for (const ctor of type.constructors) {
                    suggestions.push(createCompletionItem(ctor, range));
                }
            } else {
                // Default constructor hint for types without explicit constructors
                suggestions.push({
                    label: type.name,
                    kind: kindMap['Class'],
                    detail: type.documentation || type.name,
                    insertText: `${type.name}()`,
                    range: range
                });
            }
        }
    }

    function addTopLevelCompletions(suggestions, range, context) {
        // Script globals
        if (completionData?.globals) {
            for (const global of completionData.globals) {
                suggestions.push(createCompletionItem(global, range));
            }
        }

        // Snippets
        if (completionData?.snippets) {
            for (const snippet of completionData.snippets) {
                suggestions.push({
                    label: snippet.label,
                    kind: kindMap['Snippet'],
                    detail: snippet.detail,
                    documentation: snippet.documentation,
                    insertText: snippet.insertText,
                    insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                    range: range
                });
            }
        }

        // Type names (for static access like DateTime.Now)
        if (completionData?.types) {
            for (const type of completionData.types) {
                if (type.staticMembers?.length > 0) {
                    suggestions.push({
                        label: type.name,
                        kind: kindMap[type.kind] || kindMap['Class'],
                        detail: type.namespace ? `${type.namespace}.${type.name}` : type.name,
                        documentation: type.documentation,
                        insertText: type.name,
                        range: range
                    });
                }
            }
        }

        // Extensions access
        suggestions.push({
            label: 'Extensions',
            kind: kindMap['Property'],
            detail: 'dynamic',
            documentation: 'Module-provided extension objects (e.g., Extensions.Http)',
            insertText: 'Extensions',
            range: range
        });
    }

    function addLinqCompletions(suggestions, range) {
        const linqMethods = [
            { label: 'Where', kind: 'Method', detail: 'IEnumerable<T> Where(Func<T, bool> predicate)', insertText: 'Where(${1:x} => ${2:condition})', isSnippet: true },
            { label: 'Select', kind: 'Method', detail: 'IEnumerable<TResult> Select(Func<T, TResult> selector)', insertText: 'Select(${1:x} => ${2:x})', isSnippet: true },
            { label: 'SelectMany', kind: 'Method', detail: 'IEnumerable<TResult> SelectMany(Func<T, IEnumerable<TResult>> selector)', insertText: 'SelectMany(${1:x} => ${2:x})', isSnippet: true },
            { label: 'OrderBy', kind: 'Method', detail: 'IOrderedEnumerable<T> OrderBy(Func<T, TKey> keySelector)', insertText: 'OrderBy(${1:x} => ${2:x})', isSnippet: true },
            { label: 'OrderByDescending', kind: 'Method', detail: 'IOrderedEnumerable<T> OrderByDescending(Func<T, TKey> keySelector)', insertText: 'OrderByDescending(${1:x} => ${2:x})', isSnippet: true },
            { label: 'ThenBy', kind: 'Method', detail: 'IOrderedEnumerable<T> ThenBy(Func<T, TKey> keySelector)', insertText: 'ThenBy(${1:x} => ${2:x})', isSnippet: true },
            { label: 'GroupBy', kind: 'Method', detail: 'IEnumerable<IGrouping<TKey, T>> GroupBy(Func<T, TKey> keySelector)', insertText: 'GroupBy(${1:x} => ${2:x})', isSnippet: true },
            { label: 'Join', kind: 'Method', detail: 'IEnumerable<TResult> Join(...)', insertText: 'Join(${1:inner}, ${2:outerKey} => ${3}, ${4:innerKey} => ${5}, (${6:o}, ${7:i}) => ${8})', isSnippet: true },
            { label: 'First', kind: 'Method', detail: 'T First()', insertText: 'First()' },
            { label: 'FirstOrDefault', kind: 'Method', detail: 'T? FirstOrDefault()', insertText: 'FirstOrDefault()' },
            { label: 'Last', kind: 'Method', detail: 'T Last()', insertText: 'Last()' },
            { label: 'LastOrDefault', kind: 'Method', detail: 'T? LastOrDefault()', insertText: 'LastOrDefault()' },
            { label: 'Single', kind: 'Method', detail: 'T Single()', insertText: 'Single()' },
            { label: 'SingleOrDefault', kind: 'Method', detail: 'T? SingleOrDefault()', insertText: 'SingleOrDefault()' },
            { label: 'ElementAt', kind: 'Method', detail: 'T ElementAt(int index)', insertText: 'ElementAt(${1:index})', isSnippet: true },
            { label: 'ToList', kind: 'Method', detail: 'List<T> ToList()', insertText: 'ToList()' },
            { label: 'ToArray', kind: 'Method', detail: 'T[] ToArray()', insertText: 'ToArray()' },
            { label: 'ToDictionary', kind: 'Method', detail: 'Dictionary<TKey, TValue> ToDictionary(Func<T, TKey> keySelector)', insertText: 'ToDictionary(${1:x} => ${2:x.Key})', isSnippet: true },
            { label: 'ToHashSet', kind: 'Method', detail: 'HashSet<T> ToHashSet()', insertText: 'ToHashSet()' },
            { label: 'Count', kind: 'Method', detail: 'int Count()', insertText: 'Count()' },
            { label: 'LongCount', kind: 'Method', detail: 'long LongCount()', insertText: 'LongCount()' },
            { label: 'Sum', kind: 'Method', detail: 'T Sum()', insertText: 'Sum()' },
            { label: 'Average', kind: 'Method', detail: 'double Average()', insertText: 'Average()' },
            { label: 'Min', kind: 'Method', detail: 'T Min()', insertText: 'Min()' },
            { label: 'Max', kind: 'Method', detail: 'T Max()', insertText: 'Max()' },
            { label: 'Any', kind: 'Method', detail: 'bool Any()', insertText: 'Any()' },
            { label: 'All', kind: 'Method', detail: 'bool All(Func<T, bool> predicate)', insertText: 'All(${1:x} => ${2:condition})', isSnippet: true },
            { label: 'Contains', kind: 'Method', detail: 'bool Contains(T value)', insertText: 'Contains(${1:value})', isSnippet: true },
            { label: 'Take', kind: 'Method', detail: 'IEnumerable<T> Take(int count)', insertText: 'Take(${1:count})', isSnippet: true },
            { label: 'TakeLast', kind: 'Method', detail: 'IEnumerable<T> TakeLast(int count)', insertText: 'TakeLast(${1:count})', isSnippet: true },
            { label: 'TakeWhile', kind: 'Method', detail: 'IEnumerable<T> TakeWhile(Func<T, bool> predicate)', insertText: 'TakeWhile(${1:x} => ${2:condition})', isSnippet: true },
            { label: 'Skip', kind: 'Method', detail: 'IEnumerable<T> Skip(int count)', insertText: 'Skip(${1:count})', isSnippet: true },
            { label: 'SkipLast', kind: 'Method', detail: 'IEnumerable<T> SkipLast(int count)', insertText: 'SkipLast(${1:count})', isSnippet: true },
            { label: 'SkipWhile', kind: 'Method', detail: 'IEnumerable<T> SkipWhile(Func<T, bool> predicate)', insertText: 'SkipWhile(${1:x} => ${2:condition})', isSnippet: true },
            { label: 'Distinct', kind: 'Method', detail: 'IEnumerable<T> Distinct()', insertText: 'Distinct()' },
            { label: 'DistinctBy', kind: 'Method', detail: 'IEnumerable<T> DistinctBy(Func<T, TKey> keySelector)', insertText: 'DistinctBy(${1:x} => ${2:x})', isSnippet: true },
            { label: 'Reverse', kind: 'Method', detail: 'IEnumerable<T> Reverse()', insertText: 'Reverse()' },
            { label: 'Concat', kind: 'Method', detail: 'IEnumerable<T> Concat(IEnumerable<T> second)', insertText: 'Concat(${1:second})', isSnippet: true },
            { label: 'Union', kind: 'Method', detail: 'IEnumerable<T> Union(IEnumerable<T> second)', insertText: 'Union(${1:second})', isSnippet: true },
            { label: 'Intersect', kind: 'Method', detail: 'IEnumerable<T> Intersect(IEnumerable<T> second)', insertText: 'Intersect(${1:second})', isSnippet: true },
            { label: 'Except', kind: 'Method', detail: 'IEnumerable<T> Except(IEnumerable<T> second)', insertText: 'Except(${1:second})', isSnippet: true },
            { label: 'Zip', kind: 'Method', detail: 'IEnumerable<(T, TSecond)> Zip(IEnumerable<TSecond> second)', insertText: 'Zip(${1:second})', isSnippet: true },
            { label: 'Aggregate', kind: 'Method', detail: 'T Aggregate(Func<T, T, T> func)', insertText: 'Aggregate((${1:acc}, ${2:x}) => ${3})', isSnippet: true },
            { label: 'DefaultIfEmpty', kind: 'Method', detail: 'IEnumerable<T> DefaultIfEmpty()', insertText: 'DefaultIfEmpty()' },
            { label: 'OfType', kind: 'Method', detail: 'IEnumerable<TResult> OfType<TResult>()', insertText: 'OfType<${1:T}>()', isSnippet: true },
            { label: 'Cast', kind: 'Method', detail: 'IEnumerable<TResult> Cast<TResult>()', insertText: 'Cast<${1:T}>()', isSnippet: true },
        ];

        for (const method of linqMethods) {
            suggestions.push(createCompletionItem(method, range));
        }
    }

    function createCompletionItem(item, range) {
        const kind = kindMap[item.kind] || kindMap['Method'];

        return {
            label: item.label,
            kind: kind,
            detail: item.detail,
            documentation: item.documentation,
            insertText: item.insertText || item.label,
            insertTextRules: item.isSnippet ?
                monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet : undefined,
            range: range
        };
    }
})();
