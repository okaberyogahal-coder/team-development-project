import {headers} from 'next/headers';
import {notFound,redirect} from 'next/navigation';
import {isLocalDemoUrl} from '../../../server/local-demo/http';
import {getLocalDemoRepository} from '../../../server/local-demo/repository';
import {resolveCurrentLocalSession,getSessionToken} from '../../../server/local-demo/session';
import {FriendsClient} from '../FriendsClient';
import '../friends.css';

export const dynamic='force-dynamic';

export default async function LocalDemoFriendsPage(){
  const requestHeaders=await headers();
  const host=requestHeaders.get('host');
  if(process.env.NODE_ENV!=='development'||process.env.MILIFE_AI_NAVI_DEMO!=='local'||!host||!isLocalDemoUrl(`http://${host}`))notFound();

  const userId=resolveCurrentLocalSession(
    getSessionToken(new Request(`http://${host}`,{headers:{cookie:requestHeaders.get('cookie')??''}})),
  );
  if(!userId)redirect('/login');

  const repository=getLocalDemoRepository();
  const [user,initialData]=await Promise.all([
    repository.getUser(userId),
    repository.readFriendPage(userId),
  ]);
  return <FriendsClient nickname={user.nickname} initialData={initialData} apiPath="/api/local-demo/friends" demoMode/>;
}
