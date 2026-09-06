import { ImageResponse } from "next/og";
import { NextRequest } from "next/server";

export const runtime = "edge";

export async function GET(request: NextRequest) {
  try {
    const { searchParams } = new URL(request.url);
    const postId = searchParams.get("id");
    const apiUrl = process.env.INTERNAL_API_URL || process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";

    let authorName = "Pengguna CEBAS";
    let authorHandle = "cebas_user";
    let content = "Percakapan publik di platform CEBAS.";
    let likeCount = 0;
    let replyCount = 0;

    if (postId) {
      try {
        const res = await fetch(`${apiUrl}/api/v1/posts/${encodeURIComponent(postId)}`, {
          next: { revalidate: 30 },
        });
        if (res.ok) {
          const json = await res.json();
          const post = json.data;
          if (post && !post.isDeleted && !post.isHidden) {
            authorName = post.author?.displayName || post.author?.username || authorName;
            authorHandle = post.author?.username || authorHandle;
            content = post.content || content;
            likeCount = post.likeCount ?? 0;
            replyCount = post.replyCount ?? 0;
          }
        }
      } catch {
        // Fallback
      }
    }

    const truncatedContent = content.length > 200 ? `${content.substring(0, 197)}...` : content;

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
              Postingan Publik
            </div>
          </div>

          {/* Post Content */}
          <div style={{ display: "flex", flexDirection: "column", gap: "24px" }}>
            {/* Author */}
            <div style={{ display: "flex", alignItems: "center", gap: "16px" }}>
              <div
                style={{
                  width: "64px",
                  height: "64px",
                  borderRadius: "50%",
                  backgroundColor: "#4f46e5",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  fontSize: "28px",
                  fontWeight: "bold",
                  color: "#ffffff",
                }}
              >
                {authorName.charAt(0).toUpperCase()}
              </div>
              <div style={{ display: "flex", flexDirection: "column" }}>
                <div style={{ fontSize: "26px", fontWeight: "bold", color: "#f8fafc" }}>
                  {authorName}
                </div>
                <div style={{ fontSize: "18px", color: "#94a3b8" }}>
                  @{authorHandle}
                </div>
              </div>
            </div>

            {/* Post Text */}
            <div
              style={{
                fontSize: "28px",
                fontWeight: "500",
                color: "#f1f5f9",
                lineHeight: "1.4",
                maxWidth: "1000px",
              }}
            >
              &ldquo;{truncatedContent}&rdquo;
            </div>
          </div>

          {/* Bottom Engagement Footer */}
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
              <span style={{ fontWeight: "bold", color: "#f8fafc" }}>{likeCount}</span>
              <span>Suka</span>
            </div>
            <div style={{ display: "flex", gap: "8px" }}>
              <span style={{ fontWeight: "bold", color: "#f8fafc" }}>{replyCount}</span>
              <span>Balasan</span>
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
