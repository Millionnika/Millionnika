import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Миллионика",
  description: "Браузерная социальная RPG",
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="ru">
      <body>{children}</body>
    </html>
  );
}
