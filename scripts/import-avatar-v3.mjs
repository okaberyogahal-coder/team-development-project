import {execFileSync} from 'node:child_process';
import {existsSync,readFileSync,writeFileSync,mkdirSync,readdirSync} from 'node:fs';
import path from 'node:path';
import {createHash,randomUUID} from 'node:crypto';
const commit='d71d307e8a7f22446e3429516d3719e0d42c8d31';
const root='unity/FutureCity/';
const manifestPath='docs/avatar-v3-import.json';
const writing=process.argv.includes('--write');
const sha=bytes=>createHash('sha256').update(bytes).digest('hex');
if(!writing){
 const manifest=JSON.parse(readFileSync(manifestPath,'utf8'));
 for(const file of manifest.files){
  if(!existsSync(file.target))throw new Error('Missing imported file: '+file.target);
  if(!/\.(cs|tsx?|css|mjs)$/.test(file.target)&&sha(readFileSync(file.target))!==file.sha256)throw new Error('Changed imported asset: '+file.target);
 }
 console.log(`V3 import verified: ${manifest.files.length} files, ${manifest.modelIds.length} models (${manifest.commit.slice(0,7)}).`);
 process.exit(0);
}
const tree=new Map(execFileSync('git',['ls-tree','-r',commit],{encoding:'utf8',maxBuffer:64*1024*1024}).trim().split('\n').map(line=>{const [info,name]=line.split('\t');return [name,info.split(' ')[2]];}));
const blobs=new Map();
function batch(hashes){
 const unique=[...new Set(hashes)].filter(h=>!blobs.has(h));if(!unique.length)return;
 const data=execFileSync('git',['cat-file','--batch'],{input:unique.join('\n')+'\n',maxBuffer:256*1024*1024});let at=0;
 for(const hash of unique){const end=data.indexOf(10,at);const size=Number(data.subarray(at,end).toString().split(' ')[2]);blobs.set(hash,data.subarray(end+1,end+1+size));at=end+size+2;}
}
function source(name){const h=tree.get(name);if(!h)throw new Error('Missing source '+name);batch([h]);return blobs.get(h);}
batch([...tree].filter(([p])=>p.startsWith(root+'Assets/')&&p.endsWith('.meta')).map(([,h])=>h));
const guids=new Map();for(const [p,h] of tree){if(p.startsWith(root+'Assets/')&&p.endsWith('.meta')){const id=blobs.get(h).toString().match(/^guid: ([a-f0-9]{32})/m)?.[1];if(id)guids.set(id,p.slice(0,-5));}}
const localGuids=new Map();
function scan(dir){if(!existsSync(dir))return;for(const item of readdirSync(dir,{withFileTypes:true})){const p=path.join(dir,item.name);if(item.isDirectory())scan(p);else if(item.name.endsWith('.meta')){const g=readFileSync(p,'utf8').match(/^guid: ([a-f0-9]{32})/m)?.[1];if(g)localGuids.set(g,p.slice(0,-5));}}}
scan(root+'Assets');scan(root+'Library/PackageCache');
const core=['MiraifuRuntimeAvatar','MiraifuRuntimeAvatarModel','MiraifuRuntimeAvatarLab','MiraifuWebAvatarReceiver','MiraifuAppearanceRuntime','MiraifuAppearanceSet','MiraifuFaceSeedWebBridge'];
function target(p){const bare=p.replace(/\.meta$/,'');const name=path.basename(bare,'.cs');return bare.endsWith('.cs')&&core.includes(name)?root+'Assets/MelifeAvatarV3/Runtime/'+name+'.cs'+(p.endsWith('.meta')?'.meta':''):p;}
const tracked=new Set(execFileSync('git',['ls-files'],{encoding:'utf8'}).trim().split('\n'));
const records=new Map(),seen=new Set(),packageGuids=new Set();
function put(p){if(seen.has(p))return;seen.add(p);
 if(!tree.has(p)&&tree.has(p+'.meta')){mkdirSync(target(p),{recursive:true});put(p+'.meta');return;}
 const output=target(p),data=source(p);
 if(p.endsWith('.cs')&&!core.includes(path.basename(p,'.cs')))throw new Error('Unexpected script dependency: '+p);
 if(existsSync(output)&&!records.has(output)){
  const local=readFileSync(output);if(!local.equals(data)){
   const sameMeta=p.endsWith('.meta')&&local.toString().match(/^guid: (.+)$/m)?.[1]===data.toString().match(/^guid: (.+)$/m)?.[1];
   const sameFolder=p.endsWith('.meta')&&local.toString().includes('folderAsset: yes')&&data.toString().includes('folderAsset: yes');
   if(!sameMeta&&!sameFolder)throw new Error('Refusing to overwrite existing file: '+output);
  }
 }else{mkdirSync(path.dirname(output),{recursive:true});writeFileSync(output,data);}
 records.set(output,{source:p,target:output,sha256:sha(readFileSync(output))});
 if(tree.has(p+'.meta'))put(p+'.meta');
 if(!(p.includes('/ithappy/')&&tracked.has(p))&&/\.(meta|unity|prefab|asset|mat|controller|anim)$/.test(p))for(const match of data.toString().matchAll(/guid: ([a-f0-9]{32})/g)){
  const g=match[1];if(g.startsWith('0000000000000000'))continue;
  if(guids.has(g))put(guids.get(g));else if(localGuids.has(g))packageGuids.add(g);else throw new Error('Unresolved GUID '+g+' in '+p);
 }
 let dir=path.dirname(p);while(dir.startsWith(root+'Assets/')&&dir!==root+'Assets'){if(tree.has(dir+'.meta'))put(dir+'.meta');dir=path.dirname(dir);}
}
put(root+'Assets/MiraifuAvatarRuntimePrototype/Generated/v3-web/AvatarV3WebPreview.unity');
for(const name of core){const p=[...tree.keys()].find(p=>p.endsWith('/'+name+'.cs'));if(!p)throw new Error(name);put(p);}
put(root+'Assets/MiraifuFaceSeedWeb/Plugins/FaceSeed.jslib');
for(const [p] of tree){if(p.startsWith('shared/avatar-')&&/\.(ts|json)$/.test(p))put(p);if(['public/images/avatar-parts/','public/images/avatar-v3-unified-20261104/','public/images/avatars/'].some(prefix=>p.startsWith(prefix)))put(p);}
for(const dir of [root+'Assets/MelifeAvatarV3',root+'Assets/MelifeAvatarV3/Runtime']){mkdirSync(dir,{recursive:true});if(!existsSync(dir+'.meta'))writeFileSync(dir+'.meta',`fileFormatVersion: 2\nguid: ${randomUUID().replaceAll('-','')}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`);}
writeFileSync(root+'Assets/MelifeAvatarV3/Runtime/Melife.AvatarV3.asmdef',JSON.stringify({name:'Melife.AvatarV3',references:['Unity.InputSystem'],autoReferenced:true},null,2)+'\n');
mkdirSync('docs',{recursive:true});writeFileSync(manifestPath,JSON.stringify({commit,modelIds:['young-man','young-woman','middle-aged-man','middle-aged-woman','elderly-man','elderly-woman'],packageGuids:[...packageGuids].sort(),files:[...records.values()].sort((a,b)=>a.target.localeCompare(b.target))},null,2)+'\n');
console.log(`Imported ${records.size} V3 dependency files without replacing the current world.`);
