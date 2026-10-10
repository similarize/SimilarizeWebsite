# Similarize "Mountain Hug" mark animation (v4: mountain climb + high five)

**Live preview:** https://www.similarize.com/lab/mountain-hug/  (tap the mark to replay)
**Video preview:** `mountain-hug.mp4` / `mountain-hug.gif` in this folder.

## The story it tells
In *s-imi-larize* the two **i**'s are people facing each other and the **m** between them is a
mountain: a lifetime of different experience that separates every two minds.

1. 0.0–1.9 s  The word **Similarize** appears (blue). A red disc rises behind **imi** and those letters turn white. Each i's dot opens its little notch "face".
2. 1.9–3.0 s  The other letters drift away; the camera settles on the imi disc, which deepens to brand red #900000.
3. 3.0–3.5 s  The two i's come alive: a hop, a wave, arms and legs appear.
4. 3.45–5.05 s The m mutates into the outline of a mountain (valley rises into the peak, humps become crags, legs spread into the base). The disc and view widen with it; the people glide back to its foot. They stay full size; the mountain is what grows.
5. 5.05–8.2 s Hand-over-hand climb up opposite faces: bodies lean into the slope, hands reach and grip, legs trail and push softly.
6. 8.25–9.05 s At the summit they straighten up and high-five, with a small burst where the hands meet.
7. 8.95–9.75 s They jump together with both arms up (cheer) and land.
8. 9.8–10.9 s The mountain scene fades out as the resting Similarize logo fades in at exactly its final position.
9. 10.9–11.6 s The logo holds (white imi on a #900000 disc).

## Files
- `similarize-mark.js`: the whole animation. Pure SVG + JS, ~19 KB, no images, fonts or libraries. Vector, so sharp at any size.
- `index.html`: demo page with the tagline "removing the mountains between us" fading in at the end.

## Drop-in for the homepage
Replace the current `.logo-stage` block (the drive-collapse PNG layers) with:

```html
<div class="logo-stage" id="logo-stage" style="width:100%;aspect-ratio:1250/560"></div>
<script src="/lab/mountain-hug/similarize-mark.js?v=4"></script>
<script>
  SimilarizeMark.create(document.getElementById('logo-stage'), {
    autoplay: true,        // play on load
    pageBg: '#ffffff',     // page colour behind the word (used for the notch/outline colour before the disc appears)
    speed: 1,              // 1 = ~11.6 s; 1.3 = ~8.9 s
    clickToReplay: true,   // tap the mark to replay
    onDone: function () { /* e.g. fade in the tagline */ }
  });
</script>
```

API: `create()` returns `{ play(), stop(), seek(seconds), duration, svg }`.
`prefers-reduced-motion` users get the final logo with no motion.
The container sets the size; any aspect ratio works (the word view is wide, the logo view is square, both centred).

Built by Hark Bot for Bill, Oct 9, 2026.
