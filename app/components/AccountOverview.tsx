import Link from 'next/link';
import {Logo} from './Logo';
import {LogoutButton} from './LogoutButton';
import {AccountAvatarPreview} from './AccountAvatarPreview';
import {ageBadge} from '../../shared/profile-demographics';
import '../account/account.css';

export type AccountOverviewUser = {
 id:string; email:string|null;
 profile:{nickname:string;gender:string|null;age:number|null;wantsToLearn:string[];teaches:string[]}|null;
 preferences:{interests:string[];strengths:string|null;goal:string|null}|null;
};

function Topics({items,tone}:{items:string[];tone:string}) {
 return items.length ? <span className={`account-topics account-topics-${tone}`}>{items.map((item,index)=><span key={`${item}-${index}`}>{item}</span>)}</span> : <span className="account-unset">未設定</span>;
}

export function AccountOverview({user,updated=false}:{user:AccountOverviewUser;updated?:boolean}) {
 const nickname = user.profile?.nickname || '利用者';
 const badge = ageBadge(user.profile?.age);
 return <main className="account-page">
  <header className="account-header"><Logo priority/><span className="account-header-caption">世代をこえて、学び合う未来へ。</span><Link className="account-home-link" href="/" aria-label="トップページへ">← トップページへ</Link></header>
  <div className="account-page-body">
   <div className="account-page-heading"><div><p className="account-eyebrow">マイページ</p><h1>こんにちは、{nickname}さん。</h1><p>あなたの「好き」の続きを、ここから。</p></div><Link className="primary-button account-world-link" href="/plaza">ワールドに入る <span aria-hidden="true">↗</span></Link></div>
   {updated&&<p role="status" className="account-notice">✓ プロフィールを保存しました。</p>}
   {(!user.profile?.gender||!badge)&&<p role="status" className="account-notice account-notice-warning">性別・年齢の登録が必要です。<Link href="/account/edit">プロフィールを編集してください →</Link></p>}
   <div className="account-grid">
    <aside className="account-avatar-card" aria-label="あなたのアバター">
     <div className="account-avatar-heading"><span className="account-eyebrow">MY AVATAR</span><span className="account-avatar-dot" aria-hidden="true"/></div>
     <AccountAvatarPreview key={user.id} ownerId={user.id} nickname={nickname} gender={user.profile?.gender || null}/>
     <div className="account-avatar-identity"><span className="account-age-badge">{badge || '年齢バッジ未設定'}</span><h2>{nickname}</h2><p>いまのあなたのアバター</p></div>
     <Link className="secondary-button account-avatar-edit" href="/avatar">アバターを変更 <span aria-hidden="true">↗</span></Link>
     <p className="account-avatar-note">外見はこのブラウザに保存されています。</p>
    </aside>
    <section className="account-profile" aria-label="あなたのプロフィール">
     <div className="account-profile-heading"><div><p className="account-eyebrow">MY PROFILE</p><h2>あなたのプロフィール</h2></div><div className="account-profile-actions"><Link className="secondary-button account-profile-edit" href="/account/edit">プロフィールを編集 <span aria-hidden="true">↗</span></Link><LogoutButton/></div></div>
     <section className="account-info-card"><div className="account-card-heading"><span className="account-card-number">01</span><h3>基本情報</h3></div><dl className="account-details account-basic-details">
      <div className="account-email-row"><dt>メールアドレス</dt><dd>{user.email || '未設定'}</dd></div>
      <div><dt>性別</dt><dd>{user.profile?.gender==='male'?'男性':user.profile?.gender==='female'?'女性':'未設定'}</dd></div>
      <div><dt>年齢バッジ</dt><dd>{badge || '未設定'}</dd></div>
     </dl></section>
     <section className="account-info-card"><div className="account-card-heading"><span className="account-card-number">02</span><h3>好きなこと・学びのこと</h3></div><dl className="account-details account-learning-details">
      <div><dt>興味のある分野</dt><dd><Topics items={user.preferences?.interests || []} tone="blue"/></dd></div>
      <div><dt>学びたいこと</dt><dd><Topics items={user.profile?.wantsToLearn || []} tone="green"/></dd></div>
      <div><dt>教えられること</dt><dd><Topics items={user.profile?.teaches || []} tone="pink"/></dd></div>
     </dl></section>
     <section className="account-info-card account-private-card"><div className="account-card-heading"><span className="account-card-number">03</span><h3>自分のこと、これからのこと</h3><span className="account-private-label">本人のみ</span></div><dl className="account-details account-private-details">
      <div><dt>得意なこと（本人のみ）</dt><dd>{user.preferences?.strengths || <span className="account-unset">未設定</span>}</dd></div>
      <div><dt>目標（本人のみ）</dt><dd>{user.preferences?.goal || <span className="account-unset">未設定</span>}</dd></div>
     </dl><p className="account-private-note">この内容は、ほかの利用者には表示されません。</p></section>
    </section>
   </div>
   <footer className="account-footer"><span>ME LIFE</span> 小さな一歩が、未来を変える。</footer>
  </div>
 </main>;
}
