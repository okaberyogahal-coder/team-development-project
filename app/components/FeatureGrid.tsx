import { features } from "../data/home";

export function FeatureGrid() {
  return (
    <section className="bg-white px-5 py-20 lg:px-8" id="features">
      <div className="mx-auto max-w-7xl">
        <div className="text-center">
          <p className="inline-flex rounded-full bg-blue-100 px-5 py-2 text-sm font-black text-blue-600">
            できること
          </p>
          <h2 className="mt-5 text-4xl font-black tracking-normal text-slate-900 sm:text-5xl">
            ミライフでできること
          </h2>
          <p className="mx-auto mt-4 max-w-2xl text-lg font-medium leading-8 text-slate-600">
            学びも、趣味も、交流も。あなたの「やってみたい」を応援します。
          </p>
        </div>

        <div className="mt-12 grid gap-6 md:grid-cols-2 lg:grid-cols-3">
          {features.map((feature) => (
            <article
              className={`${feature.color} rounded-[32px] p-7 shadow-sm ring-2 ${feature.ring} transition duration-300 hover:-translate-y-2 hover:shadow-xl`}
              key={feature.title}
            >
              <div className="flex h-16 w-16 items-center justify-center rounded-3xl bg-white text-4xl shadow-sm">
                {feature.icon}
              </div>
              <h3 className={`mt-6 text-2xl font-black ${feature.accent}`}>
                {feature.title}
              </h3>
              <p className="mt-3 text-base font-medium leading-7 text-slate-600">
                {feature.description}
              </p>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}
