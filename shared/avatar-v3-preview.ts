import type {AvatarChoice} from './avatar-types';
import {isV3Choice,v3Fingerprint} from './avatar-v3';
export function previewAccepted(value:unknown,revision:number,choice:AvatarChoice):boolean {
 if(!value||typeof value!=='object')return false;
 const message=value as Record<string,unknown>;
 if(message.type!=='miraifu:avatar-state'||message.revision!==revision||!message.state||typeof message.state!=='object')return false;
 const state=message.state as Record<string,unknown>;
 return state.accepted===true&&state.pairMatched===true&&isV3Choice(state.choice)&&v3Fingerprint(state.choice)===v3Fingerprint(choice);
}
