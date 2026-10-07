import { Logo } from "./Logo";

export function Footer() {
  return (
    <footer className="border-t border-blue-100 bg-white px-5 py-10 lg:px-8">
      <div className="mx-auto flex max-w-7xl flex-col justify-between gap-5 md:flex-row md:items-center">
        <Logo />
        <p className="text-lg font-black text-blue-600">
          何歳からでも、新しい未来へ。
        </p>
      </div>
    </footer>
  );
}
