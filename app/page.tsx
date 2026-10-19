import { AiNaviSection } from "./components/AiNaviSection";
import { CommunitySection } from "./components/CommunitySection";
import { FeatureGrid } from "./components/FeatureGrid";
import { FlowSection } from "./components/FlowSection";
import { Footer } from "./components/Footer";
import { Header } from "./components/Header";
import { HeroSection } from "./components/HeroSection";
import { ScrollReveal } from "./components/ScrollReveal";

import "./home.css";

export default function Home() {
  return (
    <main className="home-page">
      <Header />
      <HeroSection />
      <FeatureGrid />
      <FlowSection />
      <CommunitySection />
      <AiNaviSection />
      <Footer />
      <ScrollReveal />
    </main>
  );
}
