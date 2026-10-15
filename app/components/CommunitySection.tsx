import Image from "next/image";

export function CommunitySection() {
  return (
    <section className="home-section home-community" id="community">
      <div className="home-container home-community-grid">
        <div className="home-community-art"><span className="home-community-spark" aria-hidden="true">✳</span><div className="home-picture-frame"><Image src="/images/milife-future.jpg" alt="園芸やパソコンを一緒に学び、世代をこえて手を取り合って未来へ進む人たち" width={1888} height={1337} sizes="(min-width: 1024px) 45vw, 90vw" /></div><p className="home-sticker home-sticker-pink">ちがう世代。だから、おもしろい。</p></div>
        <div className="home-community-copy"><p className="home-eyebrow">LEARN FROM EACH OTHER</p><h2>今日は先生。<br />明日は、<span className="home-marker">生徒。</span></h2><p>スマートフォンの便利な使い方を教えたら、<br className="home-desktop-break" />今度は、長年育ててきた庭の話を聞いてみる。<br />年齢で、教える人と教わる人を決めない。<br /><strong>それが、ミライフの学び合いです。</strong></p>
          <div className="home-exchange-notes"><article><span aria-hidden="true">📱</span><div><h3>「その写真、もっと素敵に撮れるよ！」</h3><p>若い世代のデジタルの知識を、暮らしの楽しみに。</p></div></article><article><span aria-hidden="true">🪴</span><div><h3>「植物が元気に育つコツ、知ってる？」</h3><p>積み重ねた経験や生活の知恵を、誰かの学びに。</p></div></article></div>
        </div>
      </div>
    </section>
  );
}
