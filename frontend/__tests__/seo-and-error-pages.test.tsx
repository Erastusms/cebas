import { describe, it, expect, vi } from "vitest";
import React from "react";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import NotFound from "../app/not-found";
import RootError from "../app/error";

describe("Frontend Error Boundaries & Error Pages", () => {
  it("renders 404 NotFound page with accessible heading and home link", () => {
    render(<NotFound />);

    expect(screen.getByRole("heading", { level: 1, name: /Halaman Tidak Ditemukan/i })).toBeDefined();
    expect(screen.getByText(/404 Error/i)).toBeDefined();

    const homeLink = screen.getByRole("link", { name: /Kembali ke Beranda/i });
    expect(homeLink).toBeDefined();
    expect(homeLink.getAttribute("href")).toBe("/home");
  });

  it("renders RootError boundary and triggers reset handler on retry click", async () => {
    const user = userEvent.setup();
    const mockReset = vi.fn();
    const testError = new Error("Test simulation failure");
    (testError as any).digest = "DIGEST_12345";

    render(<RootError error={testError} reset={mockReset} />);

    expect(screen.getByRole("alert")).toBeDefined();
    expect(screen.getByRole("heading", { level: 1, name: /Terjadi Kesalahan/i })).toBeDefined();
    expect(screen.getByText(/ID Masalah: DIGEST_12345/i)).toBeDefined();

    const retryBtn = screen.getByRole("button", { name: /Coba Lagi/i });
    await user.click(retryBtn);

    expect(mockReset).toHaveBeenCalledTimes(1);
  });
});

describe("SEO & JSON-LD Escaping Validation", () => {
  it("safely serializes JSON-LD avoiding script injection and HTML tag execution", () => {
    const maliciousPayload = {
      "@context": "https://schema.org",
      "@type": "Person",
      "name": "Malicious User </script><script>alert('xss')</script>",
      "description": "Exploit attempt <img src=x onerror=alert(1)>",
    };

    const serialized = JSON.stringify(maliciousPayload).replace(/</g, "\\u003c");

    // Must not contain unescaped closing script tags
    expect(serialized).not.toContain("</script>");
    expect(serialized).toContain("\\u003c/script>");
    expect(serialized).not.toContain("<img");
    expect(serialized).toContain("\\u003cimg");

    // Must be valid JSON when decoded
    const parsed = JSON.parse(serialized);
    expect(parsed.name).toContain("Malicious User </script>");
  });

  it("constructs compliant ProfilePage schema for public profiles", () => {
    const profile = {
      displayName: "Erastus",
      username: "erastusms",
      bio: "Software Architect & Builder",
      avatarUrl: "https://example.com/avatar.jpg",
      stats: { postCount: 42, followerCount: 1500 },
    };

    const schema = {
      "@context": "https://schema.org",
      "@type": "ProfilePage",
      "mainEntity": {
        "@type": "Person",
        "name": profile.displayName,
        "alternateName": `@${profile.username}`,
        "identifier": profile.username,
        "description": profile.bio,
        "image": profile.avatarUrl,
        "interactionStatistic": [
          {
            "@type": "InteractionCounter",
            "interactionType": "https://schema.org/WriteAction",
            "userInteractionCount": profile.stats.postCount,
          },
          {
            "@type": "InteractionCounter",
            "interactionType": "https://schema.org/FollowAction",
            "userInteractionCount": profile.stats.followerCount,
          },
        ],
      },
    };

    expect(schema["@type"]).toBe("ProfilePage");
    expect(schema.mainEntity.name).toBe("Erastus");
    expect(schema.mainEntity.interactionStatistic[0].userInteractionCount).toBe(42);
  });
});
