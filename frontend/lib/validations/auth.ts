import { z } from "zod";

export const registerSchema = z
  .object({
    username: z
      .string()
      .min(3, "Username must be at least 3 characters.")
      .max(30, "Username cannot exceed 30 characters.")
      .regex(/^[a-zA-Z0-9_]+$/, "Username may only contain letters, numbers, and underscores."),
    email: z
      .string()
      .min(1, "Email address is required.")
      .max(255, "Email cannot exceed 255 characters.")
      .email("Please enter a valid email address."),
    displayName: z
      .string()
      .max(50, "Display name cannot exceed 50 characters.")
      .optional()
      .or(z.literal("")),
    password: z
      .string()
      .min(8, "Password must be at least 8 characters.")
      .max(128, "Password cannot exceed 128 characters."),
    confirmPassword: z.string().min(1, "Please confirm your password."),
  })
  .refine((data) => data.password === data.confirmPassword, {
    message: "Passwords do not match.",
    path: ["confirmPassword"],
  });

export type RegisterFormData = z.infer<typeof registerSchema>;

export const loginSchema = z.object({
  identifier: z.string().min(1, "Username or email is required."),
  password: z.string().min(1, "Password is required."),
});

export type LoginFormData = z.infer<typeof loginSchema>;

export const editProfileSchema = z.object({
  displayName: z
    .string()
    .min(1, "Display name is required.")
    .max(50, "Display name cannot exceed 50 characters."),
  bio: z
    .string()
    .max(160, "Biography cannot exceed 160 characters.")
    .optional()
    .or(z.literal("")),
});

export type EditProfileFormData = z.infer<typeof editProfileSchema>;
