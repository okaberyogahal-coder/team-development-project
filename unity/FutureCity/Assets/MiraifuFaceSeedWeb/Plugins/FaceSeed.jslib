mergeInto(LibraryManager.library, {
  MiraifuFaceSeedCatalog: function (json) { if (window.faceSeedCatalog) window.faceSeedCatalog(UTF8ToString(json)); },
  MiraifuFaceSeedReady: function () { if (window.faceSeedReady) window.faceSeedReady(); },
  MiraifuFaceSeedState: function (json) { if (window.faceSeedState) window.faceSeedState(UTF8ToString(json)); }
});
