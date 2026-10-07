import { aiSuggestions } from "../data/home";

export function AiNaviSection() {
  return (
    <section className="px-5 py-20 lg:px-8" id="ai-navi">
      <div className="mx-auto grid max-w-7xl items-center gap-10 overflow-hidden rounded-[40px] bg-violet-50 p-7 shadow-xl shadow-violet-100 ring-1 ring-violet-100 sm:p-10 lg:grid-cols-[0.85fr_1.15fr] lg:p-14">
        <div>
          <div className="inline-flex h-24 w-24 items-center justify-center rounded-[32px] bg-white text-6xl shadow-md">
            ✨
          </div>
          <h2 className="mt-6 text-4xl font-black tracking-normal text-violet-700 sm:text-5xl">
            AI未来ナビ
          </h2>
          <p className="mt-5 max-w-xl text-lg font-medium leading-9 text-slate-600">
            あなたの興味、経験、得意なこと、目標から、AIが次に挑戦できそうなことを提案します。
          </p>
        </div>

        <div className="rounded-[34px] bg-white p-6 shadow-xl ring-1 ring-violet-100 sm:p-8">
          <div className="flex items-center gap-4">
            <div className="flex h-14 w-14 items-center justify-center rounded-2xl bg-violet-100 text-3xl">
              😊
            </div>
            <div>
              <p className="text-sm font-black text-violet-500">案内役から</p>
              <p className="text-xl font-black text-slate-800">
                あなたへのおすすめ
              </p>
            </div>
          </div>

          <div className="mt-6 grid gap-3 sm:grid-cols-2">
            {aiSuggestions.map((suggestion) => (
              <button
                className="rounded-[24px] bg-slate-50 p-5 text-left font-black leading-7 text-slate-700 transition hover:-translate-y-1 hover:bg-violet-50 hover:text-violet-700 focus:outline-none focus:ring-4 focus:ring-violet-100"
                key={suggestion}
                type="button"
              >
                <span className="mr-2 text-violet-500">●</span>
                {suggestion}
              </button>
            ))}
          </div>
        </div>
      </div>
    </section>
  );
}
