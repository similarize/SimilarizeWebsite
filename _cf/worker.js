// similarize-site: Workers Static Assets serve everything that exists. This script only runs on
// asset misses: it reassembles files over 25 MiB from cf-parts/ (see _cf/build.mjs) and returns 404s.
import BIG from "./bigfiles.json";

const CORS = { "access-control-allow-origin": "*" };

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    const isRead = request.method === "GET" || request.method === "HEAD";
    const big = isRead && BIG[url.pathname];
    if (big) return serveBig(request, env, url, big);
    return notFound(request);
  },
};

async function serveBig(request, env, url, big) {
  const headers = new Headers({
    ...CORS,
    "content-type": big.type,
    "content-length": String(big.size),
    "cache-control": "public, max-age=600",
    "accept-ranges": "none",
    etag: `"big-${big.size}-${big.parts.length}-${hash(big.parts.join("|"))}"`,
  });
  if (request.headers.get("if-none-match") === headers.get("etag")) return new Response(null, { status: 304, headers });
  if (request.method === "HEAD") return new Response(null, { status: 200, headers });

  const { readable, writable } = new FixedLengthStream(big.size);
  (async () => {
    try {
      for (const p of big.parts) {
        const r = await env.ASSETS.fetch(new URL(p, url.origin));
        if (!r.ok || !r.body) throw new Error(`part ${p} -> ${r.status}`);
        await r.body.pipeTo(writable, { preventClose: true });
      }
      await writable.close();
    } catch (e) {
      console.error("big file stream failed", url.pathname, String(e));
      await writable.abort(e);
    }
  })();
  return new Response(readable, { status: 200, headers });
}

function hash(s) {
  let h = 2166136261;
  for (let i = 0; i < s.length; i++) h = Math.imul(h ^ s.charCodeAt(i), 16777619);
  return (h >>> 0).toString(16);
}

function notFound(request) {
  const body = request.method === "HEAD" ? null : `<!DOCTYPE html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>Page not found · Similarize</title>
<style>body{font:16px/1.5 system-ui,-apple-system,sans-serif;max-width:32rem;margin:15vh auto;padding:0 1.5rem;color:#222;text-align:center}a{color:#008bff}</style>
</head><body><h1>404</h1><p>That page isn't here.</p><p><a href="https://www.similarize.com/">Back to Similarize</a></p></body></html>`;
  return new Response(body, { status: 404, headers: { ...CORS, "content-type": "text/html; charset=utf-8", "cache-control": "no-cache" } });
}
