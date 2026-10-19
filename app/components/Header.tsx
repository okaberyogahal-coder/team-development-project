import { Logo } from "./Logo";
import { MobileMenu } from "./MobileMenu";
import { homeNavItems } from "./home-nav";

export function Header() {
  return (
    <header className="home-header">
      <div className="home-header-inner">
        <Logo priority />
        <nav aria-label="メインメニュー">{homeNavItems.map(item => <a href={item.href} key={item.href}>{item.label}</a>)}</nav>
        <div className="home-header-actions">
          <a className="home-login-link" href="/login">ログイン</a>
          <a className="home-button home-button-blue home-button-small" href="/plaza">ワールドに入る</a>
          <MobileMenu />
        </div>
      </div>
    </header>
  );
}
