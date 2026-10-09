# Similarize "Mountain Hug" mark animation (v2: handshake + dissolve)

**Live preview:** https://www.similarize.com/lab/mountain-hug/  (tap the mark to replay)
**Video preview:** `mountain-hug.mp4` / `mountain-hug.gif` in this folder.

## The story it tells
In *s-imi-larize* the two **i**'s are people facing each other and the **m** between them is a
mountain: a lifetime of different experience that separates every two minds.

1. 0.0–1.9 s  The word **Similarize** appears (blue). A red disc rises behind **imi** and those letters turn white. Each i's dot opens its little notch "face".
2. 1.9–3.0 s  The other letters drift away; the camera settles on the imi disc, which deepens to brand red #900000.
3. 3.0–3.5 s  The two i's come alive: a hop, a wave, arms and legs appear.
4. 3.5–6.3 s  They shrink a little, step to the m, climb its outer walls hand over hand, and walk over the humps toward each other.
5. 6.3–8.0 s  They meet at the top and shake hands (reach in, two pumps, hold).
6. 7.85–8.85 s Seamless dissolve: the two people fade out on the summit while the logo's i's fade in at home.
7. 8.85–9.5 s Hold on the exact Similarize logo (white imi on a #900000 disc).

## Files
- `similarize-mark.js`: the whole animation. Pure SVG + JS, ~19 KB, no images, fonts or libraries. Vector, so sharp at any size.
- `index.html`: demo page with the tagline "removing the mountains between us" fading in at the end.

## Drop-in for the homepage
Replace the current `.logo-stage` block (the drive-collapse PNG layers) with:

```html
<div class="logo-stage" id="logo-stage" style="width:100%;aspect-ratio:1250/560"></div>
<script src="/lab/mountain-hug/similarize-mark.js?v=2"></script>
<script>
  SimilarizeMark.create(document.getElementById('logo-stage'), {
    autoplay: true,        // play on load
    pageBg: '#ffffff',     // page colour behind the word (used for the notch/outline colour before the disc appears)
    speed: 1,              // 1 = ~9.5 s; 1.3 = ~7.3 s
    clickToReplay: true,   // tap the mark to replay
    onDone: function () { /* e.g. fade in the tagline */ }
  });
</script>
```

API: `create()` returns `{ play(), stop(), seek(seconds), duration, svg }`.
`prefers-reduced-motion` users get the final logo with no motion.
The container sets the size; any aspect ratio works (the word view is wide, the logo view is square, both centred).

Built by Hark Bot for Bill, Oct 9, 2026.
