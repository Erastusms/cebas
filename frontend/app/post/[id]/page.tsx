import type { Metadata } from "next";
import { PostDetailClient } from "../../../components/posts/PostDetailClient";

interface PostDetailPageProps {
  params: Promise<{
    id: string;
  }>;
}

export async function generateMetadata({ params }: PostDetailPageProps): Promise<Metadata> {
  const resolvedParams = await params;
  const postId = resolvedParams.id;
  const apiUrl = process.env.INTERNAL_API_URL || process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";

  try {
    const res = await fetch(`${apiUrl}/api/v1/posts/${encodeURIComponent(postId)}`, {
      next: { revalidate: 30 },
    });

    if (!res.ok) {
      return {
        title: "Postingan Tidak Ditemukan",
        description: "Postingan tidak ditemukan di CEBAS.",
        robots: { index: false, follow: false },
      };
    }

    const json = await res.json();
    const post = json.data;

    if (!post || post.isDeleted || post.isHidden) {
      return {
        title: "Postingan Tidak Tersedia",
        robots: { index: false, follow: false },
      };
    }

    const snippet =
      post.content.length > 120
        ? `${post.content.substring(0, 117)}...`
        : post.content;
    const authorName = post.author?.displayName || post.author?.username || "Pengguna";
    const authorHandle = post.author?.username ? `@${post.author.username}` : "";
    const title = `${authorName} (${authorHandle}) di CEBAS: "${snippet}"`;
    const ogImage = `/api/og/post?id=${encodeURIComponent(postId)}`;

    return {
      title,
      description: snippet,
      alternates: {
        canonical: `/post/${postId}`,
      },
      openGraph: {
        title: `${authorName} di CEBAS`,
        description: snippet,
        url: `/post/${postId}`,
        siteName: "CEBAS",
        type: "article",
        images: [
          {
            url: ogImage,
            width: 1200,
            height: 630,
            alt: `Postingan oleh ${authorName} di CEBAS`,
          },
        ],
      },
      twitter: {
        card: "summary_large_image",
        title,
        description: snippet,
        images: [ogImage],
      },
      robots: {
        index: true,
        follow: true,
      },
    };
  } catch {
    return {
      title: "Postingan | CEBAS",
      description: "Percakapan publik di CEBAS.",
    };
  }
}

export default async function PostDetailPage({ params }: PostDetailPageProps) {
  const resolvedParams = await params;
  const postId = resolvedParams.id;
  const apiUrl = process.env.INTERNAL_API_URL || process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";

  let jsonLd: object | null = null;

  try {
    const res = await fetch(`${apiUrl}/api/v1/posts/${encodeURIComponent(postId)}`, {
      next: { revalidate: 30 },
    });

    if (res.ok) {
      const json = await res.json();
      const post = json.data;

      if (post && !post.isDeleted && !post.isHidden) {
        jsonLd = {
          "@context": "https://schema.org",
          "@type": "SocialMediaPosting",
          "headline": post.content.length > 100 ? `${post.content.substring(0, 97)}...` : post.content,
          "articleBody": post.content,
          "datePublished": post.createdAt,
          "dateModified": post.updatedAt || post.createdAt,
          "author": {
            "@type": "Person",
            "name": post.author?.displayName || post.author?.username,
            "alternateName": `@${post.author?.username}`,
            ...(post.author?.avatarUrl ? { "image": post.author.avatarUrl } : {}),
          },
          "interactionStatistic": [
            {
              "@type": "InteractionCounter",
              "interactionType": "https://schema.org/LikeAction",
              "userInteractionCount": post.likeCount ?? 0,
            },
            {
              "@type": "InteractionCounter",
              "interactionType": "https://schema.org/CommentAction",
              "userInteractionCount": post.replyCount ?? 0,
            },
          ],
        };
      }
    }
  } catch {
    // Fallback for offline builds
  }

  return (
    <>
      {jsonLd && (
        <script
          type="application/ld+json"
          dangerouslySetInnerHTML={{
            __html: JSON.stringify(jsonLd).replace(/</g, "\\u003c"),
          }}
        />
      )}
      <PostDetailClient postId={postId} />
    </>
  );
}
