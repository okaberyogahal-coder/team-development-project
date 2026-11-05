import type {Metadata} from 'next';
import {redirect} from 'next/navigation';
import {getCurrentUser} from '../../server/auth/server';
import {AccountOverview} from '../components/AccountOverview';

export const metadata:Metadata = {title:'マイページ | ミライフ ME LIFE',robots:{index:false,follow:false}};

export default async function AccountPage({searchParams}:{searchParams:Promise<{updated?:string}>}) {
 const {updated} = await searchParams;
 const user = await getCurrentUser();
 if (!user) redirect('/login');
 return <AccountOverview user={user} updated={updated==='1'}/>;
}
