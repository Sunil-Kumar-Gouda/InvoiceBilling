import { http } from "./http";

export type AuthResponse = {
  accessToken: string;
  expiresAtUtc: string;
  userId: string;
  email: string;
  displayName?: string | null;
  roles: string[];
};

export async function login(email: string, password: string): Promise<AuthResponse> {
  return http<AuthResponse>("/api/auth/login", {
    method: "POST",
    body: JSON.stringify({ email, password }),
  });
}

export async function register(
  email: string,
  password: string,
  displayName?: string
): Promise<AuthResponse> {
  return http<AuthResponse>("/api/auth/register", {
    method: "POST",
    body: JSON.stringify({ email, password, displayName }),
  });
}
