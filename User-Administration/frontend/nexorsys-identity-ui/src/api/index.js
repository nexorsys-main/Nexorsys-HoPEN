import axios from "axios";
import { readCsrfToken } from "../security/csrf";

const api = axios.create({
  baseURL:
    import.meta.env.VITE_API_BASE_URL ||
    `${window.location.origin}/api`,
  withCredentials: true,
  headers: {
    "Content-Type": "application/json",
    ...(import.meta.env.VITE_ORGANIZATION_ID
      ? { "X-Organization-Id": import.meta.env.VITE_ORGANIZATION_ID }
      : {}),
  },
});

api.interceptors.request.use((config) => {
  const csrf = readCsrfToken();
  if (csrf) config.headers["X-CSRF-TOKEN"] = decodeURIComponent(csrf);
  return config;
});

// Response interceptor for handling 401 Unauthorized
api.interceptors.response.use(
  (response) => {
    return response;
  },
  (error) => {
    if (error.response && error.response.status === 401) {
      // Redirect to login if not already there
      if (!window.location.pathname.startsWith("/login")) {
        window.location.href = "/login";
      }
    }
    return Promise.reject(error);
  },
);

export default api;
