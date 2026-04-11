export const TOKEN_COOKIE = 'ocr_token';

/** Read the JWT from the browser cookie store. Returns null if not present. */
export function getToken(): string | null {
  if (typeof document === 'undefined') return null;
  const match = document.cookie
    .split('; ')
    .find((row) => row.startsWith(`${TOKEN_COOKIE}=`));
  return match ? match.split('=')[1] : null;
}

/** Store the JWT as a browser cookie valid for the given number of hours. */
export function setToken(token: string, expiryHours = 24): void {
  const expires = new Date(Date.now() + expiryHours * 60 * 60 * 1000).toUTCString();
  document.cookie = `${TOKEN_COOKIE}=${token}; path=/; SameSite=Lax; expires=${expires}`;
}

/** Clear the JWT cookie (sign out). */
export function clearToken(): void {
  document.cookie = `${TOKEN_COOKIE}=; path=/; SameSite=Lax; max-age=0`;
}
