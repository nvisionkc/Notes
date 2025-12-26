// Editor JavaScript Interop

export function initEditor(editorId, content) {
    const editor = document.getElementById(editorId);
    if (editor) {
        editor.innerHTML = content || '';
        editor.focus();
    }
}

export function setContent(editorId, content) {
    const editor = document.getElementById(editorId);
    if (editor && editor.innerHTML !== content) {
        editor.innerHTML = content || '';
    }
}

export function getContent(editorId) {
    const editor = document.getElementById(editorId);
    return editor ? editor.innerHTML : '';
}

export function execCommand(command, value = null) {
    document.execCommand(command, false, value);
}

export function formatBlock(tag) {
    document.execCommand('formatBlock', false, `<${tag}>`);
}

export function focusEditor(editorId) {
    const editor = document.getElementById(editorId);
    if (editor) {
        editor.focus();
    }
}

// Setup keyboard shortcut handler to prevent default browser behavior
export function setupKeyboardHandler(dotNetRef) {
    document.addEventListener('keydown', async (e) => {
        if (e.ctrlKey) {
            if (e.key === 's' || e.key === 'S') {
                e.preventDefault();
                await dotNetRef.invokeMethodAsync('HandleSaveShortcut');
            } else if (e.key === 'n' || e.key === 'N') {
                e.preventDefault();
                await dotNetRef.invokeMethodAsync('HandleNewShortcut');
            }
        }
    });
}
