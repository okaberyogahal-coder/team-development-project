"use client";
import { Icon } from "./Icon";
import { useState } from "react";
import { PasswordField } from "./PasswordField";
import { useRouter } from "next/navigation";
import { notifyAuthChanged } from "../../shared/auth/events";

export function LoginForm() {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [message, setMessage] = useState("");
  const [pending, setPending] = useState(false);
  const router = useRouter();

  return (
    <div className="auth-form-screen login-screen">
      <div className="auth-login-hello" aria-hidden="true">Hello again! <span><Icon name="spark-pink" /></span></div>
      <div className="auth-form-title">
        <p className="auth-title-kicker">今日も、あたらしいワクワクを。</p>
        <h1 className="form-heading">おかえりなさい。</h1>
        <p className="auth-form-description">あなたの「好き」の続きを、ここから。</p>
      </div>
      <form className="auth-login-form" aria-busy={pending} onSubmit={async event => {
        event.preventDefault();
        if (pending) return;
        setMessage("");
        setPending(true);
        try {
          const response = await fetch("/api/auth/sign-in/email", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ email, password }) });
          const data = await response.json();
          if (!response.ok) { setMessage(data.message || "ログインできませんでした。"); return; }
          setPassword("");
          notifyAuthChanged();
          router.replace("/avatar");
          router.refresh();
        } catch { setMessage("接続できませんでした。通信状態を確認してお試しください。"); }
        finally { setPending(false); }
      }}>
        <div className="form-field"><label htmlFor="login-email">メールアドレス</label><input id="login-email" name="email" type="email" autoComplete="email" required maxLength={254} aria-describedby={message ? "login-error" : undefined} aria-invalid={Boolean(message) || undefined} value={email} onChange={event => setEmail(event.target.value)} placeholder="例：hello@example.com" /></div>
        <PasswordField label="パスワード" id="login-password" value={password} onChange={setPassword} describedBy={message ? "login-error" : undefined} invalid={Boolean(message)} />
        <details className="auth-disclosure auth-recovery"><summary>パスワードを忘れた方へ</summary><p>パスワード再設定機能は準備中です。認証サービスの接続後に、メールで再設定できるようになります。</p></details>
        {message && <p id="login-error" role="alert" className="form-alert">{message}</p>}
        <button className="primary-button" type="submit" disabled={pending}>{pending ? "ログイン中…" : "ログイン"} <span aria-hidden="true">→</span></button>
      </form>
      <div className="auth-login-bottom">
        <div className="auth-or-divider"><span>はじめての方はこちら</span></div>
        <a className="secondary-button" href="/signup">アカウントを作成 <span aria-hidden="true">↗</span></a>
        <p className="auth-demo-note">登録したメールアドレスとパスワードでログインできます。</p>
      </div>
    </div>
  );
}
