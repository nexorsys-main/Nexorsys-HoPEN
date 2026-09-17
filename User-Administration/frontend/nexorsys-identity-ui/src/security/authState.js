export function authStateFromFailure(error) {
  const status = error?.response?.status;
  if (status === 401) return "unauthorized";
  if (status === 403) return "forbidden";
  if (typeof status === "number" && status >= 500) return "error";
  if (status) return "error";
  return "unavailable";
}

export function requestFailureMessage(error, resource = "service") {
  const state = authStateFromFailure(error);
  if (state === "unauthorized") return "Session expirée. Veuillez vous reconnecter.";
  if (state === "forbidden") return "Accès refusé pour cette ressource.";
  if (state === "error") return `Le ${resource} est temporairement indisponible.`;
  return `Impossible de joindre le ${resource}. Vérifiez la connexion puis réessayez.`;
}
