function Person({
  className,
  face,
  label,
}: {
  className: string;
  face: string;
  label: string;
}) {
  return (
    <div
      className={`absolute rounded-[28px] bg-white p-4 text-center shadow-xl shadow-blue-100/70 ring-1 ring-blue-50 ${className}`}
    >
      <div className="text-5xl" role="img" aria-label={label}>
        {face}
      </div>
      <p className="mt-2 text-xs font-black text-slate-600">{label}</p>
    </div>
  );
}

export function HeroIllustration() {
  return (
    <div
      aria-label="PCとタブレットで参加するミライフ広場のイメージ"
      className="relative mx-auto min-h-[430px] max-w-[560px]"
    >
      <div className="absolute inset-x-3 bottom-0 top-5 rounded-[40px] border-[10px] border-white bg-sky-100 shadow-2xl shadow-blue-100" />
      <div className="absolute inset-x-8 bottom-8 top-12 overflow-hidden rounded-[32px] bg-gradient-to-b from-sky-100 via-white to-green-100 ring-1 ring-blue-100">
        <div className="absolute inset-x-0 top-0 h-16 bg-white/70" />
        <div className="absolute left-6 top-5 flex gap-2">
          <span className="h-3 w-3 rounded-full bg-pink-300" />
          <span className="h-3 w-3 rounded-full bg-orange-300" />
          <span className="h-3 w-3 rounded-full bg-green-300" />
        </div>

        <div className="absolute left-1/2 top-20 -translate-x-1/2 rounded-full bg-white px-6 py-3 text-lg font-black text-blue-600 shadow-md">
          ミライフ広場
        </div>

        <div className="absolute bottom-7 left-1/2 h-36 w-[78%] -translate-x-1/2 rounded-[100%] bg-green-200/80" />
        <div className="absolute bottom-24 left-1/2 h-20 w-20 -translate-x-1/2 rounded-[24px] bg-green-500 text-center text-5xl leading-[5rem] shadow-lg">
          🌳
        </div>

        <Person className="bottom-28 left-6" face="👩‍💻" label="デジタルを教える" />
        <Person className="bottom-16 right-8" face="👵" label="経験を伝える" />
        <Person className="bottom-8 left-24 hidden sm:block" face="👨‍🎓" label="一緒に学ぶ" />

        <div className="absolute right-8 top-28 rounded-[22px] bg-white px-4 py-3 text-sm font-black text-green-700 shadow-lg">
          スマホの使い方を学ぶ
        </div>
        <div className="absolute left-10 top-32 rounded-[22px] bg-white px-4 py-3 text-sm font-black text-orange-600 shadow-lg">
          仕事の経験を聞く
        </div>

        <div className="absolute bottom-4 right-20 rounded-2xl bg-blue-600 px-5 py-2 text-sm font-black text-white shadow-lg">
          PC / タブレットで参加
        </div>
      </div>
    </div>
  );
}
