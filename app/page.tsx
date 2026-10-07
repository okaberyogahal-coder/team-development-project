import { AiNaviSection } from "./components/AiNaviSection";
import { CommunitySection } from "./components/CommunitySection";
import { FeatureGrid } from "./components/FeatureGrid";
import { FlowSection } from "./components/FlowSection";
import { Footer } from "./components/Footer";
import { Header } from "./components/Header";
import { HeroSection } from "./components/HeroSection";

export default function Home() {
  return (
    <main className="min-h-screen bg-[#f7fcff] text-slate-800">
      <Header />
      <HeroSection />
      <FeatureGrid />
      <FlowSection />
      <CommunitySection />
      <AiNaviSection />
      <Footer />
    </main>
  );
}
