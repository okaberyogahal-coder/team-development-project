import Image from "next/image";

export function HeroSection() {
  return (
    <section id="about" className="home-hero">
      <div className="home-hero-confetti" aria-hidden="true"><span>✦</span><span>✳</span><span>●</span></div>
      <div className="home-container home-hero-grid">
        <div className="home-hero-copy">
          <p className="home-sticker">毎日に、あたらしいワクワクを。</p>
          <h1>いくつになっても、<br /><span className="home-marker">「はじめて」は</span><br />おもしろい。</h1>
          <p className="home-hero-description">あなたの得意が、誰かのきっかけに。<br />誰かとの出会いが、次の一歩に。<br /><strong>世代をこえて学び合う、ミライフ。</strong></p>
          <div className="home-hero-actions"><a className="home-button home-button-blue" href="/signup">アカウントを作成 <span aria-hidden="true">→</span></a><a className="home-hero-more" href="#features">どんなことができる？ <span aria-hidden="true">↓</span></a></div>
          <p className="home-fine-print">18歳以上の方が対象です · PCブラウザー推奨</p>
        </div>
        <div className="home-hero-art">
          <div className="home-picture-frame"><Image src="/images/milife-world.jpg" alt="未来の街で若者とシニアがパソコンや園芸を通して学び合う、ミライフの世界" width={800} height={450} sizes="(min-width: 1024px) 56vw, 94vw" priority /></div>
          <span className="home-art-sticker">誰もが先生。<br /><strong>誰もが生徒！</strong></span>
          <div className="home-hero-tags" aria-label="ミライフの楽しみ方"><span>📖 学んで</span><span>🌱 教えて</span><span>💬 つながろう</span></div>
          <p className="home-art-caption">2127年の学び合いを描く、未来創造展の作品です。</p>
        </div>
      </div>
      <div className="home-ribbon"><p><span>学ぶって、たのしい。</span><b aria-hidden="true">✦</b><span>教えるって、うれしい。</span><b aria-hidden="true">✦</b><span>つながるって、おもしろい。</span></p></div>
    </section>
  );
}
