export function readCsrfToken(cookieString = document.cookie) {
  return cookieString
    .split(";")
    .map((part) => part.trim())
    .find((part) => part.startsWith("nexorsys_csrf="))
    ?.split("=")
    .slice(1)
    .join("=") || null;
}

