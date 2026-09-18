export interface AuthUser {
  id: number
  email: string
}

export interface AuthResponse {
  user: AuthUser
  expiresAt: string
}
