import assert from 'node:assert/strict';
import {readFileSync,existsSync} from 'node:fs';
import {test} from 'node:test';
test('V3 dependency manifest pins six complete models and keeps city source untouched',()=>{
 const manifest=JSON.parse(readFileSync('docs/avatar-v3-import.json','utf8'));
 assert.equal(manifest.commit,'d71d307e8a7f22446e3429516d3719e0d42c8d31');
 assert.deepEqual([...manifest.modelIds].sort(),['elderly-man','elderly-woman','middle-aged-man','middle-aged-woman','young-man','young-woman']);
 assert.ok(manifest.files.length>100);
 for(const file of manifest.files)assert.ok(existsSync(file.target),file.target);
 assert.ok(!manifest.files.some((file:{target:string})=>file.target==='unity/FutureCity/Assets/FutureCity/Scenes/FutureCity.unity'));
 assert.ok(existsSync('unity/FutureCity/Assets/MelifeAvatarV3/Runtime/Melife.AvatarV3.asmdef'));
});
