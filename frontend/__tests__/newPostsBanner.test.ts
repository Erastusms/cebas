import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import React from "react";
import { NewPostsBanner } from "../components/posts/NewPostsBanner";

describe("NewPostsBanner component", () => {
  it("should not render when count is 0", () => {
    const { container } = render(React.createElement(NewPostsBanner, { count: 0, onClick: vi.fn() }));
    expect(container.firstChild).toBeNull();
  });

  it("should not render when count is negative", () => {
    const { container } = render(React.createElement(NewPostsBanner, { count: -1, onClick: vi.fn() }));
    expect(container.firstChild).toBeNull();
  });

  it("should render message with count when count is greater than 0", () => {
    render(React.createElement(NewPostsBanner, { count: 5, onClick: vi.fn() }));

    expect(screen.getByText(/Ada 5 celotehan baru/i)).toBeDefined();
  });

  it("should invoke onClick when clicked", () => {
    const handleClick = vi.fn();
    render(React.createElement(NewPostsBanner, { count: 3, onClick: handleClick }));

    const button = screen.getByRole("button");
    fireEvent.click(button);

    expect(handleClick).toHaveBeenCalledTimes(1);
  });
});
