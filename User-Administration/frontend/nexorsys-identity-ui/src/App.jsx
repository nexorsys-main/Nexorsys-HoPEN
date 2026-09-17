import React, { lazy, Suspense, useEffect } from "react";
import {
  BrowserRouter as Router,
  Routes,
  Route,
  Navigate,
} from "react-router-dom";
const Layout = lazy(() => import("./components/Layout"));
const Dashboard = lazy(() => import("./app/dashboard/Dashboard"));
const UserDirectory = lazy(() => import("./app/users/UserDirectory"));
const Workflows = lazy(() => import("./app/workflows/Workflows"));
const Kiosk = lazy(() => import("./app/kiosk/Kiosk"));
const Login = lazy(() => import("./app/auth/Login"));
const ResetPassword = lazy(() => import("./app/auth/ResetPassword"));
const Settings = lazy(() => import("./app/settings/Settings"));
const AuditLogs = lazy(() => import("./app/audit/AuditLogs"));
const Profile = lazy(() => import("./app/profile/Profile"));
const HelpAndInfo = lazy(() => import("./pages/HelpAndInfo"));
const Federation = lazy(() => import("./app/federation/Federation"));
const DeviceManagement = lazy(() => import("./app/devices/DeviceManagement"));
const MonitoringDashboard = lazy(() => import("./app/monitoring/MonitoringDashboard"));
const WindowsAuthDashboard = lazy(() => import("./app/windows-auth/WindowsAuthDashboard"));
const PolicyManagement = lazy(() => import("./app/policies/PolicyManagement"));
const PinResetRequests = lazy(() => import("./app/kiosk/PinResetRequests"));
const Vault = lazy(() => import("./app/vault/Vault"));
import { useApp } from "./AppContext";
import "./styles.css";

const ProtectedRoute = ({ children }) => {
  const { user, authChecked, authState } = useApp();

  if (!authChecked || authState === "loading") return <main role="status" className="min-h-screen grid place-items-center">Checking session…</main>;

  if (!user && authState === "unauthorized") {
    return <Navigate to="/login" replace />;
  }
  if (authState === "forbidden") return <main role="alert" className="min-h-screen grid place-items-center">Your account is not authorized for this area.</main>;
  if (!user) return <main role="alert" className="min-h-screen grid place-items-center"><div><p>Identity service is unavailable; no security state was assumed.</p><button onClick={() => window.location.reload()}>Retry</button></div></main>;

  return <Layout>{children}</Layout>;
};

import { AppProvider } from "./AppContext";
import { notification } from "antd";

const GlobalNotificationListener = () => {
  const { lastEvent } = useApp();

  useEffect(() => {
    if (
      lastEvent?.type === "OnWorkflowChanged" &&
      lastEvent.data?.type === "PIN_RESET_REQUEST"
    ) {
      notification.warning({
        message: "Demande de Réinitialisation PIN",
        description: `Une demande de réinitialisation de PIN a été reçue pour l'utilisateur ID: ${lastEvent.data.userId} depuis la machine: ${lastEvent.data.machineName || "inconnue"}`,
        placement: "topRight",
        duration: 0, // Keep open until dismissed
      });
    }
  }, [lastEvent]);

  return null;
};

function App() {
  return (
    <AppProvider>
      <GlobalNotificationListener />
      <Router>
        <Suspense fallback={<main role="status" className="min-h-screen grid place-items-center">Loading…</main>}>
        <Routes>
          <Route path="/login" element={<Login />} />
          <Route path="/reset-password" element={<ResetPassword />} />

          <Route
            path="/"
            element={
              <ProtectedRoute>
                <Dashboard />
              </ProtectedRoute>
            }
          />

          <Route
            path="/users"
            element={
              <ProtectedRoute>
                <UserDirectory />
              </ProtectedRoute>
            }
          />

          <Route
            path="/workflows"
            element={
              <ProtectedRoute>
                <Workflows />
              </ProtectedRoute>
            }
          />

          <Route
            path="/kiosk"
            element={
              <ProtectedRoute>
                <Kiosk />
              </ProtectedRoute>
            }
          />

          <Route
            path="/kiosk/pin-resets"
            element={
              <ProtectedRoute>
                <PinResetRequests />
              </ProtectedRoute>
            }
          />

          <Route
            path="/audit"
            element={
              <ProtectedRoute>
                <AuditLogs />
              </ProtectedRoute>
            }
          />

          <Route
            path="/settings"
            element={
              <ProtectedRoute>
                <Settings />
              </ProtectedRoute>
            }
          />

          <Route
            path="/profile"
            element={
              <ProtectedRoute>
                <Profile />
              </ProtectedRoute>
            }
          />

          <Route
            path="/help"
            element={
              <ProtectedRoute>
                <HelpAndInfo />
              </ProtectedRoute>
            }
          />

          <Route
            path="/federation"
            element={
              <ProtectedRoute>
                <Federation />
              </ProtectedRoute>
            }
          />

          <Route
            path="/devices"
            element={
              <ProtectedRoute>
                <DeviceManagement />
              </ProtectedRoute>
            }
          />

          <Route
            path="/monitoring"
            element={
              <ProtectedRoute>
                <MonitoringDashboard />
              </ProtectedRoute>
            }
          />

          <Route
            path="/fleet"
            element={
              <ProtectedRoute>
                <WindowsAuthDashboard />
              </ProtectedRoute>
            }
          />

          <Route
            path="/policies"
            element={
              <ProtectedRoute>
                <PolicyManagement />
              </ProtectedRoute>
            }
          />

          <Route
            path="/vault"
            element={
              <ProtectedRoute>
                <Vault />
              </ProtectedRoute>
            }
          />

          {/* Fallback */}
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
        </Suspense>
      </Router>
    </AppProvider>
  );
}

export default App;
