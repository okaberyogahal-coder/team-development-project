import { defineConfig, globalIgnores } from "eslint/config";
import nextVitals from "eslint-config-next/core-web-vitals";

export default defineConfig([
  ...nextVitals,
  // These existing exits intentionally perform a full page navigation.
  {
    files: ["app/components/AuthShell.tsx", "app/components/Footer.tsx", "app/components/SignupForm.tsx", "app/components/plaza/PlazaClient.tsx", "app/plaza/page.tsx"],
    rules: { "@next/next/no-html-link-for-pages": "warn" },
  },
  // Keep the legacy plaza's existing hook patterns visible during this dependency upgrade.
  {
    files: ["app/components/plaza/world3d/WorldCanvas.tsx", "app/hooks/usePlazaConnection.ts", "app/hooks/useRoomCall.ts"],
    rules: { "react-hooks/refs": "warn" },
  },
  {
    files: ["app/components/plaza/JoinForm.tsx", "app/hooks/useRoomCall.ts"],
    rules: { "react-hooks/set-state-in-effect": "warn" },
  },
  globalIgnores([".next/**", "generated/**", "unity/**", "public/unity/**", "test-results/**", "playwright-report/**"]),
]);
