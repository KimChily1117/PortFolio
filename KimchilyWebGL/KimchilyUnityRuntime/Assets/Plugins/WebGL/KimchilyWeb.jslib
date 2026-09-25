mergeInto(LibraryManager.library, {
  KimchilyWeb_OnEvent: function (jsonPointer) {
    var json = UTF8ToString(jsonPointer);
    if (typeof window !== 'undefined' && typeof window.KimchilyWebReceive === 'function') {
      window.KimchilyWebReceive(json);
    }
  }
});
