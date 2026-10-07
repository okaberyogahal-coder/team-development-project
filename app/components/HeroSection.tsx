import { HeroIllustration } from "./HeroIllustration";

export function HeroSection() {
  return (
    <section id="about" className="overflow-hidden px-5 py-16 sm:py-20 lg:px-8">
      <div className="mx-auto grid max-w-7xl items-center gap-12 lg:grid-cols-[0.95fr_1.05fr]">
        <div>
          <p className="inline-flex rounded-full bg-orange-100 px-5 py-2 text-sm font-black text-orange-500">
            明るい未来へ、人生をアップデート
          </p>

          <h1 className="mt-6 text-5xl font-black leading-[1.12] tracking-normal text-slate-900 sm:text-6xl lg:text-7xl">
            何歳からでも、
            <br />
            <span className="text-blue-600">新しい未来</span>へ
            <br />
            挑戦できる。
          </h1>

          <p className="mt-7 max-w-2xl text-lg font-medium leading-9 text-slate-600 sm:text-xl">
            学ぶ。体験する。誰かとつながる。
            <br />
            ミライフは、世代や場所を超えて新しい自分に出会える
            人生アップデート空間です。
          </p>

          <div className="mt-9 flex flex-wrap gap-4">
            <a
              className="rounded-full bg-blue-600 px-7 py-4 text-base font-black text-white shadow-xl shadow-blue-200 transition hover:-translate-y-1 hover:bg-blue-700 focus:outline-none focus:ring-4 focus:ring-blue-200"
              href="/plaza"
            >
              ミライフをはじめる
            </a>
            <a
              className="rounded-full border-2 border-blue-100 bg-white px-7 py-4 text-base font-black text-blue-600 shadow-sm transition hover:-translate-y-1 hover:bg-sky-50 focus:outline-none focus:ring-4 focus:ring-blue-100"
              href="#community"
            >
              ミライフについて
            </a>
          </div>
        </div>

        <HeroIllustration />
      </div>
    </section>
  );
}
