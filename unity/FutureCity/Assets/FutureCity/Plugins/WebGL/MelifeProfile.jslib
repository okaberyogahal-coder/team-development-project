mergeInto(LibraryManager.library, {
  MelifeAvatarEvent: function(json) {
    window.dispatchEvent(new CustomEvent('melife-unity-avatar', {detail: JSON.parse(UTF8ToString(json))}));
  }
});
