import { Logo } from "./Logo";

const navItems = [
  { label: "ミライフとは", href: "#about" },
  { label: "できること", href: "#features" },
  { label: "世代をこえて", href: "#community" },
  { label: "未来ナビ", href: "#ai-navi" },
];

export function Header() {
  return (
    <header className="home-header">
      <div className="home-header-inner">
        <Logo />
        <nav aria-label="メインメニュー">{navItems.map(item => <a href={item.href} key={item.href}>{item.label}</a>)}</nav>
        <div className="home-header-actions"><a className="home-login-link" href="/login">ログイン</a><a className="home-button home-button-blue home-button-small" href="/plaza">ワールドに入る</a></div>
      </div>
    </header>
  );
}
