// Ace interop only. All rendering and animation lives in Razor/SVG/CSS.
const storageKey = "algoviz.script";
let state = null;

function load() {
    try { return localStorage.getItem(storageKey); } catch { return null; }
}

function save(value) {
    try { localStorage.setItem(storageKey, value); } catch { /* storage unavailable */ }
}

function themeName(dark) {
    return dark ? "ace/theme/tomorrow_night" : "ace/theme/tomorrow";
}

/** Creates the editor. Returns true when a saved script was restored from localStorage. */
export function init(element, dotnet, options) {
    dispose();
    ace.config.set("basePath", "lib/ace");
    const editor = ace.edit(element, {
        mode: "ace/mode/csharp",
        theme: themeName(options.dark),
        fontSize: 14,
        showPrintMargin: false,
        tabSize: 4,
        useSoftTabs: true,
        enableBasicAutocompletion: true,
        enableLiveAutocompletion: true,
    });

    const langTools = ace.require("ace/ext/language_tools");
    // Static list of Viz API members (built by the host via reflection); Ace filters it by prefix.
    const vizCompletions = options.completions.map(c => ({
        caption: c.caption, value: c.value, meta: c.meta, docText: c.doc, score: 1000,
    }));
    const vizCompleter = {
        getCompletions(_editor, _session, _pos, _prefix, callback) {
            callback(null, vizCompletions);
        },
    };
    editor.completers = [vizCompleter, langTools.keyWordCompleter, langTools.textCompleter];

    editor.commands.addCommand({
        name: "run",
        bindKey: { win: "Ctrl-Enter", mac: "Command-Enter" },
        exec: () => dotnet.invokeMethodAsync("RunFromEditor"),
    });

    // Keep typing in the editor from reaching the player's keyboard shortcuts.
    const stop = e => e.stopPropagation();
    element.addEventListener("keydown", stop);

    const saved = load();
    if (saved) editor.setValue(saved, -1);

    let saveTimer = 0;
    editor.session.on("change", () => {
        clearTimeout(saveTimer);
        saveTimer = setTimeout(() => save(editor.getValue()), 300);
    });

    const resizeObserver = new ResizeObserver(() => editor.resize());
    resizeObserver.observe(element);

    state = { editor, element, stop, resizeObserver, marker: null, errorMarker: null };
    return !!saved;
}

export function getValue() {
    return state ? state.editor.getValue() : "";
}

export function setValue(code) {
    if (!state) return;
    state.editor.setValue(code, -1);
    save(code);
}

/** diagnostics: [{ line, column, message, severity: "error" | "warning" }] (1-based). */
export function setAnnotations(diagnostics) {
    if (!state) return;
    const session = state.editor.session;
    session.setAnnotations(diagnostics.map(d => ({
        row: d.line - 1,
        column: Math.max(0, d.column - 1),
        text: d.message,
        type: d.severity,
    })));
    if (state.errorMarker !== null) session.removeMarker(state.errorMarker);
    state.errorMarker = null;
    const first = diagnostics.find(d => d.severity === "error" && d.line > 0);
    if (first) {
        const Range = ace.require("ace/range").Range;
        state.errorMarker = session.addMarker(new Range(first.line - 1, 0, first.line - 1, 1), "ace-error-line", "fullLine");
    }
}

export function highlightLine(line) {
    if (!state) return;
    clearHighlight();
    if (!line || line < 1) return;
    const Range = ace.require("ace/range").Range;
    state.marker = state.editor.session.addMarker(new Range(line - 1, 0, line - 1, 1), "ace-active-op", "fullLine");
    const renderer = state.editor.renderer;
    if (line - 1 < renderer.getFirstFullyVisibleRow() || line - 1 > renderer.getLastFullyVisibleRow()) {
        state.editor.scrollToLine(line - 1, true, false);
    }
}

export function clearHighlight() {
    if (!state || state.marker === null) return;
    state.editor.session.removeMarker(state.marker);
    state.marker = null;
}

export function setTheme(dark) {
    if (state) state.editor.setTheme(themeName(dark));
}

export function focus() {
    if (state) state.editor.focus();
}

export function dispose() {
    if (!state) return;
    state.resizeObserver.disconnect();
    state.element.removeEventListener("keydown", state.stop);
    state.editor.destroy();
    state = null;
}
