import {spawn} from 'node:child_process';
import {createRequire} from 'node:module';

// Do not forward user-supplied host arguments: this paid, unauthenticated demo is local only.
if(process.argv.length>2){
  console.error('AI未来ナビはローカル専用です。追加の起動引数は指定できません。');
  process.exit(1);
}
const require=createRequire(import.meta.url);
const child=spawn(process.execPath,[require.resolve('next/dist/bin/next'),'dev','--hostname','127.0.0.1','--port','3002'],{
  stdio:'inherit',
  env:{...process.env,MILIFE_AI_NAVI_DEMO:'local',WATCHPACK_POLLING:'true',CHOKIDAR_USEPOLLING:'true'},
});
for(const signal of ['SIGINT','SIGTERM']) process.on(signal,()=>{child.kill(signal);});
child.on('error',()=>{console.error('AI未来ナビの開発サーバーを起動できませんでした。');process.exitCode=1;});
child.on('exit',code=>{process.exitCode=code??0;});
