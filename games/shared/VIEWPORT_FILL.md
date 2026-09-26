# Viewport fill + browser Fullscreen API

## Why Tesla helper was not enough

`tesla-fullscreen.js` only runs on Tesla UA (YouTube redirect for chromeless car browser). On desktop / phone Chrome, Safari, Edge it is a **no-op**. Ben’s “whole screen” request still needs:

1. CSS/layout so the play canvas fills the **layout viewport** (no letterbox / flex chrome eating height).
2. Optional **Fullscreen API** (`requestFullscreen`) on a user gesture to hide browser chrome.

## Shared helper

File: `games/shared/viewport-fill.js` (`?v=vfs1`)  
API: `window.SimilarizeViewportFS = { ensureCss(), enter(), exit(), isActive(), bindGesture(el) }`

- Injects minimal `html,body { 100dvh; overflow:hidden; margin:0 }` CSS (no `touch-action` — Maps needs pan).
- `enter()` calls Fullscreen API on `documentElement`. Browsers require a user gesture; Start / GO / first tap hooks call it.

Keep loading **after** `tesla-fullscreen.js` in live entry HTML.

## Per-game notes

| Game | Layout fix | Fullscreen gesture |
| --- | --- | --- |
| RC Rally Jump | Removed 16:9 letterbox; stretch VIEW_W×VIEW_H to canvas | Start / begin() |
| Soccer RC (`basketball.html`) | Top + pads are overlays; `.stage` is `inset:0` | Canvas / Sound / Reset |
| Asteroids | `#view` absolute fill + 100dvh | Title overlay start |
| Four Froggies | `#view` absolute fill; lobby unchanged | GO button |
| Book of Mormon Maps | 100dvh + overflow hidden | Map click / bar |

## Limitations

- Browser Fullscreen API **needs a click/tap/key** — cannot auto-enter on load.
- Esc exits browser fullscreen (expected).
- Tesla path still uses YouTube redirect separately; desktop does not.
- RC Rally stretch can mildly distort on extreme aspect ratios (preferred over letterbox bars).
