import {test} from 'node:test';
import assert from 'node:assert/strict';
import {avatarSaveInput} from '../../shared/account-avatar-save';
import {DEFAULT_AVATAR_CHOICE} from '../../shared/avatar-types';
import {DEFAULT_AVATAR} from '../../shared/demo-profile';
import {createAvatarHandlers} from '../../server/account/avatar-handler';
import {ProfileConflictError} from '../../server/account/profile-handler';

const appearance = {version: 3 as const, assetProfile: 'v3-unified-20261104' as const, choice: {...DEFAULT_AVATAR_CHOICE, baseAvatarId: 'elderly-woman'}};
const input = {appearance, version: 0};
const request = (body: unknown = input, owner = 'owner', origin = 'http://localhost:3002') => new Request('http://localhost:3002/api/account/avatar', {
  method: 'PUT', headers: {'Content-Type': 'application/json', Origin: origin, 'X-Profile-Owner': owner}, body: JSON.stringify(body),
});

test('account saves accept V3 and legacy outfits while rejecting invalid parts and identity fields', () => {
  assert.deepEqual(avatarSaveInput(input), input);
  assert.deepEqual(avatarSaveInput({appearance: DEFAULT_AVATAR, version: 1}).appearance, DEFAULT_AVATAR);
  for (const value of [{...input, userId: 'other'}, {...input, version: -1}, {...input, version: 1.5}, {...input, appearance: {...appearance, choice: {...appearance.choice, hair: 100}}}, {...input, appearance: {...appearance, choice: {...appearance.choice, topColor: 'red'}}}]) {
    assert.throws(() => avatarSaveInput(value));
  }
});

test('account avatar writes use the current session owner and return private responses', async () => {
  let saved: unknown;
  const handlers = createAvatarHandlers({getUser: async () => ({id:'owner'}), read: async id => ({ownerId:id}), save: async (id, value, version) => { saved = {id, value, version}; return {ownerId:id}; }, allowSave: async () => true});
  const response = await handlers.PUT(request());
  assert.equal(response.status, 200);
  assert.equal(response.headers.get('cache-control'), 'no-store');
  assert.deepEqual(saved, {id:'owner',value:appearance,version:0});
});

test('unsigned, changed-owner and cross-origin requests cannot save, and stale versions are reported', async () => {
  let writes=0;
  const deps = {getUser: async ():Promise<{id:string}|null> => ({id:'owner'}), read: async () => null, save: async () => {writes++; return null;}, allowSave: async () => true};
  assert.equal((await createAvatarHandlers({...deps,getUser:async()=>null}).PUT(request())).status,401);
  assert.equal((await createAvatarHandlers(deps).PUT(request(input,'other'))).status,409);
  assert.equal((await createAvatarHandlers(deps).PUT(request(input,'owner','https://untrusted.invalid'))).status,403);
  assert.equal(writes,0);
  assert.equal((await createAvatarHandlers({...deps,save:async()=>{throw new ProfileConflictError();}}).PUT(request())).status,409);
});
