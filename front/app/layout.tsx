import type { Metadata, Viewport } from "next";
import { Geist } from "next/font/google";
import "./globals.css";
import { AuthProvider } from "@/lib/auth-context";
import Navbar from "@/components/Navbar";

const geist = Geist({ subsets: ["latin"], variable: "--font-geist" });

export const metadata: Metadata = {
  title: "CMS Blog",
  description: "Gerenciador de conteúdo",
};

export const viewport: Viewport = {
  width: "device-width",
  initialScale: 1,
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="pt-BR" className={geist.variable}>
      <body className="bg-gray-50 text-gray-900 min-h-screen font-sans antialiased">
        <AuthProvider>
          <Navbar />
          <main className="max-w-4xl mx-auto px-4 py-6 sm:py-8">{children}</main>
        </AuthProvider>
      </body>
    </html>
  );
}
