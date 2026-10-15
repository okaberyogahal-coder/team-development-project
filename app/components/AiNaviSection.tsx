import { aiSuggestions } from "../data/home";

export function AiNaviSection() {
  return (
    <section className="home-section home-navi" id="ai-navi">
      <div className="home-container home-navi-board">
        <div className="home-navi-copy"><p className="home-sticker">迷ったときは、いっしょに考えよう。</p><div className="home-navi-character" aria-hidden="true">✦<span>☺</span>✦</div><h2>次の「やってみたい」を、<br /><span className="home-marker">未来ナビと。</span></h2><p>興味のあること、得意なこと、これからの目標。<br />あなたらしい一歩を見つける、AIの案内役です。</p><p className="home-navi-caption">たとえば、こんな挑戦をご提案。</p></div>
        <div className="home-navi-conversation"><div className="home-navi-message"><p className="home-eyebrow">AI 未来ナビ</p><p>あなたの「好き」から、<br /><strong>新しい可能性が見つかるかも！</strong></p></div><ul>{aiSuggestions.map((suggestion, index) => <li key={suggestion}><span aria-hidden="true">{["🌏", "📷", "💻", "🌱"][index]}</span><p>{suggestion}</p><span className="home-navi-dot" aria-hidden="true">✦</span></li>)}</ul><p className="home-navi-example">※ おすすめ内容の表示例です。</p></div>
      </div>
    </section>
  );
}
