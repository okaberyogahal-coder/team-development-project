import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  icons: { icon: "/images/milife-logo.png" },
  title: "ミライフ | ME LIFE",
  description:
    "何歳からでも、新しい未来へ挑戦できる人生アップデート空間。",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="ja">
      <body>{children}</body>
    </html>
  );
}
