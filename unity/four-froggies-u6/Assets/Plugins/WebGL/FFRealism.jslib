// ?realism=1 test: the browser's user agent, so Realism.cs can put the Tesla browser on the Lite tier.
mergeInto(LibraryManager.library, {
  FFRealUA: function () {
    var s = (typeof navigator !== "undefined" && navigator.userAgent) ? navigator.userAgent : "";
    var n = lengthBytesUTF8(s) + 1;
    var b = _malloc(n);
    stringToUTF8(s, b, n);
    return b;
  }
});
