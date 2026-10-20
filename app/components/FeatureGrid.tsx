import { Icon } from "./Icon";
import { features } from "../data/home";

const notes = ["あたらしい自分に出会う", "あなたの経験が、宝もの", "好きが同じ、を見つけよう", "次の一歩の、おともに"];

export function FeatureGrid() {
  return (
    <section className="home-section home-features" id="features" data-scroll-reveal>
      <div className="home-container">
        <div className="home-section-heading"><p className="home-sticker home-sticker-pink">できること、いろいろ！</p><h2>あなたの「好き」が、<br className="home-mobile-break" /><span className="home-marker">ここから広がる。</span></h2><p>ミライフは、知識や経験を分かち合うオンラインの学び合い空間。<br className="home-desktop-break" />学びたい気持ちも、教えたい気持ちも、どちらも大歓迎です。</p></div>
        <div className="home-feature-grid">{features.map((feature, index) => <article className={`home-feature-card home-feature-${index + 1}`} key={feature.title}>
          <div className="home-feature-top"><span className="home-feature-number">0{index + 1}</span><span className="home-feature-icon" aria-hidden="true"><Icon name={feature.icon} /></span></div>
          <p className="home-feature-note">{notes[index]}</p><h3>{feature.title}</h3><p className="home-feature-description">{feature.description}</p><span className="home-feature-spark" aria-hidden="true"><Icon name={(["spark-blue", "spark", "spark-pink", "spark-teal"] as const)[index]} /></span>
        </article>)}</div>
      </div>
    </section>
  );
}
