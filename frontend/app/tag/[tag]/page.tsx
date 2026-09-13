import type { Metadata } from "next";
import { TagTimelineClient } from "../../../components/hashtags/TagTimelineClient";

interface TagDetailPageProps {
  params: Promise<{
    tag: string;
  }>;
}

export async function generateMetadata({ params }: TagDetailPageProps): Promise<Metadata> {
  const resolvedParams = await params;
  const rawTag = decodeURIComponent(resolvedParams.tag || "").replace(/^#/, "");
  const cleanTag = rawTag.toLowerCase();

  const title = `#${cleanTag} — Topik di CEBAS`;
  const description = `Jelajahi percakapan dan celotehan terbaru dengan tag #${cleanTag} di CEBAS.`;

  return {
    title,
    description,
    alternates: {
      canonical: `/tag/${encodeURIComponent(cleanTag)}`,
    },
    openGraph: {
      title,
      description,
      url: `/tag/${encodeURIComponent(cleanTag)}`,
      siteName: "CEBAS",
      type: "website",
    },
    twitter: {
      card: "summary",
      title,
      description,
    },
  };
}

export default async function TagPage({ params }: TagDetailPageProps) {
  const resolvedParams = await params;
  const rawTag = decodeURIComponent(resolvedParams.tag || "").replace(/^#/, "");

  return <TagTimelineClient tag={rawTag} />;
}
