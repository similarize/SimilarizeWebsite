/*! SimilarizeViewportFS — desktop/browser true viewport fill + Fullscreen API.
 *
 * Tesla YouTube-redirect chrome removal stays in tesla-fullscreen.js (Tesla UA only).
 * This helper is for normal browsers:
 *   1) Injects light base CSS so html/body fill 100dvh with no margins/overflow scroll.
 *   2) Exposes Fullscreen API helpers; call enter() from a user gesture (Start/GO/tap).
 *
 * Drop-in (after tesla-fullscreen.js is fine):
 *   <script src="…/shared/viewport-fill.js?v=vfs1"></script>
 *
 * API (window.SimilarizeViewportFS):
 *   enter(el?)   — requestFullscreen on documentElement (or el)
 *   exit()       — exitFullscreen if active
 *   isActive()   — currently in browser fullscreen
 *   bindGesture(elOrSelector) — enter() on pointerdown/click of that control
 *   ensureCss()  — re-apply base CSS (idempotent)
 */
(function (global) {
  "use strict";

  var STYLE_ID = "similarize-viewport-fill-css";

  function ensureCss() {
    if (typeof document === "undefined") return;
    if (document.getElementById(STYLE_ID)) return;
    var css = document.createElement("style");
    css.id = STYLE_ID;
    /* Keep rules minimal: no touch-action (Maps/Leaflet needs pan). Games set their own. */
    css.textContent = [
      "html,body{margin:0;padding:0;width:100%;height:100%;height:100dvh;max-height:100dvh;",
      "overflow:hidden;box-sizing:border-box}",
      "html{overscroll-behavior:none}",
      "*,*::before,*::after{box-sizing:border-box}"
    ].join("");
    (document.head || document.documentElement).appendChild(css);
  }

  function fsEl() {
    return (
      document.fullscreenElement ||
      document.webkitFullscreenElement ||
      document.msFullscreenElement ||
      null
    );
  }

  function isActive() {
    return !!fsEl();
  }

  function enter(target) {
    if (typeof document === "undefined") return false;
    if (isActive()) return true;
    var node = target || document.documentElement;
    var req =
      node.requestFullscreen ||
      node.webkitRequestFullscreen ||
      node.webkitRequestFullScreen ||
      node.msRequestFullscreen;
    if (!req) return false;
    try {
      var out = req.call(node);
      return out && typeof out.then === "function" ? out.catch(function () {}) : true;
    } catch (e) {
      return false;
    }
  }

  function exit() {
    if (!isActive()) return false;
    var x =
      document.exitFullscreen ||
      document.webkitExitFullscreen ||
      document.webkitCancelFullScreen ||
      document.msExitFullscreen;
    if (!x) return false;
    try {
      var out = x.call(document);
      return out && typeof out.then === "function" ? out.catch(function () {}) : true;
    } catch (e) {
      return false;
    }
  }

  function resolveEl(elOrSelector) {
    if (!elOrSelector) return null;
    if (typeof elOrSelector === "string") {
      try {
        return document.querySelector(elOrSelector);
      } catch (e) {
        return null;
      }
    }
    return elOrSelector;
  }

  function bindGesture(elOrSelector) {
    var node = resolveEl(elOrSelector);
    if (!node || node.__similarizeFsBound) return false;
    node.__similarizeFsBound = true;
    var go = function () {
      enter();
    };
    node.addEventListener("pointerdown", go, { passive: true });
    node.addEventListener("click", go, { passive: true });
    return true;
  }

  var api = {
    ensureCss: ensureCss,
    enter: enter,
    exit: exit,
    isActive: isActive,
    bindGesture: bindGesture
  };
  global.SimilarizeViewportFS = api;

  if (typeof document !== "undefined") {
    if (document.readyState === "loading") {
      document.addEventListener("DOMContentLoaded", ensureCss);
    } else {
      ensureCss();
    }
  }
})(typeof window !== "undefined" ? window : this);
