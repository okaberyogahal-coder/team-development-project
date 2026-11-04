import type {Metadata} from 'next';
import { getCurrentUser } from '../../server/auth/server';
import './unity.css';

export const metadata:Metadata={title:'交流空間 | ミライフ',description:'アバターで集まり、世代を超えておしゃべりするミライフの交流空間。'};

type Props = {
  searchParams: Promise<{ avatar?: string }>;
};

export default async function PlazaPage({ searchParams }: Props){
 const user = await getCurrentUser();
 const params = await searchParams;

 // /avatar ページから渡ってきたアバターJSONをiframeのURLに転送する
 const avatarParam = params.avatar ? `?avatar=${encodeURIComponent(params.avatar)}` : '';
 const unitySrc = `/unity/FutureCity/index.html${avatarParam}`;

 return <main className="unity-page">
  <header className="unity-header">
   <div>
    <span className="unity-brand">MIRAI LIFE</span>
    <h1>ミライフ ワールド</h1>
   </div>
   <div style={{ display: 'flex', gap: '16px', alignItems: 'center' }}>
    <a href="/avatar">🧍 アバター編集</a>
    <a href={user ? "/account" : "/login"}>{user ? `${user.profile?.nickname || '利用者'}さんのマイページ` : 'ログイン'}</a>
    <a href="/">TOPへ戻る</a>
   </div>
  </header>
  <iframe className="unity-frame" title="ミライフ Unityワールド" src={unitySrc} allow="fullscreen; autoplay" allowFullScreen />
  <footer className="unity-guide">
   <p>画面をクリックして操作をはじめる · W A S D / 矢印キーで移動 · マウスで見回す · Escでカーソルを表示</p>
   <a href="/unity/FutureCity/index.html" target="_blank" rel="noreferrer">ワールドだけを別タブで開く ↗</a>
  </footer>
 </main>;
}
