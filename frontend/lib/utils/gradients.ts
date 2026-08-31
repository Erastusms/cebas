export interface GradientPreset {
  id: string;
  name: string;
  gradient: string;
  previewClass: string;
}

export const BANNER_GRADIENTS: GradientPreset[] = [
  {
    id: "gradient-indigo-purple",
    name: "Indigo Purple",
    gradient: "linear-gradient(135deg, #4f46e5 0%, #3b82f6 50%, #7c3aed 100%)",
    previewClass: "from-indigo-600 via-blue-500 to-purple-600",
  },
  {
    id: "gradient-sunset-rose",
    name: "Sunset Rose",
    gradient: "linear-gradient(135deg, #f43f5e 0%, #ec4899 50%, #f97316 100%)",
    previewClass: "from-rose-500 via-pink-500 to-orange-500",
  },
  {
    id: "gradient-ocean-cyan",
    name: "Ocean Cyan",
    gradient: "linear-gradient(135deg, #06b6d4 0%, #0d9488 50%, #1d4ed8 100%)",
    previewClass: "from-cyan-500 via-teal-600 to-blue-700",
  },
  {
    id: "gradient-emerald-forest",
    name: "Emerald Forest",
    gradient: "linear-gradient(135deg, #059669 0%, #16a34a 50%, #0f766e 100%)",
    previewClass: "from-emerald-600 via-green-600 to-teal-700",
  },
  {
    id: "gradient-velvet-magenta",
    name: "Velvet Magenta",
    gradient: "linear-gradient(135deg, #7e22ce 0%, #c026d3 50%, #e11d48 100%)",
    previewClass: "from-purple-700 via-fuchsia-600 to-pink-600",
  },
  {
    id: "gradient-amber-blaze",
    name: "Amber Blaze",
    gradient: "linear-gradient(135deg, #ea580c 0%, #d97706 50%, #dc2626 100%)",
    previewClass: "from-orange-600 via-amber-600 to-red-600",
  },
  {
    id: "gradient-cyber-neon",
    name: "Cyber Neon",
    gradient: "linear-gradient(135deg, #c026d3 0%, #9333ea 50%, #06b6d4 100%)",
    previewClass: "from-fuchsia-600 via-purple-700 to-cyan-500",
  },
  {
    id: "gradient-midnight-slate",
    name: "Midnight Slate",
    gradient: "linear-gradient(135deg, #1e293b 0%, #0f172a 50%, #334155 100%)",
    previewClass: "from-slate-800 via-slate-900 to-zinc-800",
  },
];

/**
 * Deterministically computes a gradient for any seed (e.g. username or userId)
 * so that users without a custom banner always get a consistent, vibrant gradient.
 */
export function getDeterministicBannerGradient(seed: string): string {
  if (!seed) return BANNER_GRADIENTS[0].gradient;

  let hash = 0;
  for (let i = 0; i < seed.length; i++) {
    hash = (hash << 5) - hash + seed.charCodeAt(i);
    hash |= 0; // Convert to 32bit integer
  }

  const index = Math.abs(hash) % BANNER_GRADIENTS.length;
  return BANNER_GRADIENTS[index].gradient;
}

/**
 * Resolves the CSS background value for a given banner string.
 * If it's a URL (starts with http or /), returns `url(...)`.
 * If it's a gradient ID or CSS gradient, returns the gradient.
 * Otherwise falls back to deterministic gradient from seed.
 */
export function resolveBannerStyle(bannerUrl?: string | null, fallbackSeed?: string): React.CSSProperties {
  if (bannerUrl) {
    // Check if it matches one of our preset IDs
    const matchedPreset = BANNER_GRADIENTS.find((p) => p.id === bannerUrl);
    if (matchedPreset) {
      return { background: matchedPreset.gradient };
    }

    // Check if it is a raw CSS gradient string
    if (bannerUrl.startsWith("linear-gradient") || bannerUrl.startsWith("radial-gradient")) {
      return { background: bannerUrl };
    }

    // Image URL
    return {
      backgroundImage: `url(${bannerUrl})`,
      backgroundSize: "cover",
      backgroundPosition: "center",
    };
  }

  // Fallback to deterministic gradient
  return {
    background: getDeterministicBannerGradient(fallbackSeed || "default"),
  };
}
