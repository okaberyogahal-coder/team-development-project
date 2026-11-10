import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import vm from 'node:vm';

test('read-only profile preview never starts world authentication or top-level navigation',()=>{
 const window={location:{search:'?experience=profile'},melifeUnityReady:undefined as unknown};
 const document={getElementById(){throw new Error('World session controls must not be initialized for a profile preview');}};
 const script=readFileSync('unity/FutureCity/Assets/WebGLTemplates/Melife/session.js','utf8');
 assert.doesNotThrow(()=>vm.runInNewContext(script,{window,document,URLSearchParams}));
 assert.equal(typeof window.melifeUnityReady,'function');
});
