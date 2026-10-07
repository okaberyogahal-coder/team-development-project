export function CommunitySection() {
  return (
    <section className="bg-orange-50 px-5 py-20 lg:px-8" id="community">
      <div className="mx-auto grid max-w-7xl items-center gap-10 lg:grid-cols-[0.9fr_1.1fr]">
        <div>
          <p className="inline-flex rounded-full bg-white px-5 py-2 text-sm font-black text-orange-500 shadow-sm">
            世代交流
          </p>
          <h2 className="mt-5 text-4xl font-black leading-tight tracking-normal text-slate-900 sm:text-5xl">
            教える人と、
            <br />
            教わる人を決めない。
          </h2>
          <p className="mt-6 max-w-xl text-lg font-medium leading-9 text-slate-600">
            若い世代からデジタル技術を学んだり、シニア世代から仕事や人生の経験を学んだり。
            誰もが先生にも、生徒にもなれる場所です。
          </p>
        </div>

        <div className="grid gap-5 md:grid-cols-2">
          <article className="rounded-[34px] bg-white p-7 shadow-xl shadow-orange-100 ring-1 ring-orange-100 transition hover:-translate-y-1">
            <div className="flex items-center justify-between gap-4">
              <div>
                <p className="text-sm font-black text-blue-600">学生・若者</p>
                <h3 className="mt-2 text-2xl font-black text-slate-900">
                  シニアの経験から学ぶ
                </h3>
              </div>
              <div className="text-6xl">👨‍🎓</div>
            </div>
            <p className="mt-5 rounded-3xl bg-blue-50 p-5 font-bold leading-7 text-slate-600">
              仕事の話、人生の選び方、趣味の深め方を聞いて、新しい視点を見つけます。
            </p>
          </article>

          <article className="rounded-[34px] bg-white p-7 shadow-xl shadow-orange-100 ring-1 ring-orange-100 transition hover:-translate-y-1">
            <div className="flex items-center justify-between gap-4">
              <div>
                <p className="text-sm font-black text-green-600">シニア</p>
                <h3 className="mt-2 text-2xl font-black text-slate-900">
                  若い世代からデジタルを学ぶ
                </h3>
              </div>
              <div className="text-6xl">👵</div>
            </div>
            <p className="mt-5 rounded-3xl bg-green-50 p-5 font-bold leading-7 text-slate-600">
              AI、スマートフォン、写真編集など、生活が楽しくなる技術に挑戦します。
            </p>
          </article>
        </div>
      </div>
    </section>
  );
}
