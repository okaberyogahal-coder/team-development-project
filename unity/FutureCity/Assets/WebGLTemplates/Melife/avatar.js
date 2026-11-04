(function () {
  'use strict';
  var latest = null;
  var ready = false;
  var catalog = null;
  function sendCatalog() {
    if (catalog) window.parent.postMessage({ type: 'miraifu:avatar-catalog', catalog: catalog }, window.location.origin);
  }
  window.miraifuAvatarCatalog = function (json) {
    try { catalog = JSON.parse(json); sendCatalog(); } catch (_) {}
  };

  function accept(value) {
    if (!value || typeof value !== 'object' || Array.isArray(value)) return;
    if (!/^#[0-9a-f]{6}$/i.test(value.topColor)) return;
    latest = value;
    try { localStorage.setItem('miraifu_avatar', JSON.stringify(value)); } catch (_) {}
    send();
  }
  function send() {
    if (!ready || !window.unityInstance || !latest) return;
    window.unityInstance.SendMessage('MiraifuCreativeAvatar', 'ApplyJson', JSON.stringify(latest));
  }
  try {
    var query = new URLSearchParams(window.location.search).get('avatar');
    accept(JSON.parse(query || localStorage.getItem('miraifu_avatar') || 'null'));
  } catch (_) {}
  window.addEventListener('message', function (event) {
    if (event.origin !== window.location.origin || event.source !== window.parent) return;
    if (event.data && event.data.type === 'miraifu:avatar') accept(event.data.choice);
    if (event.data && event.data.type === 'miraifu:catalog-request') sendCatalog();
  });
  // C# announces that the actual avatar object has completed Start, which may
  // happen before createUnityInstance resolves or after the city creates it.
  window.miraifuAvatarReady = function () {
    ready = true;
    send();
    window.parent.postMessage({ type: 'miraifu:avatar-ready' }, window.location.origin);
  };
  var unityReady = window.melifeUnityReady;
  window.melifeUnityReady = function (instance) {
    if (unityReady) unityReady(instance);
    send();
  };
}());
