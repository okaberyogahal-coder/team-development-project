import {resolveAccountAvatar} from './account-avatar';
import type {AccountAvatar} from './account-avatar-save';

/** Only the verified session selects the avatar owner. */
export function resolveWorldAvatar(storage:Pick<Storage,'getItem'>,ownerId:string|null,nickname?:string,accountAvatar?:AccountAvatar){
 return ownerId ? resolveAccountAvatar(storage,ownerId,nickname,accountAvatar) : null;
}
