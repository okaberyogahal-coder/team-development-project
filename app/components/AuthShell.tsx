import { Icon } from "./Icon";
import Image from "next/image";
import { Logo } from "./Logo";
import "./auth.css";

export function AuthShell({ children }: { children: React.ReactNode }) {
  return (
    <main className="auth-page">
      <div className="auth-confetti" aria-hidden="true"><i /><i /><i /><i /><i /><i /></div>
      <header className="auth-header">
        <Logo />
        <p>世代をこえて、学び合う未来へ。</p>
        <a className="auth-home-link" href="/"><span aria-hidden="true">←</span> トップへ戻る</a>
      </header>
      <div className="auth-layout">
        <aside className="auth-intro">
          <div className="auth-intro-copy">
            <p className="auth-sticker">毎日に、あたらしいワクワクを。</p>
            <h2>「やってみたい」に、<br /><span>何歳だっていい。</span></h2>
            <p className="auth-intro-description">あなたの得意が、誰かのきっかけに。<br />誰かとの出会いが、次の一歩に。</p>
          </div>
          <div className="auth-artwork">
            <span className="auth-artwork-spark" aria-hidden="true"><Icon name="spark" /></span>
            <Image src="/images/milife-auth-welcome.jpg" alt="明るい学びの空間で、若者とシニアが笑顔で迎え合う様子" width={1448} height={1086} priority sizes="(min-width: 1024px) 38vw, 80vw" />
            <span className="auth-artwork-sticker">いっしょなら、<br /><strong>もっと楽しい！</strong></span>
          </div>
          <div className="auth-value-tags" aria-label="ミライフでできること"><span><Icon name="open-book" /> 学ぶ</span><span><Icon name="sprout" /> 教える</span><span><Icon name="chat" /> つながる</span></div>
          <p className="auth-intro-footnote">あなたらしい未来を、ミライフで。</p>
        </aside>
        <section aria-label="アカウント入力" className="auth-panel">
          {children}
        </section>
      </div>
      <footer className="auth-page-footer"><span>ME LIFE</span>　小さな一歩が、未来を変える。</footer>
    </main>
  );
}
