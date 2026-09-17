import React, {
  createContext,
  useContext,
  useState,
  useCallback,
  useEffect,
} from "react";
import * as signalR from "@microsoft/signalr";
import api from "./api";
import { authStateFromFailure } from "./security/authState";
import { readCsrfToken } from "./security/csrf";

const AppContext = createContext();

export const AppProvider = ({ children }) => {
  const [refreshKey, setRefreshKey] = useState(0);
  const [connection, setConnection] = useState(null);
  const [lastEvent, setLastEvent] = useState(null);
  const [user, setUser] = useState(null);
  const [authChecked, setAuthChecked] = useState(false);
  const [authState, setAuthState] = useState("loading");

  useEffect(() => {
    let mounted = true;
    if (window.location.pathname.startsWith("/login")) {
      setAuthState("unauthorized");
      setAuthChecked(true);
      return () => { mounted = false; };
    }
    api.get("/auth/me")
      .then(({ data }) => {
        if (!mounted) return;
        setUser(data.user);
        setAuthState(data.user ? "authenticated" : "unauthorized");
      })
      .catch((error) => {
        if (!mounted) return;
        setUser(null);
        setAuthState(authStateFromFailure(error));
      })
      .finally(() => mounted && setAuthChecked(true));
    return () => { mounted = false; };
  }, []);

  const triggerRefresh = useCallback(() => {
    setRefreshKey((prev) => prev + 1);
  }, []);

  useEffect(() => {
    if (!authChecked || authState !== "authenticated") return undefined;
    let isMounted = true;
    const hubUrl = `${api.defaults.baseURL.replace("/api", "")}/hubs/identity`;

    const csrf = readCsrfToken();
    const newConnection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        headers: csrf
          ? { "X-CSRF-TOKEN": decodeURIComponent(csrf) }
          : undefined,
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.None)
      .build();

    const startConnection = async () => {
      try {
        await newConnection.start();
        if (isMounted) {
          setConnection(newConnection);
          newConnection.on("OnUserStatusChanged", (data) => {
            setLastEvent({
              type: "OnUserStatusChanged",
              data,
              timestamp: Date.now(),
            });
          });
          newConnection.on("OnWorkflowChanged", (data) => {
            if (isMounted) {
              setLastEvent({
                type: "OnWorkflowChanged",
                data,
                timestamp: Date.now(),
              });
              triggerRefresh();
            }
          });
        }
      } catch {
        // Connection failures are intentionally silent; no error object reaches the console.
      }
    };

    startConnection();

    return () => {
      isMounted = false;
      newConnection.stop().catch(() => {});
      setConnection(null);
    };
  }, [authChecked, authState, triggerRefresh]);

  return (
    <AppContext.Provider
      value={{ refreshKey, triggerRefresh, connection, lastEvent, user, authChecked, authState }}
    >
      {children}
    </AppContext.Provider>
  );
};

export const useApp = () => {
  const context = useContext(AppContext);
  if (!context) {
    throw new Error("useApp must be used within an AppProvider");
  }
  return context;
};
