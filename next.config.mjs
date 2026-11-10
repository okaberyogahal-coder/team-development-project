import { networkInterfaces } from "node:os";
import { lanConfig } from "./scripts/lan-config.mjs";

/** @type {import('next').NextConfig} */
const nextConfig = {
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
