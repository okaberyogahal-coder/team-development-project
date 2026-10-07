export type RoomId = 'lobby' | 'cafe' | 'hobby' | 'learning' | 'sharing';
export type AvatarId = 'coral' | 'sky' | 'mint' | 'violet' | 'sun';
export type Position = {x:number; y:number};
export type Direction = Position;
export type Participant = Position & {id:string;name:string;avatar:AvatarId;roomId:RoomId;facing:number;connected:boolean};
export type RoomSummary = {id:RoomId;count:number;capacity:number};
export type CallCredentials = {url:string;token:string;identity:string;roomId:RoomId;generation:number};
export type ClientMessage =
 | {type:'join';name:string;avatar:AvatarId;resumeToken?:string}
 | {type:'move';x:number;y:number;seq:number}
 | {type:'changeRoom';roomId:RoomId;requestId:string}
 | {type:'retryCall'}
 | {type:'leave'};
export type ServerMessage =
 | {type:'welcome';self:Participant;resumeToken:string;generation:number;participants:Participant[];rooms:RoomSummary[];total:number;callConfigured:boolean}
 | {type:'state';participants:Participant[];rooms:RoomSummary[];total:number;generation:number}
 | {type:'roomChanged';self:Participant;generation:number}
 | {type:'callCredentials';credentials:CallCredentials}
 | {type:'error';code:string;message:string};
