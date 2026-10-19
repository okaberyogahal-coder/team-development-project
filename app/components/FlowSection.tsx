import { flowSteps } from "../data/home";

export function FlowSection() {
  return (
    <section className="home-section home-flow" id="flow" data-scroll-reveal>
      <div className="home-container">
        <div className="home-section-heading"><p className="home-sticker home-sticker-white">小さな一歩からで、大丈夫。</p><h2>やってみる。<span className="home-marker">世界がひろがる。</span></h2><p>知る、試す、話してみる。その先に、また次の「やってみたい」。</p></div>
        <ol className="home-flow-grid">{flowSteps.map((step, index) => <li key={step.label}>
          <span className="home-flow-step">STEP 0{index + 1}</span><div className="home-flow-circle" aria-hidden="true">{step.icon}</div><h3>{step.label}</h3><p>{step.detail}</p>{index < flowSteps.length - 1 && <span className="home-flow-arrow" aria-hidden="true">→</span>}
        </li>)}</ol>
      </div>
    </section>
  );
}
