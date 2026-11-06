import type { Metadata } from 'next';
import Link from 'next/link';
import { Logo } from '../../components/Logo';
import { getCurrentUser } from '../../../server/auth/server';
import { AvatarCustomizer } from '../AvatarCustomizer';
import '../avatar.css';
import { V3_UNIFIED_ASSETS } from '../../../shared/avatar-development-assets';

export const metadata: Metadata = {
  title: 'v3アバター完成版 検証 | ミライフ',
  description: '完成版6体のアバター（怒り・笑顔・おだやか）のWeb動作検証ページです。',
};

export default async function AvatarV3VerificationPage({
  searchParams,
}: {
  searchParams: Promise<{ avatarGenderFixture?: string }>;
}) {
  const query = await searchParams;
  const fixture = query?.avatarGenderFixture;
  const isMale = fixture === 'male';
  const isFemale = fixture === 'female';

  const genderState = (isMale || isFemale)
    ? ({ status: 'ready', gender: fixture, source: 'development-fixture' } as const)
    : undefined;
  const allCandidatesMode = !genderState;

  const user = await getCurrentUser();

  const currentModeLabel = isMale
    ? '男性用モード（若者男性・大人男性・シニア男性）'
    : isFemale
      ? '女性用モード（若者女性・大人女性・シニア女性）'
      : '全モデル表示モード（男女6体）';

  return (
    <main className="avatar-page avatar-v3-page">
      <header className="avatar-header">
        <div className="avatar-header-left">
          <Logo priority />
          <span className="avatar-header-title">アバター編集（v3完成版 検証環境）</span>
          <span className="avatar-v3-badge">
            検証モード
          </span>
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
          <Link href="/avatar" className="avatar-btn-secondary">
            通常のアバター編集へ
          </Link>
          <Link href="/" className="avatar-btn-secondary">
            TOPへ戻る
          </Link>
        </div>
      </header>

      <div className="avatar-v3-nav-banner">
        <span className="avatar-v3-nav-desc">
          【v3完成版アバター検証環境】{currentModeLabel} ／ 初期表情: おだやか(2)
        </span>
        <div className="avatar-v3-nav-links">
          <Link
            href="/avatar/v3"
            className={`avatar-v3-pill ${!isMale && !isFemale ? 'active' : ''}`}
          >
            全6体表示
          </Link>
          <Link
            href="/avatar/v3?avatarGenderFixture=male"
            className={`avatar-v3-pill ${isMale ? 'active' : ''}`}
          >
            男性用（3体）
          </Link>
          <Link
            href="/avatar/v3?avatarGenderFixture=female"
            className={`avatar-v3-pill ${isFemale ? 'active' : ''}`}
          >
            女性用（3体）
          </Link>
        </div>
      </div>

      <AvatarCustomizer
        assets={V3_UNIFIED_ASSETS}
        genderState={genderState}
        allCandidatesMode={allCandidatesMode}
      />
    </main>
  );
}

