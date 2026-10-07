import {loadEnvConfig} from '@next/env';
import {networkInterfaces} from 'node:os';
import {lanConfig} from '../../scripts/lan-config.mjs';
import {callsFromEnvironment} from './calls';
import {startPlazaServer} from './service';
loadEnvConfig(process.cwd());
async function main(){
 const port=Number(process.env.PLAZA_PORT || 3003);
 const config=lanConfig(networkInterfaces(),Number(process.env.PLAZA_PAGE_PORT || 3002),port);
 const origins=(process.env.PLAZA_ALLOWED_ORIGINS || config.origins.join(',')).split(',').map(s=>s.trim()).filter(Boolean);
 const calls=callsFromEnvironment();
 const server=await startPlazaServer({port,origins,calls});
 console.log(`Plaza server ready on port ${port}; video calls ${calls.configured?'configured':'not configured'}`);
 let closing=false;
 const shutdown=()=>{if(closing)return;closing=true;void server.close().finally(()=>process.exit(0));};
 process.on('SIGINT',shutdown);process.on('SIGTERM',shutdown);
}
main().catch(()=>{console.error('Plaza server could not start. Check port, allowed origins and LiveKit Cloud settings.');process.exitCode=1;});
