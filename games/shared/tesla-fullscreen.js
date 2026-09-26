/*! SimilarizeTeslaFS — Tesla Cybertruck chromeless fullscreen via YouTube redirect.
 *
 * Tesla in-car browser keeps its address bar/toolbar on normal navigations.
 * Opening through https://www.youtube.com/redirect?q=<ENCODED_URL> (same trick
 * used by Fullify / TeslaTube / FullscreenTesla) lands the game chromeless.
 *
 * Drop in with one script tag BEFORE gamepad/game scripts. Auto-runs on load.
 * Desktop / non-Tesla: no-op. Anti-loop via tesla_fs=1, youtube referrer,
 * and sessionStorage per path. Force-test: ?tesla=1 or localStorage
 * similarize_tesla_fs=1.
 *
 * API (window.SimilarizeTeslaFS):
 *   maybeRedirect()      — run once; returns true if a redirect was started
 *   isTesla()            — UA / force-flag heuristics
 *   alreadyFullscreen()  — tesla_fs=1 or youtube.com referrer
 */
(function (global) {
  "use strict";

  var FLAG = "tesla_fs";
  var FORCE_QS = "tesla";
  var FORCE_LS = "similarize_tesla_fs";
  var SESSION_PREFIX = "similarize_tesla_fs_done:";
  var YT_REDIRECT = "https://www.youtube.com/redirect?q=";

  function safeUrl() {
    try {
      return new URL(global.location.href);
    } catch (e) {
      return null;
    }
  }

  function isTesla() {
    var u = safeUrl();
    if (u && u.searchParams.get(FORCE_QS) === "1") return true;
    try {
      if (global.localStorage && global.localStorage.getItem(FORCE_LS) === "1") {
        return true;
      }
    } catch (e) {}
    var ua = String(
      (global.navigator && global.navigator.userAgent) || ""
    );
    // Classic QtCarBrowser; modern Chromium builds include "Tesla/YYYY…";
    // some strings also say Tesla QtCarBrowser.
    if (/QtCarBrowser/i.test(ua)) return true;
    if (/\bTesla\b/i.test(ua)) return true;
    return false;
  }

  function alreadyFullscreen() {
    var u = safeUrl();
    if (u && u.searchParams.get(FLAG) === "1") return true;
    var ref = String((global.document && global.document.referrer) || "");
    if (/youtube\.com/i.test(ref)) return true;
    return false;
  }

  function sessionKey() {
    var path = (global.location && global.location.pathname) || "/";
    return SESSION_PREFIX + path;
  }

  function alreadyRedirectedThisSession() {
    try {
      return (
        global.sessionStorage &&
        global.sessionStorage.getItem(sessionKey()) === "1"
      );
    } catch (e) {
      return false;
    }
  }

  function markRedirected() {
    try {
      if (global.sessionStorage) {
        global.sessionStorage.setItem(sessionKey(), "1");
      }
    } catch (e) {}
  }

  /** Absolute https game URL: keep path + existing query, add tesla_fs=1, drop hash. */
  function buildTargetUrl() {
    var u = safeUrl();
    if (!u) return "";
    u.hash = "";
    // Preserve party/invite/query params; only set/overwrite our anti-loop flag.
    u.searchParams.set(FLAG, "1");
    // Prefer https if the page was somehow served over http on production host.
    if (u.protocol === "http:" && /\.similarize\.com$/i.test(u.hostname)) {
      u.protocol = "https:";
    }
    return u.toString();
  }

  function maybeRedirect() {
    if (!isTesla()) return false;
    if (alreadyFullscreen()) return false;
    if (alreadyRedirectedThisSession()) return false;
    var target = buildTargetUrl();
    if (!target) return false;
    markRedirected();
    global.location.replace(YT_REDIRECT + encodeURIComponent(target));
    return true;
  }

  var api = {
    maybeRedirect: maybeRedirect,
    isTesla: isTesla,
    alreadyFullscreen: alreadyFullscreen
  };
  global.SimilarizeTeslaFS = api;

  maybeRedirect();
})(typeof window !== "undefined" ? window : this);
