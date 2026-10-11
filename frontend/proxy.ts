import { NextRequest, NextResponse } from "next/server";

export async function proxy(request: NextRequest) {
  if (!request.cookies.has("millionnika_session")) {
    return NextResponse.redirect(new URL("/login", request.url));
  }

  if (request.nextUrl.pathname.startsWith("/admin")) {
    try {
      const backendUrl = process.env.BACKEND_URL ?? "http://localhost:5080";
      const response = await fetch(`${backendUrl}/api/auth/session`, {
        headers: { cookie: request.headers.get("cookie") ?? "" },
        cache: "no-store",
      });
      if (!response.ok) return NextResponse.redirect(new URL("/login", request.url));
      const session = (await response.json()) as { role?: string };
      if (session.role !== "Admin") return NextResponse.redirect(new URL("/profile", request.url));
    } catch {
      return NextResponse.redirect(new URL("/login", request.url));
    }
  }

  return NextResponse.next();
}

export const config = {
  matcher: ["/profile/:path*", "/contracts/:path*", "/activities/:path*", "/hacking/:path*", "/hunting/:path*", "/implant-center/:path*", "/shop/:path*", "/inventory/:path*", "/players/:path*", "/friends/:path*", "/admin/:path*"],
};
