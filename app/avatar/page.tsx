import type { Metadata } from 'next';
import Link from 'next/link';
import { Logo } from '../components/Logo';
import { getCurrentUser } from '../../server/auth/server';
import { AvatarCustomizer } from './AvatarCustomizer';
import './avatar.css';
import { getRegisteredAvatarGender, type AvatarGenderState } from '../../shared/avatar-candidates';
import { V3_UNIFIED_ASSETS } from '../../shared/avatar-development-assets';

export const metadata: Metadata = {
  title: 'アバター作成・編集 | ミライフ',
  description: '自分らしい3Dアバターに着せ替えて、未来都市ミライフへ参加しましょう。',
};

export default async function AvatarPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const query = await searchParams;
  const selectedGender = query.avatarGenderFixture === 'male' || query.avatarGenderFixture === 'female'
    ? query.avatarGenderFixture
    : undefined;
  const genderState: AvatarGenderState = selectedGender
    ? { status: 'ready', gender: selectedGender, source: 'selection' }
    : getRegisteredAvatarGender();
  const user = await getCurrentUser();
  const currentModeLabel = selectedGender === 'male'
    ? '男性用（若者・大人・シニア）'
    : selectedGender === 'female'
      ? '女性用（若者・大人・シニア）'
      : '男女6体';

  return (
    <main className="avatar-page">
      <header className="avatar-header">
        <div className="avatar-header-left">
          <Logo priority />
          <span className="avatar-header-title">アバター編集</span>
        </div>
        <div className="avatar-header-actions">
          {user ? (
            <span style={{ fontSize: '13px', color: '#1e3a8a', fontWeight: 700 }}>
              {user.profile?.nickname || '利用者'}さんでログイン中
            </span>
          ) : (
            <Link href="/login" className="avatar-btn-secondary">
              ログイン
            </Link>
          )}
          <Link href="/" className="avatar-btn-secondary">
            TOPへ戻る
          </Link>
        </div>
      </header>

      <div className="avatar-v3-nav-banner">
        <span className="avatar-v3-nav-desc">完成版V3アバター ／ {currentModeLabel}</span>
        <div className="avatar-v3-nav-links" aria-label="アバターの表示">
          <Link href="/avatar" className={`avatar-v3-pill ${!selectedGender ? 'active' : ''}`} aria-current={!selectedGender ? 'page' : undefined}>
            全6体
          </Link>
          <Link href="/avatar?avatarGenderFixture=male" className={`avatar-v3-pill ${selectedGender === 'male' ? 'active' : ''}`} aria-current={selectedGender === 'male' ? 'page' : undefined}>
            男性
          </Link>
          <Link href="/avatar?avatarGenderFixture=female" className={`avatar-v3-pill ${selectedGender === 'female' ? 'active' : ''}`} aria-current={selectedGender === 'female' ? 'page' : undefined}>
            女性
          </Link>
        </div>
      </div>

      <AvatarCustomizer
        genderState={genderState}
        assets={V3_UNIFIED_ASSETS}
        allCandidatesMode={!selectedGender}
      />
    </main>
  );
}
