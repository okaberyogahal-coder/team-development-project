import type {Metadata} from 'next';
import './unity.css';
export const metadata:Metadata={title:'交流空間 | ミライフ',description:'アバターで集まり、世代を超えておしゃべりするミライフの交流空間。'};
export default function PlazaPage(){
 return <main className="unity-page">
  <header className="unity-header">
   <div><span className="unity-brand">MIRAI LIFE</span><h1>ミライフ ワールド</h1></div>
   <a href="/">TOPへ戻る</a>
  </header>
  <iframe className="unity-frame" title="ミライフ Unityワールド" src="/unity/FutureCity/index.html" allow="fullscreen; autoplay" allowFullScreen />
  <footer className="unity-guide"><p>画面をクリックして操作をはじめる · W A S D / 矢印キーで移動 · マウスで見回す · Escでカーソルを表示</p><a href="/unity/FutureCity/index.html" target="_blank" rel="noreferrer">ワールドだけを別タブで開く ↗</a></footer>
 </main>;
}
