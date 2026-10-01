import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { ICON_SHAPES } from '../core/icons';

/** Renders one of the app's built-in icons (CLAUDE.md §5).

    The shapes are structured data, not markup, so nothing here is ever
    parsed as HTML — no `innerHTML`, no sanitizer bypass (§9). Colour comes
    from `currentColor`, so an icon inherits whatever the surrounding text
    is using. */
@Component({
  selector: 'app-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'ic' },
  template: `
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      stroke-width="2"
      stroke-linecap="round"
      stroke-linejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      @for (s of shapes(); track $index) {
        @switch (s.kind) {
          @case ('path') {
            <svg:path [attr.d]="s.d" />
          }
          @case ('circle') {
            <svg:circle [attr.cx]="s.cx" [attr.cy]="s.cy" [attr.r]="s.r" />
          }
          @case ('rect') {
            <svg:rect
              [attr.x]="s.x"
              [attr.y]="s.y"
              [attr.width]="s.w"
              [attr.height]="s.h"
              [attr.rx]="s.rx"
            />
          }
        }
      }
    </svg>
  `,
})
export class Icon {
  readonly name = input.required<string>();
  protected readonly shapes = computed(() => ICON_SHAPES[this.name()] ?? ICON_SHAPES['task']);
}
