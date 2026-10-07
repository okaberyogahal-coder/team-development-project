import { flowSteps } from "../data/home";

export function FlowSection() {
  return (
    <section className="px-5 py-20 lg:px-8">
      <div className="mx-auto max-w-7xl">
        <div className="rounded-[36px] bg-white p-7 shadow-xl shadow-blue-100/70 ring-1 ring-blue-50 sm:p-10 lg:p-12">
          <div className="text-center">
            <h2 className="text-4xl font-black tracking-normal text-slate-900 sm:text-5xl">
              学ぶ → 体験する → 交流する → 次の挑戦へ
            </h2>
            <p className="mx-auto mt-4 max-w-2xl text-lg font-medium leading-8 text-slate-600">
              小さな一歩が、次の未来につながっていく流れをつくります。
            </p>
          </div>

          <div className="mt-12 grid gap-4 md:grid-cols-4">
            {flowSteps.map((step, index) => (
              <div className="relative" key={step.label}>
                <div
                  className={`${step.color} min-h-44 rounded-[30px] p-6 text-center text-white shadow-lg transition hover:-translate-y-1`}
                >
                  <div className="text-5xl">{step.icon}</div>
                  <p className="mt-4 text-2xl font-black">{step.label}</p>
                  <p className="mt-2 text-sm font-bold text-white/90">
                    {step.detail}
                  </p>
                </div>
                {index < flowSteps.length - 1 ? (
                  <div className="absolute -right-4 top-1/2 z-10 hidden h-9 w-9 -translate-y-1/2 items-center justify-center rounded-full bg-white text-2xl font-black text-blue-600 shadow-md md:flex">
                    →
                  </div>
                ) : null}
              </div>
            ))}
          </div>
        </div>
      </div>
    </section>
  );
}
