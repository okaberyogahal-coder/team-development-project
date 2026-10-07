import type {CallCredentials,RoomId} from '../../../shared/plaza/types';
export interface CallPort {connect(c:CallCredentials):Promise<void>;close():Promise<void>;setMic(enabled:boolean):Promise<void>;setCamera(enabled:boolean):Promise<void>;}
export type CallSnapshot={status:'idle'|'connecting'|'connected'|'error';roomId:RoomId|null;micEnabled:boolean;cameraEnabled:boolean;error:string|null;busy:boolean};
const empty=():CallSnapshot=>({status:'idle',roomId:null,micEnabled:false,cameraEnabled:false,error:null,busy:false});
export class CallSession {
 snapshot=empty();private epoch=0;private port:CallPort|null=null;
 constructor(private changed:(s:CallSnapshot)=>void){}
 private update(s:Partial<CallSnapshot>){this.snapshot={...this.snapshot,...s};this.changed(this.snapshot);}
 async connect(c:CallCredentials,p:CallPort){
  const epoch=++this.epoch;const old=this.port;this.port=p;this.update({...empty(),status:'connecting',roomId:c.roomId});
  if(old)await old.close().catch(()=>{});
  if(epoch!==this.epoch){await p.close().catch(()=>{});return;}
  try{await p.connect(c);if(epoch!==this.epoch){await p.close().catch(()=>{});return;}this.update({status:'connected'});}
  catch{await p.close().catch(()=>{});if(epoch===this.epoch)this.update({status:'error',error:'通話につながりませんでした。再接続をお試しください。'});}
 }
 async disconnect(){++this.epoch;const p=this.port;this.port=null;this.update(empty());await p?.close().catch(()=>{});}
 async toggleMic(){await this.toggle('micEnabled');}
 async toggleCamera(){await this.toggle('cameraEnabled');}
 private async toggle(kind:'micEnabled'|'cameraEnabled'){
  const p=this.port,epoch=this.epoch;if(!p || this.snapshot.status!=='connected' || this.snapshot.busy)return;
  const enabled=!this.snapshot[kind];this.update({busy:true,error:null});
  try{await (kind==='micEnabled'?p.setMic(enabled):p.setCamera(enabled));if(epoch===this.epoch)this.update({[kind]:enabled});else await p.close().catch(()=>{});}
  catch{if(epoch===this.epoch)this.update({error:'機器を利用できませんでした。ブラウザの許可と接続を確認してください。'});}
  finally{if(epoch===this.epoch)this.update({busy:false});}
 }
 lost(){++this.epoch;const p=this.port;this.port=null;this.update({...empty(),status:'error',error:'通話が切断されました。再接続をお試しください。'});void p?.close().catch(()=>{});}
 setDevices(micEnabled:boolean,cameraEnabled:boolean){if(this.snapshot.status==='connected')this.update({micEnabled,cameraEnabled});}
}

export function canRetryCall(configured:boolean,inLobby:boolean,status:CallSnapshot['status'],hasCredentials:boolean){return configured && !inLobby && (status==='error' || (!hasCredentials && status==='idle'));}
