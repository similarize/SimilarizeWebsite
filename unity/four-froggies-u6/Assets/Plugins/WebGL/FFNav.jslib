// ffarc66: arcade signs in the ranch open another similarize.com game in the same tab
mergeInto(LibraryManager.library, {
  FFNavGo: function (urlPtr) {
    try { window.location.href = UTF8ToString(urlPtr); } catch (e) { console.warn('FFNav: ' + e); }
  }
});
