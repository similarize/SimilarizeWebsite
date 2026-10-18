export default {
  fetch(request) {
    const url = new URL(request.url);
    return new Response(null, {
      status: 301,
      headers: { location: `https://www.similarize.com${url.pathname}${url.search}`, "cache-control": "public, max-age=86400" },
    });
  },
};
