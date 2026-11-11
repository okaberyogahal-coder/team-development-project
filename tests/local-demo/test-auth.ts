import type {LocalUserId} from '../../shared/local-demo';
import {getLocalSessionStore,LOCAL_SESSION_COOKIE} from '../../server/local-demo/session';
export function testCookie(userId:LocalUserId):string{return `${LOCAL_SESSION_COOKIE}=${getLocalSessionStore().issue(userId,Date.now())}`;}
