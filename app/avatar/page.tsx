import type { Metadata } from 'next';
import Link from 'next/link';
import { redirect } from 'next/navigation';
import { ageBadge } from '../../shared/profile-demographics';
import { getCurrentUser } from '../../server/auth/server';
import { getRegisteredAvatarGender, type AvatarGenderState } from '../../shared/avatar-candidates';
import { V3_UNIFIED_ASSETS } from '../../shared/avatar-development-assets';
import { Logo } from '../components/Logo';
import { AvatarCustomizer } from './AvatarCustomizer';
import './avatar.css';

export const metadata: Metadata = {
  title: 'アバター作成・編集 | ミライフ',
  description: '自分らしい3Dアバターに着せ替えて、未来都市ミライフへ参加しましょう。',
};

type Props = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default async function AvatarPage({ searchParams }: Props) {
  const user = await getCurrentUser();
  if (!user) redirect('/login');
  if (!user.profile?.gender || !ageBadge(user.profile.age)) redirect('/account/edit');

  const query = await searchParams;
  const fixture = query.avatarGenderFixture;
  const selectedGender = fixture === 'male' || fixture === 'female' ? fixture : undefined;
  const registered = getRegisteredAvatarGender();
  const genderState: AvatarGenderState = selectedGender
    ? { status: 'ready', gender: selectedGender, source: 'selection' }
    : registered.status === 'ready'
      ? registered
      : { status: 'ready', gender: user.profile.gender, source: 'profile' };
  const currentModeLabel = selectedGender === 'male'
    ? '男性用（若者・大人・シニア）'
    : selectedGender === 'female'
      ? '女性用（若者・大人・シニア）'
      : '男女6体';

  return (
    <main className="avatar-page avatar-v3-page">
      <header className="avatar-header">
        <div className="avatar-header-left">
          <Logo priority />
          <h1 className="avatar-header-title">アバター編集</h1>
        </div>
        <div className="avatar-header-actions">
          <span>{user.profile.nickname || '利用者'}さんでログイン中</span>
          <Link href="/account" className="avatar-btn-secondary">マイページ</Link>
          <Link href="/" className="avatar-btn-secondary">TOPへ戻る</Link>
        </div>
      </header>
      <div className="avatar-v3-nav-banner">
        <span className="avatar-v3-nav-desc">完成版V3アバター ／ {currentModeLabel}</span>
        <div className="avatar-v3-nav-links" aria-label="アバターの表示">
          <Link href="/avatar" className={`avatar-v3-pill ${!selectedGender ? 'active' : ''}`} aria-current={!selectedGender ? 'page' : undefined}>全6体</Link>
          <Link href="/avatar?avatarGenderFixture=male" className={`avatar-v3-pill ${selectedGender === 'male' ? 'active' : ''}`} aria-current={selectedGender === 'male' ? 'page' : undefined}>男性</Link>
          <Link href="/avatar?avatarGenderFixture=female" className={`avatar-v3-pill ${selectedGender === 'female' ? 'active' : ''}`} aria-current={selectedGender === 'female' ? 'page' : undefined}>女性</Link>
        </div>
      </div>
      <AvatarCustomizer
        key={user.id}
        ownerId={user.id}
        nickname={user.profile.nickname || '利用者'}
        genderState={genderState}
        assets={V3_UNIFIED_ASSETS}
        allCandidatesMode={!selectedGender}
      />
    </main>
  );
}
