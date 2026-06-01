/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  theme: {
    extend: {
      colors: {
        panel: "#151521",
        panel2: "#1a1a28",
        edge: "#2a2a3c",
        accent: "#2dd4bf",
        muted: "#5a5a72",
      },
    },
  },
  plugins: [],
};
