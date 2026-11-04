import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const source = readFileSync('unity/FutureCity/Assets/WebGLTemplates/Melife/avatar.js', 'utf8');
function bridge(search = '', saved: string | null = null) {
  const calls: string[][] = [];
  const listeners: Record<string, (event: unknown) => void> = {};
  const storage = new Map(saved ? [['miraifu_avatar', saved]] : []);
  const parent = { postMessage() {} };
  const window = {
    parent, location: { search, origin: 'http://localhost' },
    addEventListener(type: string, handler: (event: unknown) => void) { listeners[type] = handler; },
    unityInstance: undefined as unknown,
  };
  vm.runInNewContext(source, { window, URLSearchParams, localStorage: {
    getItem: (key: string) => storage.get(key),
    setItem: (key: string, value: string) => storage.set(key, value),
  } });
  const api = window as typeof window & { miraifuAvatarReady(): void; melifeUnityReady(instance: unknown): void };
  return { calls, storage, api,
    mount() { api.unityInstance = { SendMessage: (...args: string[]) => calls.push(args) }; api.melifeUnityReady(api.unityInstance); },
    change(choice: unknown, origin = window.location.origin, sender = parent) {
      listeners.message({ origin, source: sender, data: { type: 'miraifu:avatar', choice } });
    },
  };
}

for (const topColor of ['#E11D48', '#2563EB', '#16A34A']) {
  test(`queues and restores ${topColor} without resetting other outfit fields`, () => {
    const choice = { topColor, hair: 1, pants: 1, shoes: 2, hairColor: '#60412E', pantsColor: '#627B99' };
    const preview = bridge();
    preview.change(choice);
    preview.api.miraifuAvatarReady(); // object ready before loader promise resolves
    assert.equal(preview.calls.length, 0);
    preview.mount();
    assert.deepEqual(JSON.parse(preview.calls.at(-1)![2]), choice);
    const city = bridge('', preview.storage.get('miraifu_avatar')!);
    city.mount();
    assert.equal(city.calls.length, 0);
    city.api.miraifuAvatarReady(); // city object ready after loader resolves
    assert.deepEqual(JSON.parse(city.calls.at(-1)![2]), choice);
    city.change({ ...choice, topColor: '#FFFFFF' }, 'http://untrusted');
    assert.equal(city.calls.length, 1);
  });
}

test('explicit city URL wins over saved outfit and live edits apply immediately', () => {
  const choice = { topColor: '#16A34A', shoes: 2 };
  const city = bridge('?avatar=' + encodeURIComponent(JSON.stringify(choice)), JSON.stringify({ topColor: '#2563EB' }));
  city.mount(); city.api.miraifuAvatarReady();
  assert.deepEqual(JSON.parse(city.calls[0][2]), choice);
  city.change({ ...choice, topColor: '#E11D48' });
  assert.equal(JSON.parse(city.calls[1][2]).topColor, '#E11D48');
});
