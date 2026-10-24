"use client";
import {LocalDemoLogin} from "./LocalDemoLogin";
import { Icon } from "./Icon";
import { useEffect, useState } from "react";
import { readDemoProfile, type DemoProfile } from "../../shared/demo-profile";
import { PasswordField } from "./PasswordField";

export function LoginForm() {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [message, setMessage] = useState("");
  const [profile, setProfile] = useState<DemoProfile | null>(null);
  useEffect(() => { try { setProfile(readDemoProfile(window.localStorage)); } catch { /* Storage can be disabled. */ } }, []);

  return (
    <div className="auth-form-screen login-screen">
      <div className="auth-login-hello" aria-hidden="true">Hello again! <span><Icon name="spark-pink" /></span></div>
      <div className="auth-form-title">
        <p className="auth-title-kicker">今日も、あたらしいワクワクを。</p>
        <h1 className="form-heading">おかえりなさい。</h1>
        <p className="auth-form-description">あなたの「好き」の続きを、ここから。</p>
      </div>
      <LocalDemoLogin />
      {profile && <div className="auth-demo-note"><strong>{profile.nickname}さんの体験データがあります</strong><p>実際のログイン認証は行いません。このブラウザのデータで再開します。</p><a className="secondary-button" href={profile.appearance ? "/plaza" : "/avatar"}>保存した体験を再開する</a></div>}
      <form className="auth-login-form" onSubmit={event => {
        event.preventDefault();
        setMessage("認証サービスは未接続です。現在はログインできません。入力した情報は送信・保存されません。");
      }}>
        <div className="form-field"><label htmlFor="login-email">メールアドレス</label><input id="login-email" type="email" autoComplete="email" required maxLength={254} value={email} onChange={event => setEmail(event.target.value)} placeholder="例：hello@example.com" /></div>
        <PasswordField label="パスワード" id="login-password" value={password} onChange={setPassword} />
        <details className="auth-disclosure auth-recovery"><summary>パスワードを忘れた方へ</summary><p>パスワード再設定機能は準備中です。認証サービスの接続後に、メールで再設定できるようになります。</p></details>
        {message && <p role="alert" className="form-alert">{message}</p>}
        <button className="primary-button" type="submit">ログイン <span aria-hidden="true">→</span></button>
      </form>
      <div className="auth-login-bottom">
        <div className="auth-or-divider"><span>はじめての方はこちら</span></div>
        <a className="secondary-button" href="/signup">アカウントを作成 <span aria-hidden="true">↗</span></a>
        <p className="auth-demo-note"><strong>画面体験版</strong> 実際の認証は行いません。登録体験からアバターを作成して広場へ入れます。</p>
      </div>
    </div>
  );
}
