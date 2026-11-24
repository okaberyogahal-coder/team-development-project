import { networkInterfaces } from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { lanConfig } from "./scripts/lan-config.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

/** @type {import('next').NextConfig} */
const nextConfig = {
  // Avoid tracing into parent directories that contain unrelated lockfiles (e.g. OneDrive root).
  outputFileTracingRoot: __dirname,
  // Allow dev scripts on this PC's LAN addresses so forms can hydrate there.
  allowedDevOrigins: ["127.0.0.1", ...lanConfig(networkInterfaces()).urls.map(url => new URL(url).hostname)],
  webpack: (config) => {
    config.watchOptions = {
      ...config.watchOptions,
      poll: 1000,
      ignored: ["**/.npm-cache/**", "**/node_modules/**", "**/.next/**", "**/unity/**"],
    };

    return config;
  },
};

export default nextConfig;
