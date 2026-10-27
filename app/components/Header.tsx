import { Logo } from "./Logo";
import { MobileMenu } from "./MobileMenu";
import { homeNavItems } from "./home-nav";
import { getCurrentUser } from "../../server/auth/server";

export async function Header() {
  const user = await getCurrentUser();
  return (
    <header className="home-header">
      <div className="home-header-inner">
        <Logo priority />
        <nav aria-label="メインメニュー">{homeNavItems.map(item => <a href={item.href} key={item.href}>{item.label}</a>)}</nav>
        <div className="home-header-actions">
          <a className="home-login-link" href={user ? "/account" : "/login"}>{user ? "マイページ" : "ログイン"}</a>
          <a className="home-button home-button-blue home-button-small" href="/plaza">ワールドに入る</a>
          <MobileMenu authenticated={Boolean(user)} />
        </div>
      </div>
    </header>
  );
}
