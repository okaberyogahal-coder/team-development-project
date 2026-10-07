import { Logo } from "./Logo";

const navItems = [
  { label: "ミライフとは", href: "#about" },
  { label: "できること", href: "#features" },
  { label: "交流", href: "#community" },
  { label: "AI未来ナビ", href: "#ai-navi" },
];

export function Header() {
  return (
    <header className="sticky top-0 z-50 border-b border-blue-100/70 bg-white/90 backdrop-blur-md">
      <div className="mx-auto flex max-w-7xl items-center justify-between gap-4 px-5 py-4 lg:px-8">
        <Logo />

        <nav
          aria-label="メインメニュー"
          className="hidden items-center gap-7 text-sm font-bold text-slate-600 lg:flex"
        >
          {navItems.map((item) => (
            <a
              className="rounded-full px-1 py-2 transition hover:text-blue-600 focus:outline-none focus:ring-2 focus:ring-blue-200"
              href={item.href}
              key={item.href}
            >
              {item.label}
            </a>
          ))}
        </nav>

        <a
          className="rounded-full bg-blue-600 px-4 py-3 text-sm font-black text-white shadow-lg shadow-blue-200 transition hover:-translate-y-0.5 hover:bg-blue-700 focus:outline-none focus:ring-4 focus:ring-blue-200 sm:px-6"
          href="/plaza"
        >
          ミライフを体験
        </a>
      </div>
    </header>
  );
}
