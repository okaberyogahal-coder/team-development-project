(function () {
  'use strict';

  var status = document.getElementById('session-status');
  var avatar = document.getElementById('session-avatar');
  var account = document.getElementById('session-account');
  var logout = document.getElementById('session-logout');
  var retry = document.getElementById('session-retry');
  var error = document.getElementById('session-error');
  var avatars = ['sprout', 'cat', 'bear', 'rabbit', 'penguin', 'fox'];
  var instance = null;
  var bridgeReady = false;
  var latest = null;
  var revision = 0;
  var sentRevision = -1;
  var requestGeneration = 0;
  var pending = null;
  var logoutBusy = false;
  var channel = null;

  document.body.dataset.sessionState = 'loading';

  function showError(message) {
    error.textContent = message;
    error.hidden = false;
  }

  function sendLatest() {
    if (!instance || !bridgeReady || !latest || sentRevision === latest.revision) return;
    instance.SendMessage('MelifeSessionBridge', 'ApplySession', JSON.stringify(latest));
    sentRevision = latest.revision;
  }

  function apply(session) {
    latest = Object.assign({}, session, { revision: ++revision });
    sendLatest();
  }

  function validTags(value) {
    return Array.isArray(value) && value.length <= 10 && value.every(function (item) {
      return typeof item === 'string' && item.length <= 30;
    });
  }

  function validSession(value) {
    if (!value || typeof value.authenticated !== 'boolean') return false;
    if (!value.authenticated) return value.profile === null;
    var profile = value.profile;
    return profile && typeof profile.userId === 'string' && /^[0-9a-f-]{36}$/i.test(profile.userId)
      && typeof profile.nickname === 'string' && profile.nickname.length > 0 && profile.nickname.length <= 20
      && avatars.indexOf(profile.avatarId) !== -1 && validTags(profile.teaches) && validTags(profile.wantsToLearn);
  }

  async function refresh() {
    if (logoutBusy) return;
    var generation = ++requestGeneration;
    if (pending) pending.abort();
    var controller = new AbortController();
    pending = controller;
    var timeout = setTimeout(function () { controller.abort(); }, 5000);
    try {
      var response = await fetch('/api/unity/session', { credentials: 'same-origin', cache: 'no-store', signal: controller.signal });
      if (!response.ok) throw new Error('session unavailable');
      var data = await response.json();
      if (generation !== requestGeneration) return;
      if (!validSession(data)) throw new Error('invalid session response');
      error.hidden = true;
      apply({ authenticated: data.authenticated, profile: data.profile, status: data.authenticated ? 'authenticated' : 'guest' });
    } catch (failure) {
      if (generation !== requestGeneration) return;
      // Clear the previous person's data even when verification cannot complete.
      apply({ authenticated: false, profile: null, status: 'unavailable' });
      showError('ログイン状態を確認できません。ゲスト表示に切り替えました。');
    } finally {
      clearTimeout(timeout);
      if (pending === controller) pending = null;
    }
  }

  window.melifeBridgeReady = function () {
    bridgeReady = true;
    sendLatest();
  };

  window.melifeUnityReady = function (unity) {
    instance = unity;
    sendLatest();
  };

  window.melifeSessionApplied = function (json) {
    var applied = JSON.parse(json);
    if (!latest || applied.revision !== latest.revision) return;
    // This snapshot is a display receipt from C#, never authority for any server API.
    window.melifeAppliedSession = Object.freeze(applied);
    document.body.dataset.sessionState = applied.state;
    status.textContent = applied.authenticated ? applied.nickname + 'さんで参加中' : 'ゲストで見学中';
    avatar.hidden = !applied.authenticated;
    if (applied.authenticated && avatars.indexOf(applied.avatarId) !== -1) {
      avatar.src = '/images/icons/' + applied.avatarId + '.svg';
      avatar.alt = applied.avatarLabel;
    } else {
      avatar.removeAttribute('src');
      avatar.alt = '';
    }
    account.href = applied.authenticated ? '/account' : '/login';
    account.textContent = applied.authenticated ? 'マイページ' : 'ログインする';
    logout.hidden = !applied.authenticated;
    logout.disabled = logoutBusy;
    retry.hidden = applied.state !== 'unavailable';
  };

  logout.addEventListener('click', async function () {
    if (logoutBusy) return;
    logoutBusy = true;
    logout.disabled = true;
    error.hidden = true;
    ++requestGeneration;
    if (pending) pending.abort();
    var controller = new AbortController();
    var timeout = setTimeout(function () { controller.abort(); }, 5000);
    try {
      var response = await fetch('/api/auth/sign-out', {
        method: 'POST', credentials: 'same-origin', signal: controller.signal,
        headers: { 'Content-Type': 'application/json' }, body: '{}'
      });
      if (!response.ok) throw new Error('sign out unavailable');
      apply({ authenticated: false, profile: null, status: 'guest' });
      if (channel) channel.postMessage('changed');
      logoutBusy = false;
      await refresh();
    } catch (failure) {
      logoutBusy = false;
      await refresh();
      if (!latest || latest.status !== 'guest') showError('ログアウトできませんでした。もう一度お試しください。');
    } finally {
      clearTimeout(timeout);
      logoutBusy = false;
      logout.disabled = false;
    }
  });

  retry.addEventListener('click', refresh);
  window.addEventListener('focus', refresh);
  window.addEventListener('pageshow', function (event) { if (event.persisted) refresh(); });
  document.addEventListener('visibilitychange', function () { if (!document.hidden) refresh(); });
  try {
    channel = new BroadcastChannel('melife-auth');
    channel.addEventListener('message', function (event) { if (event.data === 'changed') refresh(); });
  } catch (failure) { /* Focus and interval checks remain available. */ }
  var interval = setInterval(function () { if (!document.hidden) refresh(); }, 15000);
  window.addEventListener('pagehide', function (event) {
    if (event.persisted) return;
    ++requestGeneration;
    if (pending) pending.abort();
    clearInterval(interval);
    if (channel) channel.close();
  });
  refresh();
}());
