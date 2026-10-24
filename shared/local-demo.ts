import type {Material,PopularitySummary} from '../server/ai-navi/types';

export const LOCAL_USER_IDS=['photography','food-photo','newcomer'] as const;
export type LocalUserId=typeof LOCAL_USER_IDS[number];
export function isLocalUserId(value:unknown):value is LocalUserId {
  return typeof value==='string' && (LOCAL_USER_IDS as readonly string[]).includes(value);
}
export interface LocalEvent {id:string;materialId:string;kind:'view'|'complete';at:string}
export interface LocalUser {schemaVersion:1;id:LocalUserId;nickname:string;description:string;events:LocalEvent[]}
export interface LocalDemoState {
  users:Pick<LocalUser,'id'|'nickname'|'description'>[];
  currentUser:LocalUser|null;
  materials:Material[];
  popularity:PopularitySummary;
  revision:string;
}
