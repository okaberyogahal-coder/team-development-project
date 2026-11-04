import type { Metadata } from 'next';
import Link from 'next/link';
import { Logo } from '../components/Logo';
import { getCurrentUser } from '../../server/auth/server';
import { AvatarCustomizer } from './AvatarCustomizer';
import './avatar.css';

export const metadata: Metadata = {
  title: 'アバター作成・編集 | ミライフ',
  description: '自分らしい3Dアバターに着せ替えて、未来都市ミライフへ参加しましょう。',
};

export default async function AvatarPage() {
  const user = await getCurrentUser();

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

      <AvatarCustomizer />
    </main>
  );
}

