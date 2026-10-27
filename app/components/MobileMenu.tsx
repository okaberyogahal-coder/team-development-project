"use client";

import { useState } from "react";
import { homeNavItems } from "./home-nav";

export function MobileMenu({ authenticated = false }: { authenticated?: boolean }) {
  const [isOpen, setIsOpen] = useState(false);

  return (
    <div className="home-mobile-menu">
      <button
        aria-controls="home-mobile-menu-panel"
        aria-expanded={isOpen}
        aria-label="メニュー"
        className="home-mobile-menu-button"
        onClick={() => setIsOpen(open => !open)}
        type="button"
      >
        <span aria-hidden="true" />
        <span aria-hidden="true" />
        <span aria-hidden="true" />
      </button>
      <nav
        aria-label="モバイルメニュー"
        className="home-mobile-menu-panel"
        hidden={!isOpen}
        id="home-mobile-menu-panel"
      >
        {homeNavItems.map(item => (
          <a href={item.href} key={item.href} onClick={() => setIsOpen(false)}>
            {item.label}
          </a>
        ))}
        <a href={authenticated ? "/account" : "/login"} onClick={() => setIsOpen(false)}>{authenticated ? "マイページ" : "ログイン"}</a>
      </nav>
    </div>
  );
}
