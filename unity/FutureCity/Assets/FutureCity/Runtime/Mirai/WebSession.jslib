mergeInto(LibraryManager.library, {
  MelifeWorldInteraction: function (json) {
    window.melifeWorldInteraction = Object.freeze(JSON.parse(UTF8ToString(json)));
  },
  MelifeBridgeReady: function () {
    if (typeof window.melifeBridgeReady === 'function') window.melifeBridgeReady();
  },
  MelifeSessionApplied: function (json) {
    if (typeof window.melifeSessionApplied === 'function') window.melifeSessionApplied(UTF8ToString(json));
  },
  MelifeOpenAccount: function (authenticated) {
    var path = authenticated ? '/account' : '/login';
    try { window.top.location.assign(path); }
    catch (error) { window.location.assign(path); }
  }
});
