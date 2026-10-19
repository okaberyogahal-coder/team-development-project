import { Logo } from "./Logo";

export function Footer() {
  return (
    <footer className="home-footer">
      <div className="home-container">
        <div className="home-closing" data-scroll-reveal><span className="home-closing-spark" aria-hidden="true">✳</span><p className="home-sticker home-sticker-white">さあ、あなたの番！</p><h2>次の「好き」、<br /><span className="home-marker">いっしょに見つけよう。</span></h2><p>いつからでも、何度でも。あなたの一歩を、ミライフで。</p><div className="home-closing-actions"><a className="home-button home-button-blue" href="/signup">アカウントを作成 <span aria-hidden="true">→</span></a><a className="home-button home-button-white" href="/login">ログイン <span aria-hidden="true">↗</span></a></div><p className="home-fine-print">18歳以上の方が対象です。</p></div>
        <div className="home-footer-bottom"><Logo /><p>世代をこえて、何歳からでも学び合える未来へ。</p><nav aria-label="フッターメニュー"><a href="/">ミライフとは</a><a href="/signup">新規登録</a><a href="/login">ログイン</a></nav></div>
        <p className="home-footer-caption">ME LIFE · 2127年の学び合いを描く、未来創造展の作品です。</p>
      </div>
    </footer>
  );
}
