"use client";
import { Icon } from "./Icon";
import { useEffect, useRef, useState } from "react";
import {useRouter} from "next/navigation";
import {readLocalState,changeLocalState,LocalClientError} from "../../shared/local-demo-client";
import { PasswordField } from "./PasswordField";

export function LoginForm() {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [message, setMessage] = useState("");
  const router=useRouter();
  const [available,setAvailable]=useState<boolean|null>(null);
  const [busy,setBusy]=useState(false);const pending=useRef(false);
  useEffect(()=>{
    const abort=new AbortController();
    readLocalState(abort.signal).then(()=>setAvailable(true)).catch(error=>{
      if(abort.signal.aborted)return;
      if(error instanceof LocalClientError&&error.status===404)setAvailable(false);
      else{setAvailable(true);setMessage('ログインの準備に失敗しました。サーバーと保存ファイルを確認してください。');}
    });return()=>abort.abort();
  },[]);
  async function login(event:React.FormEvent<HTMLFormElement>){
    event.preventDefault();if(pending.current||!available)return;
    pending.current=true;setBusy(true);setMessage('');
    try{await changeLocalState({action:'login',email,password});router.push('/ai-navi');}
    catch(error){setMessage(error instanceof Error?error.message:'ログインできませんでした。');}
    finally{setPassword('');pending.current=false;setBusy(false);}
  }

  return (
    <div className="auth-form-screen login-screen">
      <div className="auth-login-hello" aria-hidden="true">Hello again! <span><Icon name="spark-pink" /></span></div>
      <div className="auth-form-title">
        <p className="auth-title-kicker">今日も、あたらしいワクワクを。</p>
        <h1 className="form-heading">おかえりなさい。</h1>
        <p className="auth-form-description">あなたの「好き」の続きを、ここから。</p>
      </div>
      {available===false&&<p className="auth-demo-note">この起動方法ではログインを利用できません。開発用の起動手順を確認してください。</p>}
      <form className="auth-login-form" onSubmit={login} aria-busy={busy}>
        <div className="form-field"><label htmlFor="login-email">メールアドレス</label><input id="login-email" type="email" autoComplete="email" required maxLength={254} value={email} onChange={event => setEmail(event.target.value)} placeholder="例：hello@example.com" /></div>
        <PasswordField label="パスワード" id="login-password" value={password} onChange={setPassword} />
        <details className="auth-disclosure auth-recovery"><summary>パスワードを忘れた方へ</summary><p>パスワード再設定機能は準備中です。認証サービスの接続後に、メールで再設定できるようになります。</p></details>
        {message && <p role="alert" className="form-alert">{message}</p>}
        <button className="primary-button" type="submit" disabled={busy||available!==true}>{busy?"ログイン中…":"ログイン"} <span aria-hidden="true">→</span></button>
      </form>
      <div className="auth-login-bottom">
        <div className="auth-or-divider"><span>はじめての方はこちら</span></div>
        <a className="secondary-button" href="/signup">アカウントを作成 <span aria-hidden="true">↗</span></a>
        <p className="auth-demo-note"><strong>開発体験版</strong> 登録済みテストアカウントでログインできます。新規登録は画面体験のみです。</p>
      </div>
    </div>
  );
}
