'use client';
import { useEffect, useRef, useState } from 'react';
import type {LocalDemoState} from '../../shared/local-demo';
import type { DiscoveryHistory, Material, PopularitySummary, Suggestion } from '../../server/ai-navi/types';

type Props={profile:{id:string;label:string;description:string;history:DiscoveryHistory[]};materials:Material[];popularity:PopularitySummary;revision:string;blocked:boolean;refreshState:()=>Promise<LocalDemoState>};
export function DemoClient({profile,materials,popularity,revision,blocked,refreshState}:Props){
  const [loading,setLoading]=useState(false);
  const [error,setError]=useState('');
  const [suggestions,setSuggestions]=useState<Suggestion[]|null>(null);
  const request=useRef<AbortController|null>(null);
  const sequence=useRef(0);
  useEffect(()=>()=>{request.current?.abort();sequence.current++;},[]);
  async function propose(){
    if(request.current||blocked) return;
    const controller=new AbortController();request.current=controller;
    const version=++sequence.current;
    setLoading(true);setError('');setSuggestions(null);
    try{
      const response=await fetch('/api/ai-navi/demo',{method:'POST',headers:{'Content-Type':'application/json'},
        body:JSON.stringify({}),signal:controller.signal});
      const result=await response.json();
      if(version!==sequence.current) return;
      if(!response.ok) {setError(result.error?.message??'提案を取得できませんでした。');return;}
      if(!Array.isArray(result.suggestions)) throw new Error('Invalid response');
      const latest=await refreshState();
      if(version!==sequence.current)return;
      if(result.revision!==revision||latest.revision!==revision||latest.currentUser?.id!==profile.id){setError('ユーザーまたは履歴が更新されたため、古い提案を破棄しました。');return;}
      setSuggestions(result.suggestions);
    }catch(failure){
      if(version===sequence.current && !(failure instanceof Error&&failure.name==='AbortError')) setError('提案を取得できませんでした。接続を確認してもう一度お試しください。');
    }finally{
      if(version===sequence.current){request.current=null;setLoading(false);}
    }
  }
  const formatDate=(at:number)=>new Intl.DateTimeFormat('ja-JP',{timeZone:'Asia/Tokyo',month:'numeric',day:'numeric'}).format(at);
  function evidenceLabel(item:Suggestion,id:string){
    if(item.basis==='history'){
      const event=profile.history.find(value=>value.id===id);
      return event?`${event.title}（${event.kind==='complete'?'受講完了':'閲覧'}）`:null;
    }
    const material=materials.find(value=>value.id===id);
    const stats=popularity.items.find(value=>value.materialId===id&&value.popular);
    return material&&stats?`${material.title}：${Math.round((stats.ratio??0)*100)}%（${stats.users} / ${popularity.activeUsers}人）`:null;
  }
  return <main className="navi-demo">
    <header className="navi-demo-header"><a href="/" className="navi-demo-brand">ME LIFE<span>みらいふ</span></a><a href="/">ホームへ戻る ↗</a></header>
    <section className="navi-demo-intro">
      <span className="navi-demo-kicker">AI 未来ナビ · 開発体験版</span>
      <h1>履歴から見つかる、<br/>新しい楽しみ。</h1>
      <p>これまで見たもの、学んだこと。そのつながりから、<br/>まだ気づいていない趣味やテーマを見つけます。</p>
    </section>
    <div className="navi-demo-notice"><strong>サンプルの履歴で試す体験版です。</strong> 教材・履歴・人気は架空のデータです。実際の登録情報は使用しません。提案の実行にはOpenAI APIの利用料が発生します。</div>
    <div className="navi-demo-columns">
      <section className="navi-demo-panel">
        <span className="navi-demo-step">01 / これまでの体験</span>
        <h2>学びの足あと</h2>
        <p className="navi-demo-explain">やりたいことを言葉にしなくても大丈夫。閲覧・受講の履歴を手がかりにします。</p>
        <div data-testid="learning-history" className="navi-demo-history">
          {profile.history.length?<ol>{profile.history.map(event=><li key={event.id}>
            <span className={`navi-demo-history-kind ${event.kind}`}>{event.kind==='complete'?'受講完了':'閲覧'}</span>
            <h3>{event.title}</h3><p>{event.description}</p>
          </li>)}</ol>:<div className="navi-demo-no-history"><h3>まだ履歴がありません</h3><p>みらいふ内で人気のテーマを、新しい楽しみの入口として紹介します。</p></div>}
        </div>
        <p className="navi-demo-small">閲覧した教材を「受講完了」や「習得済み」とは扱いません。</p>
        <p className="navi-demo-small">{profile.label}さんのJSONに保存した履歴です。再ログインしても保持されます。</p>
        <button className="navi-demo-submit" disabled={loading||blocked} onClick={propose}>{loading?'テーマを考えています…':'履歴からテーマを見つける'}<span aria-hidden="true">↗</span></button>
        <p className="navi-demo-small">1回ずつ、60秒に3回までお試しいただけます。</p>
        {error&&<p className="navi-demo-error" role="alert">{error}</p>}
        <details className="navi-demo-settings"><summary>はじめて使う方へ：接続の設定</summary><p>サーバーの .env.local に OPENAI_API_KEY と OPENAI_MODEL を設定して、開発サーバーを再起動します。キーはこの画面やチャットに入力しないでください。</p></details>
      </section>
      <section className="navi-demo-panel navi-demo-result" aria-live="polite" aria-busy={loading}>
        <span className="navi-demo-step">02 / 見つかる新しい楽しみ</span><h2>こんなテーマはいかがですか？</h2>
        {loading?<div className="navi-demo-empty"><span className="navi-demo-orbit" aria-hidden="true">✦</span><p>履歴のつながりから、<br/>次に楽しめそうなテーマを考えています。</p></div>
        :suggestions===null?<div className="navi-demo-empty"><span aria-hidden="true">✧</span><p>これまでの体験から、新しい発見へ。<br/>テーマと、その理由、最初の一歩をご提案します。</p></div>
        :suggestions.length===0?<p>今回はテーマが見つかりませんでした。別の履歴ケースをお試しください。</p>
        :<div data-testid="suggestions" className="navi-demo-suggestions">{suggestions.map(item=><article key={item.theme}>
          <span className="navi-demo-tag">{item.basis==='history'?'履歴のつながりから':'人気を入口にした提案'}</span>
          <h3>{item.theme}</h3>
          <div className="navi-demo-reason"><strong>なぜ合いそう？</strong><p>{item.reason}</p></div>
          <div className="navi-demo-next"><strong>最初に試せること</strong><p>{item.firstStep}</p></div>
          <div className="navi-demo-evidence"><strong>{item.basis==='history'?'手がかりになった履歴':'参考にしたみらいふ内の人気（サンプル）'}</strong>
            <ul>{item.evidenceIds.map(id=>{const label=evidenceLabel(item,id);return label?<li key={id}>{label}</li>:null;})}</ul>
          </div>
          {item.relatedMaterialIds.length>0&&<details className="navi-demo-related"><summary>関連する教材</summary><p className="navi-demo-small">テーマを試すための補助案内です。</p>{item.relatedMaterialIds.map(id=>{const material=materials.find(value=>value.id===id);return material?<div key={id}><h4>{material.title}</h4><p>{material.description}</p></div>:null;})}<p className="navi-demo-small">サンプル教材のため、実際の受講はできません。</p></details>}
        </article>)}</div>}
      </section>
    </div>
    <section className="navi-demo-catalog"><details><summary>体験版で使っているサンプル教材・人気情報</summary>
      <p>教材は趣味・テーマを見つけ、試すための補助情報です。人気の基準は直近30日の教材利用者の80%以上。以下はサンプル{popularity.activeUsers}人の集計です（{formatDate(popularity.start)}〜{formatDate(popularity.end)}）。</p>
      <ul>{materials.map(material=>{const stats=popularity.items.find(item=>item.materialId===material.id);return <li key={material.id}><h3>{material.title}</h3><p>{material.description}</p>{stats?.popular&&<span className="navi-demo-popular">サンプル人気 <strong>{Math.round((stats.ratio??0)*100)}%（{stats.users} / {popularity.activeUsers}人）</strong></span>}</li>})}</ul>
    </details></section>
    <footer className="navi-demo-footer">何歳からでも、新しい未来へ。<span>ME LIFE / AI未来ナビ</span></footer>
  </main>;
}
