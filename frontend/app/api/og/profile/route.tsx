import { ImageResponse } from "next/og";
import { NextRequest } from "next/server";

export const runtime = "edge";

export async function GET(request: NextRequest) {
  try {
    const { searchParams } = new URL(request.url);
    const username = searchParams.get("username") || "pengguna";
    const apiUrl = process.env.INTERNAL_API_URL || process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";

    let displayName = username;
    let bio = "Percakapan publik bebas hambatan di platform CEBAS.";
    let postCount = 0;
    let followerCount = 0;

    try {
      const res = await fetch(`${apiUrl}/api/v1/users/${encodeURIComponent(username)}/profile`, {
        next: { revalidate: 60 },
      });
      if (res.ok) {
        const json = await res.json();
        const profile = json.data;
        if (profile) {
          displayName = profile.displayName || username;
          bio = profile.bio || bio;
          postCount = profile.stats?.postCount ?? 0;
          followerCount = profile.stats?.followerCount ?? 0;
        }
      }
    } catch {
      // Fallback to default copy
    }

    const truncatedBio = bio.length > 150 ? `${bio.substring(0, 147)}...` : bio;

    return new ImageResponse(
      (
        <div
          style={{
            height: "100%",
            width: "100%",
            display: "flex",
            flexDirection: "column",
            justifyContent: "space-between",
            padding: "60px 80px",
            backgroundColor: "#090d16",
            backgroundImage: "radial-gradient(circle at 10% 20%, rgba(99, 102, 241, 0.15) 0%, transparent 50%), radial-gradient(circle at 90% 80%, rgba(59, 130, 246, 0.1) 0%, transparent 50%)",
            color: "#f8fafc",
            fontFamily: "sans-serif",
          }}
        >
          {/* Top Brand Header */}
          <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
            <div style={{ display: "flex", alignItems: "center", gap: "16px" }}>
              <div
                style={{
                  width: "48px",
                  height: "48px",
                  borderRadius: "12px",
                  backgroundColor: "#6366f1",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  fontWeight: "bold",
                  fontSize: "26px",
                  color: "#ffffff",
                }}
              >
                C
              </div>
              <span style={{ fontSize: "28px", fontWeight: "bold", letterSpacing: "-0.5px" }}>
                CEBAS
              </span>
            </div>
            <div
              style={{
                backgroundColor: "rgba(99, 102, 241, 0.2)",
                border: "1px solid rgba(99, 102, 241, 0.4)",
                padding: "6px 16px",
                borderRadius: "20px",
                fontSize: "14px",
                fontWeight: "600",
                color: "#a5b4fc",
              }}
            >
              Celoteh Bebas
            </div>
          </div>

          {/* User Profile Card */}
          <div style={{ display: "flex", flexDirection: "column", gap: "16px" }}>
            <div style={{ display: "flex", alignItems: "center", gap: "24px" }}>
              <div
                style={{
                  width: "96px",
                  height: "96px",
                  borderRadius: "50%",
                  backgroundColor: "#4f46e5",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  fontSize: "42px",
                  fontWeight: "bold",
                  color: "#ffffff",
                  border: "4px solid rgba(255, 255, 255, 0.1)",
                }}
              >
                {displayName.charAt(0).toUpperCase()}
              </div>
              <div style={{ display: "flex", flexDirection: "column" }}>
                <div style={{ fontSize: "40px", fontWeight: "800", color: "#f8fafc" }}>
                  {displayName}
                </div>
                <div style={{ fontSize: "22px", color: "#94a3b8", fontWeight: "500" }}>
                  @{username}
                </div>
              </div>
            </div>

            <div style={{ fontSize: "22px", color: "#cbd5e1", lineHeight: "1.5", maxWidth: "950px" }}>
              {truncatedBio}
            </div>
          </div>

          {/* Bottom Stats Footer */}
          <div
            style={{
              display: "flex",
              alignItems: "center",
              gap: "40px",
              borderTop: "1px solid rgba(255, 255, 255, 0.1)",
              paddingTop: "24px",
              fontSize: "18px",
              color: "#94a3b8",
            }}
          >
            <div style={{ display: "flex", gap: "8px" }}>
              <span style={{ fontWeight: "bold", color: "#f8fafc" }}>{postCount}</span>
              <span>Postingan</span>
            </div>
            <div style={{ display: "flex", gap: "8px" }}>
              <span style={{ fontWeight: "bold", color: "#f8fafc" }}>{followerCount}</span>
              <span>Pengikut</span>
            </div>
            <div style={{ marginLeft: "auto", fontSize: "16px", color: "#64748b" }}>
              cebas.social
            </div>
          </div>
        </div>
      ),
      {
        width: 1200,
        height: 630,
      }
    );
  } catch {
    return new Response("Error generating OpenGraph image", { status: 500 });
  }
}
