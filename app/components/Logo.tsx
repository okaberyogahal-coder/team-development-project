export function Logo({ href = "#" }: { href?: string }) {
  return (
    <a aria-label="ミライフ トップへ" className="flex items-end gap-2" href={href}>
      <span className="text-3xl font-black tracking-normal sm:text-4xl">
        <span className="text-blue-600">ミ</span>
        <span className="text-green-500">ラ</span>
        <span className="text-orange-400">イ</span>
        <span className="text-pink-500">フ</span>
      </span>
      <span className="pb-1 text-[11px] font-black leading-tight text-blue-600 sm:text-xs">
        MIRAI
        <br />
        LIFE
      </span>
    </a>
  );
}
