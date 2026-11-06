import {redirect} from 'next/navigation';
import Link from 'next/link';
import {ageBadge} from '../../shared/profile-demographics';
import type { Metadata } from 'next';
import {AvatarCustomizer} from './AvatarCustomizer';
import {V3_UNIFIED_ASSETS} from '../../shared/avatar-development-assets';
import { Logo } from '../components/Logo';
import '../plaza/unity.css';
import './avatar.css';
import { getCurrentUser } from '../../server/auth/server';
export const metadata: Metadata = {title:'アバター作成 | ミライフ'};
export default async function AvatarPage() {
 const user = await getCurrentUser();
 if(!user) redirect('/login');
 if(user&&(!user.profile?.gender||!ageBadge(user.profile?.age))) redirect('/account/edit');
 return <main className="avatar-page avatar-v3-page">
  <header className="avatar-header"><div className="avatar-header-left"><Logo priority/><h1 className="avatar-header-title">アバター編集</h1></div><div className="avatar-header-actions"><span>{user.profile?.nickname || '利用者'}さんでログイン中</span><Link href="/account" className="avatar-btn-secondary">マイページ</Link><Link href="/" className="avatar-btn-secondary">TOPへ戻る</Link></div></header>
  <AvatarCustomizer key={user.id} ownerId={user.id} nickname={user.profile?.nickname || "利用者"} assets={V3_UNIFIED_ASSETS} allCandidatesMode />
 </main>;
}
