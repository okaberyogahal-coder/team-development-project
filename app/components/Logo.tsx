export function Logo({ href = "/" }: { href?: string }) {
  return (
    <a aria-label="ミライフ トップへ" className="flex items-end gap-2" href={href}>
      <span className="text-3xl font-black tracking-normal sm:text-4xl">
        <span className="text-blue-600">ミ</span>
        <span className="text-sky-500">ラ</span>
        <span className="text-rose-500">イ</span>
        <span className="text-yellow-500">フ</span>
      </span>
      <span className="pb-1 text-[11px] font-black leading-tight text-blue-600 sm:text-xs">
        ME
        <br />
        LIFE
      </span>
    </a>
  );
}
