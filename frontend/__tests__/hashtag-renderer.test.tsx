import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import React from "react";
import { HashtagText } from "../components/hashtags/HashtagText";

describe("HashtagText Renderer (Phase 12.1)", () => {
  it("renders plain text without hashtags unchanged", () => {
    const { container } = render(<HashtagText text="Halo semua celoteh bebas" />);
    expect(container.textContent).toBe("Halo semua celoteh bebas");
    expect(container.querySelectorAll("a")).toHaveLength(0);
  });

  it("renders single hashtag as an accessible link with canonical lowercase href", () => {
    render(<HashtagText text="Selamat datang di #CEBAS!" />);

    const link = screen.getByRole("link", { name: /Tag #CEBAS/i });
    expect(link).toBeDefined();
    expect(link.getAttribute("href")).toBe("/tag/cebas");
    expect(link.textContent).toBe("#CEBAS");
  });

  it("renders multiple distinct hashtags with appropriate routes", () => {
    render(<HashtagText text="Belajar #NextJS dan #dotNet di #CEBAS." />);

    const nextLink = screen.getByRole("link", { name: /Tag #NextJS/i });
    expect(nextLink.getAttribute("href")).toBe("/tag/nextjs");

    const dotnetLink = screen.getByRole("link", { name: /Tag #dotNet/i });
    expect(dotnetLink.getAttribute("href")).toBe("/tag/dotnet");

    const cebasLink = screen.getByRole("link", { name: /Tag #CEBAS/i });
    expect(cebasLink.getAttribute("href")).toBe("/tag/cebas");
  });

  it("handles duplicate hashtags appearing multiple times in the same post", () => {
    render(<HashtagText text="Keren #CEBAS dan sungguh #CEBAS mantap!" />);

    const links = screen.getAllByRole("link", { name: /Tag #CEBAS/i });
    expect(links).toHaveLength(2);
    expect(links[0].getAttribute("href")).toBe("/tag/cebas");
    expect(links[1].getAttribute("href")).toBe("/tag/cebas");
  });

  it("correctly identifies hashtags adjacent to punctuation", () => {
    render(<HashtagText text="Cek ini: (#topic), [#another], #end." />);

    const topicLink = screen.getByRole("link", { name: /Tag #topic/i });
    expect(topicLink.getAttribute("href")).toBe("/tag/topic");

    const anotherLink = screen.getByRole("link", { name: /Tag #another/i });
    expect(anotherLink.getAttribute("href")).toBe("/tag/another");

    const endLink = screen.getByRole("link", { name: /Tag #end/i });
    expect(endLink.getAttribute("href")).toBe("/tag/end");
  });

  it("ignores standalone # and email addresses", () => {
    const { container } = render(<HashtagText text="Email me at user#domain.com or # standalone" />);
    expect(container.querySelectorAll("a")).toHaveLength(0);
  });

  it("safely handles XSS attempts without evaluating raw HTML", () => {
    const malicious = "<script>alert('xss')</script><img src=x onerror=alert(1)> #safeTag";
    const { container } = render(<HashtagText text={malicious} />);

    // Script and img should be rendered as plain text nodes, not elements
    expect(container.querySelector("script")).toBeNull();
    expect(container.querySelector("img")).toBeNull();
    expect(container.textContent).toContain("<script>alert('xss')</script>");

    const safeLink = screen.getByRole("link", { name: /Tag #safeTag/i });
    expect(safeLink.getAttribute("href")).toBe("/tag/safetag");
  });

  it("returns null for empty or null text", () => {
    const { container: c1 } = render(<HashtagText text="" />);
    expect(c1.firstChild).toBeNull();

    const { container: c2 } = render(<HashtagText text={null} />);
    expect(c2.firstChild).toBeNull();
  });
});
