// ffxweb3: Xbox Edge / ?lowmem=1 (games/shared/xbox-compat.js SimXbox.lowmem) -> the game's low-memory lite path (Core/XboxLite.cs)
mergeInto(LibraryManager.library, {
  FFXboxLowMem: function () {
    try { return (typeof window !== "undefined" && window.SimXbox && window.SimXbox.lowmem) ? 1 : 0; } catch (e) { return 0; }
  }
});
