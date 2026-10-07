import {networkInterfaces} from 'node:os';
import {spawn} from 'node:child_process';
import nextEnv from '@next/env';
import {lanConfig} from './lan-config.mjs';
nextEnv.loadEnvConfig(process.cwd());
const config=lanConfig(networkInterfaces(),Number(process.env.PLAZA_PAGE_PORT || 3002),Number(process.env.PLAZA_PORT || 3003));
const existing=(process.env.PLAZA_ALLOWED_ORIGINS || '').split(',').map(s=>s.trim()).filter(Boolean);
const env={...process.env,PLAZA_PORT:String(config.plazaPort),PLAZA_ALLOWED_ORIGINS:Array.from(new Set([...config.origins,...existing])).join(','),NEXT_PUBLIC_PLAZA_WS_URL:''};
console.log('\nOther PCs on the same Wi-Fi/LAN can join:');
for(const url of config.urls)console.log(`  ${url}`);
if(!config.urls.length)console.log('  No LAN IPv4 address found. Connect to Wi-Fi/LAN first.');
console.log(`This PC: http://localhost:${config.pagePort}/plaza\n`);
const children=[];let closing=false;
function stop(code=0){
 if(closing)return;closing=true;
 for(const child of children)if(child.pid){try{if(process.platform==='win32')child.kill('SIGTERM');else process.kill(-child.pid,'SIGTERM');}catch{}}
 process.exitCode=code;
}
for(const args of [['run','dev','--','--hostname','0.0.0.0','--port',String(config.pagePort)],['run','dev:plaza']]){
 const child=spawn(process.platform==='win32'?'npm.cmd':'npm',args,{stdio:'inherit',env,detached:process.platform!=='win32'});children.push(child);
 child.on('error',()=>{console.error('LAN server could not start. Check npm and port availability.');stop(1);});
 child.on('exit',code=>{if(!closing)stop(code || 0);});
}
process.on('SIGINT',()=>stop());process.on('SIGTERM',()=>stop());
