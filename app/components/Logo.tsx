import Image from "next/image";

export function Logo({ href = "/", priority = false }: { href?: string; priority?: boolean }) {
  return (
    <a aria-label="ミライフ トップへ" className="home-brand-logo" href={href}>
      <Image
        className="home-brand-logo-image"
        src="/images/milife-logo-matte.png"
        alt=""
        aria-hidden="true"
        width={2172}
        height={724}
        sizes="(min-width: 1320px) 190px, (min-width: 541px) 14vw, 145px"
        priority={priority}
      />
    </a>
  );
}
