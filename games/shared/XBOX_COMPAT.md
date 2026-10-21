# Xbox / console Edge compatibility (`xbox-compat.js`, xb1, 2026-10-20)

One shared, dependency-free script for Unity WebGL/WebGPU pages that run on Xbox Series/One Edge. First user: `four-froggies-u6`.

## Include (before the Unity loader script)

```html
<script src="../shared/xbox-compat.js?v=xbp5"></script>
```

Optional, before the include: `window.SimXboxOptions = { canvas: "#unity-canvas" }` (the default is `#unity-canvas`, then the first `<canvas>`).

## Config snippet

```js
var config = { dataUrl: ..., frameworkUrl: ..., codeUrl: ..., devicePixelRatio: myDpr, ... };
if (window.SimXbox) SimXbox.applyConfig(config);   // low-mem: DPR capped (<=1, backbuffer <=1280x720), no IndexedDB data cache
createUnityInstance(canvas, config, onProgress).then(function (inst) { window.unityInstance = inst; /* the gate keys off this */ });
```

If the game changes DPR at runtime (as FF does through FFSetDPR), feed it `SimXbox.capDpr(raw)` when `SimXbox.lowmem` is set. It's also kind on memory to turn off any forced canvas MSAA when `SimXbox.lowmem` is set.

## What it does

| Trigger | Effect |
|---|---|
| UA contains `Xbox`, or `?lowmem=1` (`?lowmem=0` turns it off) | `SimXbox.lowmem = true`: hides `navigator.gpu` (forces WebGL 2), and `capDpr`/`applyConfig` cap the render size |
| `?webgpu=0` | hides `navigator.gpu` only |
| always | `navigator.gamepadInputEmulation = "gamepad"` where it exists; canvas re-focus on `fullscreenchange`, `visibilitychange`, window `focus` and `gamepadconnected` |
| Xbox (or `?padgate=1`; `?padgate=0` turns it off), once `window.unityInstance` exists and before any pad button | top strip: "Move the cursor here, hold Menu, choose Use game controls, then press A". It isn't over the canvas and sits under the page toolbar. A click (A in cursor mode) focuses the canvas and requests fullscreen. The first real pad button or stick input hides it. |
| `?padlog=1` | once a second, console plus an on-screen box: each pad's id and **mapping** (standard or not), pressed buttons, axes, focus, active element, fullscreen, pointer lock, WebGPU |

API: `window.SimXbox = { isXbox, lowmem, forceWebGL2, dprCap: 1, maxCanvas: [1280, 720], padSeen, capDpr(raw), applyConfig(cfg), canvas(), refocus() }`.

## Why

- **Crash at "Loading 0%":** Unity 6 loaders call `navigator.gpu.requestAdapter()` before downloading anything (0% is shown right then), and the WebGPU path then allocates GPU memory. Xbox Edge has very little per-app memory. Hiding `navigator.gpu` makes the loader and engine take the WebGL 2 path, as long as the build lists OpenGLES3 too.
- **Controller dead / stuck in cursor mode:** Edge for Xbox starts in *browsing controls* (the pad drives a cursor, and the page gets no Gamepad API input). The user switches by **holding** Menu and choosing *Use game controls*; a tap does nothing. That toggle reportedly only works when the cursor is outside the game area (ppeccin/WebMSX#68), which is why the strip exists. The View button / fullscreen toggles can drop page focus; opening and closing the Xbox Guide restores it (webrcade docs).

## Verified vs assumed

Verified, in headless Chrome 155 with the Xbox Edge UA `...Xbox; Xbox One... Edg/130...`, on four-froggies-u6:
- WebGL 2 path is taken (`U6: graphics API OpenGLES3`, `WebGL2RenderingContext`), the load gets past 0% to the lobby, the canvas backbuffer is 1280x720 at a 1920x1080 viewport, and no console errors.
- The desktop UA still takes WebGPU, unchanged from before the change (it had the same SwiftShader-only WebGPU validation noise before and after).
- The gate appears, clicking it focuses the canvas and enters fullscreen, focus returns to the canvas after fullscreen changes, and a (stubbed) standard pad's A press hides the gate. The padlog box shows id and mapping.

Assumed, not testable here (needs a real Xbox):
- That the Xbox crash is the WebGPU and memory path. The 1 GB max heap may still be too much; see the Unity settings below.
- That current Chromium Edge ignores `gamepadInputEmulation`. It is set anyway and is harmless.
- That Edge exposes the pad through `navigator.getGamepads()` only in *game controls*, and only after a button press. Chromium generally needs a button press after connect.
- That fullscreen or pointer lock does **not** by itself release the controller to the page. No evidence that it does, so the strip tells the user to switch modes.
- The cursor-outside-the-game-area detail comes from one third-party report.

## Per-build Unity settings to check (BuildScript / Player Settings, WebGL)

1. **Graphics APIs include OpenGLES3 (WebGL 2)**, for example `{ WebGPU, OpenGLES3 }`. Without it, hiding WebGPU leaves nothing to render with.
2. **Maximum memory size:** keep it as low as the game fits. 1024 MB is too much for Xbox Edge, which shares about 1 GB across processes. Aim for **512 MB** (`PlayerSettings.WebGL.maximumMemorySize = 512`), profile peak heap with the memory profiler, and trim textures (max size / ASTC/DXT crunch) and data if it doesn't fit. Leave initial memory at 128 MB or lower (64 MB).
3. **Compression: Brotli** (`PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli`, `decompressionFallback = false` since the server sends `Content-Encoding: br`). Cloudflare compresses `.wasm`/`.js` on the fly but serves the `.data` raw, so make sure the host sends `.br` files with the right headers.
4. **Stripping:** `stripEngineCode = true` and Managed Stripping Level Medium/High (with `link.xml` for reflection) shrink the wasm, so compiling it needs less memory.
5. `dataCaching`: optional. The page already turns it off at runtime in low-memory mode.

Status of current builds (checked 2026-10-20 from `Build/*` on gh-pages):

| Game | WebGPU in build | WebGL 2 in build | wasm | Heap init / max |
|---|---|---|---|---|
| four-froggies-u6 | yes | yes | 45.9 MB, uncompressed | 128 / 1024 MB |
| lambo-blast-u6 | yes | yes | 40 MB | 128 / 1024 MB |
| four-froggies-unity | no (WebGL 2 only) | yes | 35 MB | 128 / 1024 MB |
| balloon-blast-unity | no | yes | 31 MB | 128 / 1024 MB |
| lambo-blast | no | yes | 30 MB | 128 / 1024 MB |

## xbp3 (2026-10-20, Four Froggies World)
- Pointer lock is blocked on Xbox (`Element.prototype.requestPointerLock` becomes a no-op). Unity `Cursor.lockState = Locked` grabbed the cursor when A acted as a click, which kept HOLD Menu -> "Use game controls" from opening. `?lock=1` allows it, and `?lock=0` blocks it on any browser. Padlog shows `nolock=`.
- Don't map gamepad Menu (button 9) or View (button 8) in games. Edge on Xbox reserves them: holding Menu goes back to cursor mode, and View takes focus.
- Unity WebGL templates must reference the current `?v=`. A Unity publish that ships an older template resets the bump; FF u6 and FF unity had gone back to `xb1`.
