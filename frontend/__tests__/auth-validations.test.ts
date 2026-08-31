import { describe, it, expect } from "vitest";
import { registerSchema, loginSchema, editProfileSchema } from "../lib/validations/auth";

describe("Frontend Zod Validation Schemas", () => {
  describe("registerSchema", () => {
    it("should accept valid registration details", () => {
      const validData = {
        username: "johndoe",
        email: "john@example.com",
        displayName: "John Doe",
        password: "Password123!",
        confirmPassword: "Password123!",
      };

      const result = registerSchema.safeParse(validData);
      expect(result.success).toBe(true);
    });

    it("should reject mismatching passwords", () => {
      const invalidData = {
        username: "johndoe",
        email: "john@example.com",
        password: "Password123!",
        confirmPassword: "DifferentPassword123!",
      };

      const result = registerSchema.safeParse(invalidData);
      expect(result.success).toBe(false);
      if (!result.success) {
        expect(result.error.issues.some((e) => (e.path as Array<string | number>).includes("confirmPassword"))).toBe(true);
      }
    });

    it("should reject usernames with invalid characters", () => {
      const invalidData = {
        username: "john doe",
        email: "john@example.com",
        password: "Password123!",
        confirmPassword: "Password123!",
      };

      const result = registerSchema.safeParse(invalidData);
      expect(result.success).toBe(false);
    });

    it("should reject short passwords", () => {
      const invalidData = {
        username: "johndoe",
        email: "john@example.com",
        password: "short",
        confirmPassword: "short",
      };

      const result = registerSchema.safeParse(invalidData);
      expect(result.success).toBe(false);
    });
  });

  describe("loginSchema", () => {
    it("should accept valid login input", () => {
      const valid = { identifier: "johndoe", password: "Password123!" };
      const result = loginSchema.safeParse(valid);
      expect(result.success).toBe(true);
    });

    it("should reject empty identifier or password", () => {
      const invalid = { identifier: "", password: "" };
      const result = loginSchema.safeParse(invalid);
      expect(result.success).toBe(false);
    });
  });

  describe("editProfileSchema", () => {
    it("should accept valid profile edits", () => {
      const valid = { displayName: "Jane Doe", bio: "Software Engineer & Designer" };
      const result = editProfileSchema.safeParse(valid);
      expect(result.success).toBe(true);
    });

    it("should reject empty display name", () => {
      const invalid = { displayName: "", bio: "Valid bio" };
      const result = editProfileSchema.safeParse(invalid);
      expect(result.success).toBe(false);
    });

    it("should reject biographies exceeding 160 characters", () => {
      const invalid = { displayName: "Valid Name", bio: "a".repeat(161) };
      const result = editProfileSchema.safeParse(invalid);
      expect(result.success).toBe(false);
    });
  });
});
