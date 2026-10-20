import type { IconName } from "../../shared/icons";

/** Local SVG images keep illustrations identical across operating systems. */
export function Icon({ name, label, className = "" }: {
  name: IconName;
  label?: string;
  className?: string;
}) {
  // SVG files are already small and do not need raster image optimization.
  // eslint-disable-next-line @next/next/no-img-element
  return <img src={`/images/icons/${name}.svg`} width={64} height={64} alt={label ?? ""} aria-hidden={label ? undefined : true} className={`milife-icon ${className}`} draggable={false} />;
}
