import type { Metadata } from 'next';
import { UnityExperience } from '../components/UnityExperience';
import '../plaza/unity.css';
export const metadata: Metadata = {title:'アバター作成 | ミライフ'};
export default function AvatarPage() {
 return <main className="unity-page"><header className="unity-header"><div><span className="unity-brand">MIRAI LIFE</span><h1>3Dアバターを作成</h1></div><a href="/">TOPへ戻る</a></header><UnityExperience mode="avatar" /><footer className="unity-guide"><p>体験用です。名前と外見はこのブラウザだけに保存されます。実際のアカウント認証やDB保存は行いません。</p></footer></main>;
}
