/** @type {import('next').NextConfig} */
const nextConfig = {
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
