// Ace interop only. Grids, plan trees, the schema explorer and the ER diagram are Razor + HTML/SVG + CSS.
const scriptKey = "sqliteviz.script";
const themeKey = "sqliteviz.theme";
let state = null;

function load(key) {
    try { return localStorage.getItem(key); } catch { return null; }
}

function save(key, value) {
    try { localStorage.setItem(key, value); } catch { /* storage unavailable */ }
}

function aceTheme(dark) {
    return dark ? "ace/theme/tomorrow_night" : "ace/theme/tomorrow";
}

/** True when the page should start dark: the saved choice, else the OS preference. */
export function prefersDark() {
    const saved = load(themeKey);
    if (saved) return saved === "dark";
    return window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches;
}

/**
 * Creates the editor. options: { dark, completions: [{ value, caption, meta, doc }], schemaWords: [{ value, meta }] }.
 * Returns true when a saved script was restored from localStorage.
 */
export function init(element, dotnet, options) {
    dispose();
    ace.config.set("basePath", "lib/ace");
    const editor = ace.edit(element, {
        mode: "ace/mode/sql",
        theme: aceTheme(options.dark),
        fontSize: 14,
        showPrintMargin: false,
        tabSize: 4,
        useSoftTabs: true,
        enableBasicAutocompletion: true,
        enableLiveAutocompletion: true,
    });

    // Static keywords and functions (from the host), plus live table/column names (replaced after each run).
    const staticCompletions = options.completions.map(c => ({
        caption: c.caption, value: c.value, meta: c.meta, docText: c.doc || undefined,
        score: c.meta === "keyword" ? 100 : 200,
    }));
    const completer = {
        getCompletions(_editor, _session, _pos, _prefix, callback) {
            callback(null, staticCompletions.concat(state ? state.schemaCompletions : []));
        },
    };
    const langTools = ace.require("ace/ext/language_tools");
    editor.completers = [completer, langTools.textCompleter];

    editor.commands.addCommand({
        name: "runAll",
        bindKey: { win: "Ctrl-Enter", mac: "Command-Enter" },
        exec: () => dotnet.invokeMethodAsync("RunAllFromEditor"),
    });
    editor.commands.addCommand({
        name: "runStatement",
        bindKey: { win: "Ctrl-Shift-Enter", mac: "Command-Shift-Enter" },
        exec: () => dotnet.invokeMethodAsync("RunStatementFromEditor"),
    });

    const saved = load(scriptKey);
    if (saved) editor.setValue(saved, -1);

    let saveTimer = 0;
    editor.session.on("change", () => {
        clearTimeout(saveTimer);
        saveTimer = setTimeout(() => save(scriptKey, editor.getValue()), 300);
    });

    const resizeObserver = new ResizeObserver(() => editor.resize());
    resizeObserver.observe(element);

    state = { editor, element, resizeObserver, marker: null, errorMarker: null, schemaCompletions: [] };
    setCompletions(options.schemaWords || []);
    editor.focus();
    return !!saved;
}

export function getValue() {
    return state ? state.editor.getValue() : "";
}

export function setValue(sql) {
    if (!state) return;
    state.editor.setValue(sql, -1);
    state.editor.scrollToLine(0, false, false);
    save(scriptKey, sql);
}

/**
 * What Ctrl+Shift+Enter should run. Returns the selection when there is one; otherwise the whole script
 * plus the cursor, and the host picks the statement with SQLite's own tokenizer. Positions are 1-based.
 */
export function getSelectionOrStatementAtCursor() {
    if (!state) return null;
    const editor = state.editor;
    const range = editor.getSelectionRange();
    const selection = editor.session.getTextRange(range);
    const cursor = editor.getCursorPosition();
    return {
        selection: selection.trim() ? selection : null,
        startLine: range.start.row + 1,
        startColumn: range.start.column + 1,
        script: editor.getValue(),
        cursorLine: cursor.row + 1,
        cursorColumn: cursor.column + 1,
    };
}

/** errors: [{ line, endLine, column, message }] (1-based). Marks the failing statement and annotates its line. */
export function setAnnotations(errors) {
    if (!state) return;
    const session = state.editor.session;
    session.setAnnotations(errors.map(e => ({
        row: e.line - 1,
        column: Math.max(0, (e.column || 1) - 1),
        text: e.message,
        type: "error",
    })));
    if (state.errorMarker !== null) session.removeMarker(state.errorMarker);
    state.errorMarker = null;
    const first = errors[0];
    if (first) {
        const Range = ace.require("ace/range").Range;
        state.errorMarker = session.addMarker(
            new Range(first.line - 1, 0, (first.endLine || first.line) - 1, Infinity), "ace-error-range", "fullLine");
        scrollIntoView(first.line);
    }
}

export function highlightRange(startLine, endLine) {
    if (!state) return;
    clearHighlight();
    const Range = ace.require("ace/range").Range;
    state.marker = state.editor.session.addMarker(
        new Range(startLine - 1, 0, endLine - 1, Infinity), "ace-statement-highlight", "fullLine");
    scrollIntoView(startLine);
}

export function clearHighlight() {
    if (!state || state.marker === null) return;
    state.editor.session.removeMarker(state.marker);
    state.marker = null;
}

/** schemaWords: [{ value, meta }] from the last schema snapshot. */
export function setCompletions(schemaWords) {
    if (!state) return;
    state.schemaCompletions = schemaWords.map(w => ({ caption: w.value, value: w.value, meta: w.meta, score: 300 }));
}

export function setTheme(dark) {
    save(themeKey, dark ? "dark" : "light");
    if (state) state.editor.setTheme(aceTheme(dark));
}

export function focus() {
    if (state) state.editor.focus();
}

function scrollIntoView(line) {
    const renderer = state.editor.renderer;
    if (line - 1 < renderer.getFirstFullyVisibleRow() || line - 1 > renderer.getLastFullyVisibleRow()) {
        state.editor.scrollToLine(line - 1, true, true);
    }
}

export function dispose() {
    if (!state) return;
    state.resizeObserver.disconnect();
    state.editor.destroy();
    state = null;
}
