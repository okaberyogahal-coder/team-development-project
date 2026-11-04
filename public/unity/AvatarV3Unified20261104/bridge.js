(function () {
  'use strict';
  var ready = false, latest = null, catalog = null, lastSent = '';
  function send() {
    if (!ready || !window.unityInstance || !latest) return;
    var json = JSON.stringify(latest);
    if (json === lastSent) return;
    window.unityInstance.SendMessage('MiraifuFaceSeedWeb', 'ApplyJson', json);
    lastSent = json;
  }
  function accept(value) {
    if (!value || typeof value !== 'object' || Array.isArray(value)) return;
    latest = value;
    send();
  }
  function sendCatalog() {
    if (catalog) window.parent.postMessage({ type: 'miraifu:avatar-catalog', catalog: catalog }, window.location.origin);
  }
  window.faceSeedCatalog = function (json) { catalog = JSON.parse(json); window.miraifuFaceSeedCatalog = catalog; sendCatalog(); };
  window.faceSeedReady = function () { ready = true; send(); window.parent.postMessage({ type: 'miraifu:avatar-ready' }, window.location.origin); };
  window.faceSeedState = function (json) { window.miraifuFaceSeedState = JSON.parse(json); };
  window.addEventListener('message', function (event) {
    if (event.origin !== window.location.origin || event.source !== window.parent) return;
    if (event.data && event.data.type === 'miraifu:avatar') accept(event.data.choice);
    if (event.data && event.data.type === 'miraifu:catalog-request') sendCatalog();
  });
  try { accept(JSON.parse(new URLSearchParams(window.location.search).get('avatar') || localStorage.getItem('miraifu_avatar') || 'null')); } catch (_) {}
  setInterval(send, 200);
}());
