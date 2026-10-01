/* ============================================================
   Icon set — CLAUDE.md §5.

   Lucide-style 24x24 stroke shapes, stored as structured data rather than
   raw SVG strings. Rendering them element-by-element (see ui/icon.ts) means
   the app never has to hand markup to `innerHTML` or the DomSanitizer at
   all, which is a stricter reading of §9 than Solution A managed.
   ============================================================ */

export type IconShape =
  | { kind: 'path'; d: string }
  | { kind: 'circle'; cx: number; cy: number; r: number }
  | { kind: 'rect'; x: number; y: number; w: number; h: number; rx: number };

const p = (d: string): IconShape => ({ kind: 'path', d });
const c = (cx: number, cy: number, r: number): IconShape => ({ kind: 'circle', cx, cy, r });
const r = (x: number, y: number, w: number, h: number, rx: number): IconShape => ({
  kind: 'rect',
  x,
  y,
  w,
  h,
  rx,
});

export const ICON_SHAPES: Record<string, IconShape[]> = {
  /* ---- task icons (offered in the picker) ---- */
  cat: [
    p(
      'M12 5c.67 0 1.35.09 2 .26 1.78-2 5.03-2.84 6.42-2.26 1.4.58-.42 7-.42 7 .57 1.07 1 2.24 1 3.44C21 17.9 16.97 21 12 21s-9-3.1-9-7.56c0-1.25.43-2.37 1-3.44 0 0-1.82-6.42-.42-7 1.39-.58 4.64.26 6.42 2.26C11.65 5.09 12.33 5 12 5Z',
    ),
    p('M8 14v.5'),
    p('M16 14v.5'),
    p('M11.25 16.25h1.5L12 17l-.75-.75Z'),
  ],
  house: [p('m3 9 9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2Z'), p('M9 22V12h6v10')],
  work: [r(2, 7, 20, 14, 2), p('M16 21V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16')],
  gym: [
    p('m6.5 6.5 11 11'),
    p('m21 21-1-1'),
    p('m3 3 1 1'),
    p('m18 22 4-4'),
    p('m2 6 4-4'),
    p('m3 10 7-7'),
    p('m14 21 7-7'),
  ],
  nature: [
    p('M10 10v.2A3 3 0 0 1 8.9 16H5a3 3 0 0 1-1-5.8V10a3 3 0 0 1 6 0Z'),
    p('M7 16v6'),
    p('M13 19v3'),
    p(
      'M12 19h8.3a1 1 0 0 0 .7-1.7L18 14h.3a1 1 0 0 0 .7-1.7L16 9h.2a1 1 0 0 0 .8-1.7L13 3l-1.4 1.5',
    ),
  ],
  food: [
    p('M3 2v7a3 3 0 0 0 3 3 3 3 0 0 0 3-3V2'),
    p('M6 12v10'),
    p('M21 15V2a5 5 0 0 0-5 5v6c0 1.1.9 2 2 2h3Zm0 0v7'),
  ],
  shop: [
    c(9, 20, 1.5),
    c(18, 20, 1.5),
    p('M2 3h2.2a1 1 0 0 1 1 .8L6 7m0 0 1.6 7.6a2 2 0 0 0 2 1.6h8a2 2 0 0 0 2-1.6L21 7Z'),
  ],
  clean: [
    p(
      'm12 3 1.9 5.8a2 2 0 0 0 1.3 1.3L21 12l-5.8 1.9a2 2 0 0 0-1.3 1.3L12 21l-1.9-5.8a2 2 0 0 0-1.3-1.3L3 12l5.8-1.9a2 2 0 0 0 1.3-1.3Z',
    ),
    p('M5 3v4'),
    p('M3 5h4'),
    p('M19 17v4'),
    p('M17 19h4'),
  ],
  task: [p('M21 10.5V19a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11'), p('m9 11 3 3L22 4')],
  document: [
    p('M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8Z'),
    p('M14 2v6h6'),
    p('M16 13H8'),
    p('M16 17H8'),
    p('M10 9H8'),
  ],
  money: [
    p('M19 7V5a1 1 0 0 0-1-1H5a2 2 0 0 0 0 4h15a1 1 0 0 1 1 1v3h-3a2 2 0 0 0 0 4h3a1 1 0 0 1-1 1'),
    p('M3 6v13a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-4'),
  ],
  battery: [
    p('M15 7h1a2 2 0 0 1 2 2v6a2 2 0 0 1-2 2h-2'),
    p('M7 7H4a2 2 0 0 0-2 2v6a2 2 0 0 0 2 2h2'),
    p('m11 6-3 6h4l-3 6'),
    p('M22 11v2'),
  ],

  /* ---- UI-only icons ---- */
  ui_home: [p('m3 9 9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2Z'), p('M9 22V12h6v10')],
  ui_list: [
    p('M8 6h13'),
    p('M8 12h13'),
    p('M8 18h13'),
    p('M3 6h.01'),
    p('M3 12h.01'),
    p('M3 18h.01'),
  ],
  ui_settings: [
    c(12, 12, 3),
    p(
      'M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.6h.09A1.65 1.65 0 0 0 10 3.09V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9v.09a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1Z',
    ),
  ],
  ui_back: [p('m15 18-6-6 6-6')],
  ui_plus: [p('M12 5v14'), p('M5 12h14')],
  ui_edit: [p('M17 3a2.83 2.83 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5Z')],
  ui_trash: [
    p('M3 6h18'),
    p('M8 6V4a1 1 0 0 1 1-1h6a1 1 0 0 1 1 1v2'),
    p('M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6'),
    p('M10 11v6'),
    p('M14 11v6'),
  ],
  ui_check: [p('M20 6 9 17l-5-5')],
  ui_undo: [p('M3 7v6h6'), p('M3 13a9 9 0 1 0 3-7.7L3 8')],
  ui_download: [p('M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4'), p('M7 10l5 5 5-5'), p('M12 15V3')],
  ui_upload: [p('M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4'), p('M17 8l-5-5-5 5'), p('M12 3v12')],
  ui_x: [p('M18 6 6 18'), p('m6 6 12 12')],
};

/** The task icon set offered in the picker (CLAUDE.md §5). */
export const ICON_SET: ReadonlyArray<{ key: string; label: string }> = [
  { key: 'cat', label: 'Pets' },
  { key: 'house', label: 'Home' },
  { key: 'work', label: 'Work' },
  { key: 'gym', label: 'Gym' },
  { key: 'nature', label: 'Nature' },
  { key: 'food', label: 'Food' },
  { key: 'shop', label: 'Shopping' },
  { key: 'clean', label: 'Cleaning' },
  { key: 'task', label: 'Task' },
  { key: 'document', label: 'Papers' },
  { key: 'money', label: 'Money' },
  { key: 'battery', label: 'Charging' },
];

const ICON_LABELS = new Map(ICON_SET.map((i) => [i.key, i.label]));

/** Unknown icon keys fall back to `task` (CLAUDE.md §5). */
export function iconKey(k: unknown): string {
  return typeof k === 'string' && ICON_LABELS.has(k) ? k : 'task';
}

export function iconLabel(k: string): string {
  return ICON_LABELS.get(k) ?? k;
}

export function isIconKey(k: string): boolean {
  return ICON_LABELS.has(k);
}
