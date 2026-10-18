# similarize.com on Cloudflare Workers (static assets)

Replaces GitHub Pages so this repo can go private. Mirrors Still-Has-Value/StillHasValue.

- Worker: `similarize-site` (account ben-e22). Config: `/wrangler.jsonc`, ignore list: `/.assetsignore`, headers: `/_headers`.
- Workers Builds (branch `gh-pages`): build command `node _cf/build.mjs`, deploy command `npx wrangler deploy`.
- `_cf/build.mjs` does what Pages/Jekyll did that matters: strips front matter from `index.html`, and splits any
  file over 25 MiB (Workers asset limit) into `cf-parts/…partN`. `_cf/worker.js` streams the parts back at the
  original URL (same bytes, same Content-Length). It runs only on asset misses, so normal files cost no invocations.
- Anything starting with `_` or `.` is not published (same as Jekyll). Raw `*.md` files are published (Pages did too).
- `www.similarize.com/p` and `/p/*` belong to the `similarize-private` Worker (routes beat the Custom Domain).
- `similarize.com` (bare) → www 301 is the `similarize-apex` Worker in `_cf/apex/`.
- Local deploy: `node _cf/build.mjs && npx wrangler deploy`, then `git checkout index.html .assetsignore`.

Pushing to gh-pages (Hark, Unity workflows, people) deploys automatically once Workers Builds is connected.
Don't commit `cf-parts/` or `_cf/bigfiles.json`; the build makes them.
