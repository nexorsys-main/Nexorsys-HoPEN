import { render, screen } from "@testing-library/react";
import Vault from "./Vault";
import { describe, it, expect, vi } from "vitest";
import "@testing-library/jest-dom";

// Mock matchMedia
Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: vi.fn().mockImplementation(query => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: vi.fn(),
    removeListener: vi.fn(),
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn(),
  })),
});

describe("Vault UI Component", () => {
  it("renders Vault status correctly", () => {
    render(<Vault />);
    expect(screen.getByText(/Coffre-fort protégé/i)).toBeInTheDocument();
  });

  it("displays explicit unavailability for missing backend operations", () => {
    render(<Vault />);
    expect(screen.getByText(/Secure credential listing is currently unavailable/i)).toBeInTheDocument();
  });

  it("ensures credential release disabled behavior is truthful", () => {
    render(<Vault />);
    expect(screen.getByText(/Secure credential release is currently unavailable/i)).toBeInTheDocument();
  });
});
