/** @type {import('next').NextConfig} */
const nextConfig = {
  output: "standalone",
  async redirects() {
    return [
      {
        source: '/',
        destination: '/properties',
        permanent: true,
      },
    ];
  },
};

export default nextConfig;
