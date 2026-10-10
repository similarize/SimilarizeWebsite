# Similarize mark animation (v5, "Bridge", ~3.2 s)

**Live preview:** https://www.similarize.com/lab/mountain-hug/  (tap the mark to replay)
**Video preview:** `mountain-hug.mp4` / `mountain-hug.gif` in this folder.

## The story it tells
In *s-imi-larize* the two **i**'s are people facing each other and the **m** between them is a
mountain: a lifetime of different experience that separates every two minds.

1. 0.00–0.75 s  "Similarize" draws on in brand blue, letter by letter.
2. 0.55–1.05 s  A red disc blooms behind imi; the letters turn white and each i's dot opens its notch.
3. 0.85–1.50 s  The other letters part and the camera settles on the mark as the disc deepens to #900000.
4. 1.30–1.80 s  The m rises into a mountain between the two i's (two minds), nudging them apart.
5. 1.95–2.75 s  The mountain is removed: it drops back into the m with a soft spring, the i's lean in toward each other, and a line of light arcs from one to the other.
6. 2.75–3.20 s  Settles as the Similarize logo.

## Files
- `similarize-mark.js`: the whole animation. Pure SVG + JS, ~19 KB, no images, fonts or libraries. Vector, so sharp at any size.
- `index.html`: demo page with the tagline "removing the mountains between us" fading in at the end.

## Drop-in for the homepage
Replace the current `.logo-stage` block (the drive-collapse PNG layers) with:

```html
<div class="logo-stage" id="logo-stage" style="width:100%;aspect-ratio:1250/560"></div>
<script src="/lab/mountain-hug/similarize-mark.js?v=5"></script>
<script>
  SimilarizeMark.create(document.getElementById('logo-stage'), {
    autoplay: true,        // play on load
    pageBg: '#ffffff',     // page colour behind the word (used for the notch/outline colour before the disc appears)
    speed: 1,              // 1 = ~3.2 s
    clickToReplay: true,   // tap the mark to replay
    onDone: function () { /* e.g. fade in the tagline */ }
  });
</script>
```

API: `create()` returns `{ play(), stop(), seek(seconds), duration, svg }`.
`prefers-reduced-motion` users get the final logo with no motion.
The container sets the size; any aspect ratio works (the word view is wide, the logo view is square, both centred).

Built by Hark Bot for Bill, Oct 9, 2026.
