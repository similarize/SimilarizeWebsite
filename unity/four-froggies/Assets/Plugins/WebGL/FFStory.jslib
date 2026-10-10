// Four Froggies ffu22 story mode: the checkpoint save lives in window.localStorage (key passed from C#). Every call is
// wrapped in try/catch: private browsing, blocked storage or a full quota just means "no save" (the game carries on).
mergeInto(LibraryManager.library, {
  FFStorySave: function (keyPtr, valPtr) {
    try { window.localStorage.setItem(UTF8ToString(keyPtr), UTF8ToString(valPtr)); return 1; }
    catch (e) { console.warn('FFStory: save failed ' + e); return 0; }
  },
  FFStoryLoad: function (keyPtr) {
    var v = '';
    try { v = window.localStorage.getItem(UTF8ToString(keyPtr)) || ''; } catch (e) { console.warn('FFStory: load failed ' + e); v = ''; }
    var n = lengthBytesUTF8(v) + 1, p = _malloc(n);
    stringToUTF8(v, p, n);
    return p;
  },
  FFStoryClear: function (keyPtr) {
    try { window.localStorage.removeItem(UTF8ToString(keyPtr)); } catch (e) {}
  }
});
