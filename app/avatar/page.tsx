import type { Metadata } from 'next';
import { UnityExperience } from '../components/UnityExperience';
import { Logo } from '../components/Logo';
import '../plaza/unity.css';
import './avatar.css';
export const metadata: Metadata = {title:'アバター作成 | ミライフ'};
export default function AvatarPage() {
 return <main className="unity-page avatar-setup-page">
  <header className="avatar-setup-header"><Logo priority/><p>世代をこえて、学び合う未来へ。</p><a href="/" aria-label="TOPへ戻る">← TOPへ戻る</a></header>
  <section className="avatar-setup-intro"><div><span className="avatar-setup-label">ワールドへの、最初の一歩。</span><h1>あなたらしい姿で、はじめよう。</h1></div><p>お気に入りの服や髪型を選んで、<br/>新しい出会いへ出かけましょう。</p></section>
  <UnityExperience mode="avatar" />
  <footer className="unity-guide avatar-setup-guide"><span>学んで、教えて、つながろう。</span><p>名前と衣装は、このブラウザに保存されます。</p></footer>
 </main>;
}
