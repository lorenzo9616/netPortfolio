import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Output standalone build for optimal Docker image size
  output: "standalone",
};

export default nextConfig;
