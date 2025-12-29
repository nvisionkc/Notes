// Custom C# completions for the scripting environment
window.registerScriptCompletions = function () {
    if (typeof monaco === 'undefined') {
        console.error('Monaco not loaded yet');
        return;
    }

    // Register completion provider for C#
    monaco.languages.registerCompletionItemProvider('csharp', {
        triggerCharacters: ['.', ' '],
        provideCompletionItems: function (model, position) {
            const word = model.getWordUntilPosition(position);
            const range = {
                startLineNumber: position.lineNumber,
                endLineNumber: position.lineNumber,
                startColumn: word.startColumn,
                endColumn: word.endColumn
            };

            // Get the text before the cursor to determine context
            const textUntilPosition = model.getValueInRange({
                startLineNumber: 1,
                startColumn: 1,
                endLineNumber: position.lineNumber,
                endColumn: position.column
            });

            const suggestions = [];

            // Script Globals - Properties
            const globals = [
                { label: 'NoteContent', kind: monaco.languages.CompletionItemKind.Property, detail: 'string', documentation: 'Current note content (HTML from rich text editor)' },
                { label: 'NotePlainText', kind: monaco.languages.CompletionItemKind.Property, detail: 'string', documentation: 'Plain text version of current note' },
                { label: 'NoteTitle', kind: monaco.languages.CompletionItemKind.Property, detail: 'string', documentation: 'Current note title' },
                { label: 'ClipboardText', kind: monaco.languages.CompletionItemKind.Property, detail: 'string', documentation: 'Current clipboard text (most recent)' },
                { label: 'ClipboardHistory', kind: monaco.languages.CompletionItemKind.Property, detail: 'List<string>', documentation: 'Recent clipboard history (text items only)' },
                { label: 'OutputContent', kind: monaco.languages.CompletionItemKind.Property, detail: 'string?', documentation: 'Set this to update the note content' },
            ];

            // Script Globals - Methods
            const methods = [
                { label: 'Print', kind: monaco.languages.CompletionItemKind.Method, detail: 'void Print(object? value)', documentation: 'Print to script console', insertText: 'Print(${1:value});', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                { label: 'StripHtml', kind: monaco.languages.CompletionItemKind.Method, detail: 'string StripHtml(string html)', documentation: 'Strip HTML tags from content', insertText: 'StripHtml(${1:html})', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                { label: 'ToHtml', kind: monaco.languages.CompletionItemKind.Method, detail: 'string ToHtml(string text)', documentation: 'Wrap plain text in HTML paragraphs', insertText: 'ToHtml(${1:text})', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                { label: 'TransformLines', kind: monaco.languages.CompletionItemKind.Method, detail: 'string TransformLines(string text, Func<string, string> transform)', documentation: 'Transform each line of text', insertText: 'TransformLines(${1:text}, line => ${2:line})', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
            ];

            // Common C# snippets
            const snippets = [
                { label: 'var', kind: monaco.languages.CompletionItemKind.Snippet, detail: 'Variable declaration', insertText: 'var ${1:name} = ${2:value};', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                { label: 'foreach', kind: monaco.languages.CompletionItemKind.Snippet, detail: 'foreach loop', insertText: 'foreach (var ${1:item} in ${2:collection})\n{\n\t${3}\n}', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                { label: 'for', kind: monaco.languages.CompletionItemKind.Snippet, detail: 'for loop', insertText: 'for (int ${1:i} = 0; ${1:i} < ${2:length}; ${1:i}++)\n{\n\t${3}\n}', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                { label: 'if', kind: monaco.languages.CompletionItemKind.Snippet, detail: 'if statement', insertText: 'if (${1:condition})\n{\n\t${2}\n}', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                { label: 'ifelse', kind: monaco.languages.CompletionItemKind.Snippet, detail: 'if-else statement', insertText: 'if (${1:condition})\n{\n\t${2}\n}\nelse\n{\n\t${3}\n}', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
            ];

            // Common types and methods
            const commonTypes = [
                { label: 'Console.WriteLine', kind: monaco.languages.CompletionItemKind.Method, detail: 'void', documentation: 'Write to console (use Print instead)', insertText: 'Console.WriteLine(${1:value});', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                { label: 'DateTime.Now', kind: monaco.languages.CompletionItemKind.Property, detail: 'DateTime', documentation: 'Current date and time' },
                { label: 'DateTime.UtcNow', kind: monaco.languages.CompletionItemKind.Property, detail: 'DateTime', documentation: 'Current UTC date and time' },
                { label: 'Guid.NewGuid', kind: monaco.languages.CompletionItemKind.Method, detail: 'Guid', documentation: 'Generate a new GUID', insertText: 'Guid.NewGuid()', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                { label: 'string.IsNullOrEmpty', kind: monaco.languages.CompletionItemKind.Method, detail: 'bool', insertText: 'string.IsNullOrEmpty(${1:value})', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                { label: 'string.Join', kind: monaco.languages.CompletionItemKind.Method, detail: 'string', insertText: 'string.Join(${1:separator}, ${2:values})', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
            ];

            // String methods (when after a dot on a string-like variable)
            const stringMethods = [
                { label: 'ToUpper', kind: monaco.languages.CompletionItemKind.Method, detail: 'string', insertText: 'ToUpper()' },
                { label: 'ToLower', kind: monaco.languages.CompletionItemKind.Method, detail: 'string', insertText: 'ToLower()' },
                { label: 'Trim', kind: monaco.languages.CompletionItemKind.Method, detail: 'string', insertText: 'Trim()' },
                { label: 'Split', kind: monaco.languages.CompletionItemKind.Method, detail: 'string[]', insertText: 'Split(${1:separator})', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                { label: 'Replace', kind: monaco.languages.CompletionItemKind.Method, detail: 'string', insertText: 'Replace(${1:oldValue}, ${2:newValue})', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                { label: 'Contains', kind: monaco.languages.CompletionItemKind.Method, detail: 'bool', insertText: 'Contains(${1:value})', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                { label: 'StartsWith', kind: monaco.languages.CompletionItemKind.Method, detail: 'bool', insertText: 'StartsWith(${1:value})', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                { label: 'EndsWith', kind: monaco.languages.CompletionItemKind.Method, detail: 'bool', insertText: 'EndsWith(${1:value})', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                { label: 'Substring', kind: monaco.languages.CompletionItemKind.Method, detail: 'string', insertText: 'Substring(${1:startIndex})', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                { label: 'Length', kind: monaco.languages.CompletionItemKind.Property, detail: 'int', insertText: 'Length' },
            ];

            // LINQ methods
            const linqMethods = [
                { label: 'Where', kind: monaco.languages.CompletionItemKind.Method, detail: 'IEnumerable<T>', insertText: 'Where(${1:x} => ${2:condition})', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                { label: 'Select', kind: monaco.languages.CompletionItemKind.Method, detail: 'IEnumerable<TResult>', insertText: 'Select(${1:x} => ${2:x})', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                { label: 'FirstOrDefault', kind: monaco.languages.CompletionItemKind.Method, detail: 'T?', insertText: 'FirstOrDefault()' },
                { label: 'ToList', kind: monaco.languages.CompletionItemKind.Method, detail: 'List<T>', insertText: 'ToList()' },
                { label: 'ToArray', kind: monaco.languages.CompletionItemKind.Method, detail: 'T[]', insertText: 'ToArray()' },
                { label: 'Count', kind: monaco.languages.CompletionItemKind.Method, detail: 'int', insertText: 'Count()' },
                { label: 'Any', kind: monaco.languages.CompletionItemKind.Method, detail: 'bool', insertText: 'Any()' },
                { label: 'OrderBy', kind: monaco.languages.CompletionItemKind.Method, detail: 'IOrderedEnumerable<T>', insertText: 'OrderBy(${1:x} => ${2:x})', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                { label: 'Take', kind: monaco.languages.CompletionItemKind.Method, detail: 'IEnumerable<T>', insertText: 'Take(${1:count})', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
                { label: 'Skip', kind: monaco.languages.CompletionItemKind.Method, detail: 'IEnumerable<T>', insertText: 'Skip(${1:count})', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet },
            ];

            // Check if we're after a dot (member access)
            const lastChar = textUntilPosition.slice(-1);
            const beforeDot = textUntilPosition.slice(0, -1).trim();

            if (lastChar === '.') {
                // After a dot - show member completions
                [...stringMethods, ...linqMethods].forEach(item => {
                    suggestions.push({
                        ...item,
                        range: range
                    });
                });
            } else {
                // Not after a dot - show globals, snippets, and common types
                [...globals, ...methods, ...snippets, ...commonTypes].forEach(item => {
                    suggestions.push({
                        ...item,
                        range: range
                    });
                });
            }

            return { suggestions: suggestions };
        }
    });

    console.log('Script completions registered successfully');
};
