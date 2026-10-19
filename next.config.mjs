/** @type {import('next').NextConfig} */
const nextConfig = {
  webpack: (config) => {
    config.watchOptions = {
      ...config.watchOptions,
      ignored: ["**/.npm-cache/**", "**/node_modules/**", "**/.next/**", "**/unity/**"],
    };

    return config;
  },
};

export default nextConfig;
