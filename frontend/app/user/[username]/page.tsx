import type { Metadata } from "next";
import { UserProfileClient } from "../../../components/profile/UserProfileClient";

interface ProfilePageProps {
  params: Promise<{
    username: string;
  }>;
}

export async function generateMetadata({ params }: ProfilePageProps): Promise<Metadata> {
  const resolvedParams = await params;
  const rawUsername = resolvedParams.username;
  const username = decodeURIComponent(rawUsername);
  const apiUrl = process.env.INTERNAL_API_URL || process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";

  try {
    const res = await fetch(`${apiUrl}/api/v1/users/${encodeURIComponent(username)}/profile`, {
      next: { revalidate: 60 },
    });

    if (!res.ok) {
      return {
        title: "Pengguna Tidak Ditemukan",
        description: `Akun @${username} tidak ditemukan di CEBAS.`,
        robots: { index: false, follow: false },
      };
    }

    const json = await res.json();
    const profile = json.data;

    if (!profile || profile.isSuspended) {
      return {
        title: "Akun Ditangguhkan",
        robots: { index: false, follow: false },
      };
    }

    const title = `${profile.displayName} (@${profile.username})`;
    const description =
      profile.bio || `Lihat profil, postingan, dan linimasa @${profile.username} di CEBAS.`;
    const ogImage = `/api/og/profile?username=${encodeURIComponent(username)}`;

    return {
      title,
      description,
      alternates: {
        canonical: `/user/${encodeURIComponent(username)}`,
      },
      openGraph: {
        title: `${profile.displayName} (@${profile.username}) — CEBAS`,
        description,
        url: `/user/${encodeURIComponent(username)}`,
        siteName: "CEBAS",
        type: "profile",
        images: [
          {
            url: ogImage,
            width: 1200,
            height: 630,
            alt: `${profile.displayName} on CEBAS`,
          },
        ],
      },
      twitter: {
        card: "summary_large_image",
        title: `${profile.displayName} (@${profile.username})`,
        description,
        images: [ogImage],
      },
      robots: {
        index: true,
        follow: true,
      },
    };
  } catch {
    return {
      title: `@${username} | CEBAS`,
      description: `Lihat profil @${username} di platform CEBAS.`,
    };
  }
}

export default async function UserProfilePage({ params }: ProfilePageProps) {
  const resolvedParams = await params;
  const rawUsername = resolvedParams.username;
  const username = decodeURIComponent(rawUsername);
  const apiUrl = process.env.INTERNAL_API_URL || process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";

  let jsonLd: object | null = null;

  try {
    const res = await fetch(`${apiUrl}/api/v1/users/${encodeURIComponent(username)}/profile`, {
      next: { revalidate: 60 },
    });

    if (res.ok) {
      const json = await res.json();
      const profile = json.data;

      if (profile && !profile.isSuspended) {
        jsonLd = {
          "@context": "https://schema.org",
          "@type": "ProfilePage",
          "mainEntity": {
            "@type": "Person",
            "name": profile.displayName,
            "alternateName": `@${profile.username}`,
            "identifier": profile.username,
            "description": profile.bio || "",
            ...(profile.avatarUrl ? { "image": profile.avatarUrl } : {}),
            "interactionStatistic": [
              {
                "@type": "InteractionCounter",
                "interactionType": "https://schema.org/WriteAction",
                "userInteractionCount": profile.stats?.postCount ?? 0,
              },
              {
                "@type": "InteractionCounter",
                "interactionType": "https://schema.org/FollowAction",
                "userInteractionCount": profile.stats?.followerCount ?? 0,
              },
            ],
          },
        };
      }
    }
  } catch {
    // Graceful fallback during offline builds
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
      <UserProfileClient username={username} />
    </>
  );
}