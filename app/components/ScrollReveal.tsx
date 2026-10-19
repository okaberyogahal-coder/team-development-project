"use client";

import { useEffect } from "react";

export function ScrollReveal() {
  useEffect(() => {
    const sections = Array.from(document.querySelectorAll<HTMLElement>("[data-scroll-reveal]"));
    const homePage = document.querySelector<HTMLElement>(".home-page");
    const header = homePage?.querySelector<HTMLElement>(".home-header");
    const syncHeaderHeight = () => {
      if (homePage && header) {
        homePage.style.setProperty("--home-header-height", `${header.getBoundingClientRect().height}px`);
      }
    };
    const headerObserver = typeof ResizeObserver === "undefined" ? null : new ResizeObserver(syncHeaderHeight);
    syncHeaderHeight();
    if (header) headerObserver?.observe(header);

    if (!("IntersectionObserver" in window)) {
      sections.forEach(section => section.classList.add("scroll-revealed"));
      return () => headerObserver?.disconnect();
    }

    const observer = new IntersectionObserver((entries, currentObserver) => {
      entries.forEach(entry => {
        if (!entry.isIntersecting) return;
        entry.target.classList.add("scroll-revealed");
        currentObserver.unobserve(entry.target);
      });
    }, { threshold: 0.01 });

    sections.forEach(section => {
      const bounds = section.getBoundingClientRect();
      if (bounds.top < window.innerHeight && bounds.bottom > 0) {
        section.classList.add("scroll-revealed");
      } else {
        section.classList.add("scroll-reveal-ready");
        observer.observe(section);
      }
    });

    return () => {
      observer.disconnect();
      headerObserver?.disconnect();
    };
  }, []);

  return null;
}
