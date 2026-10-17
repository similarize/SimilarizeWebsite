/*! Similarize end-screen CTA (cta1): a small, kid-friendly pointer to the rewording tool.
 * SimilarizeCTA.attach(parentEl)  — idempotent; appends one card to parentEl. */
(function (g) {
  "use strict";
  function attach(parent) {
    if (!parent || parent.querySelector(".sim-cta")) return;
    if (!document.getElementById("sim-cta-css")) {
      var s = document.createElement("style"); s.id = "sim-cta-css";
      s.textContent = ".sim-cta{display:flex;align-items:center;gap:10px;margin:14px auto 0;max-width:26rem;padding:10px 14px;border-radius:14px;background:rgba(255,255,255,.94);color:#1b1b1b;text-decoration:none;font:500 14px/1.3 Outfit,system-ui,sans-serif;box-shadow:0 4px 16px rgba(0,0,0,.25);text-align:left}" +
        ".sim-cta b{color:#900000}.sim-cta i{font-style:normal;color:#008bff;font-weight:700;white-space:nowrap}.sim-cta .m{flex:none;width:34px;height:34px;border-radius:9px;background:#900000;color:#fff;display:grid;place-items:center;font:700 15px Outfit,system-ui,sans-serif}";
      document.head.appendChild(s);
    }
    var a = document.createElement("a");
    a.className = "sim-cta"; a.href = "/#statement"; a.target = "_top";
    a.innerHTML = '<span class="m">imi</span><span><b>Good game!</b> Want to say something kind to the other players? Try the Similarize rewording tool <i>\u2192</i></span>';
    parent.appendChild(a);
  }
  g.SimilarizeCTA = { attach: attach };
})(window);
