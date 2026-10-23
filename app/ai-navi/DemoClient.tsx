'use client';
import { useEffect, useRef, useState } from 'react';
import type { Material, PopularitySummary, Suggestion } from '../../server/ai-navi/types';

type Props={profiles:{id:string;label:string;description:string}[];materials:Material[];popularity:PopularitySummary};
export function DemoClient({profiles,materials,popularity}:Props){
  const [profileId,setProfileId]=useState(profiles[0].id);
  const [loading,setLoading]=useState(false);
  const [error,setError]=useState('');
  const [suggestions,setSuggestions]=useState<Suggestion[]|null>(null);
  const request=useRef<AbortController|null>(null);
  const sequence=useRef(0);
  useEffect(()=>()=>{request.current?.abort();sequence.current++;},[]);
  function changeProfile(value:string){
    sequence.current++;request.current?.abort();request.current=null;
    setProfileId(value);setLoading(false);setError('');setSuggestions(null);
  }
  async function propose(){
    if(request.current) return;
    const controller=new AbortController();request.current=controller;
    const version=++sequence.current;
    setLoading(true);setError('');setSuggestions(null);
    try{
      const response=await fetch('/api/ai-navi/demo',{method:'POST',headers:{'Content-Type':'application/json'},
        body:JSON.stringify({profileId}),signal:controller.signal});
      const result=await response.json();
      if(version!==sequence.current) return;
      if(!response.ok) {setError(result.error?.message??'提案を取得できませんでした。');return;}
      if(!Array.isArray(result.suggestions)) throw new Error('Invalid response');
      setSuggestions(result.suggestions);
    }catch(failure){
      if(version===sequence.current && !(failure instanceof Error&&failure.name==='AbortError')) setError('提案を取得できませんでした。接続を確認してもう一度お試しください。');
    }finally{
      if(version===sequence.current){request.current=null;setLoading(false);}
    }
  }
  const profile=profiles.find(item=>item.id===profileId)!;
  const formatDate=(at:number)=>new Intl.DateTimeFormat('ja-JP',{timeZone:'Asia/Tokyo',month:'numeric',day:'numeric'}).format(at);
  return <main className="navi-demo">
    <header className="navi-demo-header"><a href="/" className="navi-demo-brand">ME LIFE<span>みらいふ</span></a><a href="/">ホームへ戻る ↗</a></header>
    <section className="navi-demo-intro"><span className="navi-demo-kicker">AI 未来ナビ · 開発体験版</span>
      <h1>次の「やってみたい」を<br/>見つけよう。</h1><p>あなたの「好き」と、これまでの学びから。<br/>未来ナビが、次の一歩をいっしょに考えます。</p>
    </section>
    <div className="navi-demo-notice"><strong>サンプルで試す体験版です。</strong> 教材・プロフィール・人気は架空のデータです。実際の登録情報や履歴は使用しません。提案の実行にはOpenAI APIの利用料が発生します。</div>
    <div className="navi-demo-columns">
      <section className="navi-demo-panel"><span className="navi-demo-step">01 / あなたのサンプル</span><h2>どんな学びから、はじめる？</h2>
        <label htmlFor="demo-profile">サンプル利用者</label><select id="demo-profile" value={profileId} onChange={event=>changeProfile(event.target.value)}>{profiles.map(item=><option key={item.id} value={item.id}>{item.label}</option>)}</select>
        <div className="navi-demo-profile"><span aria-hidden="true">{profileId==='photography'?'◎':'✧'}</span><div><h3>{profile.label}</h3><p>{profile.description}</p></div></div>
        <p className="navi-demo-explain">興味・目標・閲覧した分野を優先。まだ情報がないときは、みらいふ内の人気からおすすめします。</p>
        <button className="navi-demo-submit" disabled={loading} onClick={propose}>{loading?'提案を考えています…':'AIに提案してもらう'}<span aria-hidden="true">↗</span></button>
        <p className="navi-demo-small">1回ずつ、60秒に3回までお試しいただけます。</p>
        {error&&<p className="navi-demo-error" role="alert">{error}</p>}
        <details className="navi-demo-settings"><summary>はじめて使う方へ：接続の設定</summary><p>サーバーの .env.local に OPENAI_API_KEY と OPENAI_MODEL を設定して、開発サーバーを再起動します。キーはこの画面やチャットに入力しないでください。</p></details>
      </section>
      <section className="navi-demo-panel navi-demo-result" aria-live="polite" aria-busy={loading}><span className="navi-demo-step">02 / 次の一歩</span><h2>未来ナビからの提案</h2>
        {loading?<div className="navi-demo-empty"><span className="navi-demo-orbit" aria-hidden="true">✦</span><p>サンプルの興味と学びを見ながら、<br/>次の一歩を考えています。</p></div>:suggestions===null?<div className="navi-demo-empty"><span aria-hidden="true">✧</span><p>あなたに合う「やってみたい」を。<br/>ボタンから、提案を試してみましょう。</p></div>:suggestions.length===0?<p>今回は候補が見つかりませんでした。別のサンプルをお試しください。</p>:<div data-testid="suggestions" className="navi-demo-suggestions">{suggestions.map(item=>{const material=materials.find(value=>value.id===item.materialId);return <article key={item.materialId}><span className="navi-demo-tag">サンプル教材</span><h3>{material?.title??'教材の提案'}</h3><p>{item.reason}</p><div className="navi-demo-next"><strong>次に学べること</strong><p>{item.nextLearning}</p></div><details><summary>教材の内容を見る</summary><p>{material?.description}</p><p className="navi-demo-small">この教材は体験用です。実際の受講はできません。</p></details></article>})}</div>}
      </section>
    </div>
    <section className="navi-demo-catalog"><div><span className="navi-demo-step">SAMPLE LIBRARY</span><h2>今回おすすめする教材</h2><p>人気の基準は、直近30日の教材利用者の80%以上。<br/>以下はサンプル{popularity.activeUsers}人の集計です（{formatDate(popularity.start)}〜{formatDate(popularity.end)}）。</p></div>
      <ul>{materials.map(material=>{const stats=popularity.items.find(item=>item.materialId===material.id);return <li key={material.id}><h3>{material.title}</h3><p>{material.description}</p>{stats?.popular&&<span className="navi-demo-popular">サンプル人気 <strong>{Math.round((stats.ratio??0)*100)}%（{stats.users} / {popularity.activeUsers}人）</strong></span>}</li>})}</ul>
    </section><footer className="navi-demo-footer">何歳からでも、新しい未来へ。<span>ME LIFE / AI未来ナビ</span></footer>
  </main>;
}
