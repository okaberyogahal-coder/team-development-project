import {readDemoProfile} from './demo-profile';
/** The verified session selects an owner; browser data supplies only that owner's appearance. */
export function resolveWorldAvatar(storage:Pick<Storage,'getItem'>,ownerId:string|null){
 const saved=readDemoProfile(storage);
 return ownerId&&saved?.id===ownerId&&saved.appearance?saved:null;
}
