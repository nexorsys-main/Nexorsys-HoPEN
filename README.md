# Nexorsys-HoPEN (Hospital Patient Environment Network)

Welcome to the **Nexorsys-HoPEN** unified workspace. This repository contains the complete enterprise suite for identity management, NFC-based kiosk authentication, and workstation management.

## 📂 Repository Structure

This is a monorepo containing multiple interconnected applications and services that make up the Nexorsys ecosystem.

### 1. `User-Administration/` (Core Production Ecosystem)
The main production suite is located in the `User-Administration` directory.
- **`/backend`**: The robust .NET 8 Web API serving as the central Identity Provider (IdP) and fleet management engine. Manages policies, telemetry, licensing, and database interactions.
- **`/frontend/nexorsys-identity-ui`**: The administrative web dashboard (React + Vite) for managing users, NFC badges, security policies, and device fleets.
- **`/kiosk-nfc`**: The production WPF NFC Kiosk application deployed to physical devices. It securely interfaces with the backend and handles NFC card reads via smartcard APIs.
- **`/kiosk-windows-auth`**: A custom C++ Windows Credential Provider designed to interface with the Nexorsys ecosystem for secure workstation authentication.
- **`/scripts`**: An extensive collection of PowerShell and Bash scripts for enterprise deployment, automated health checks, database backups, and CI/CD operations.

### 2. `Nexorsys.NFC.APP/` (Legacy Reference)
A legacy reference implementation of the NFC kiosk application. *Note: Active production development happens in `User-Administration/kiosk-nfc`.*

---

## 🚀 Getting Started

### Prerequisites
- **Backend:** .NET 8 SDK, SQL Server
- **Frontend:** Node.js (v18+)
- **Kiosk:** Windows OS, .NET 8 Desktop Runtime, compatible NFC SmartCard reader

### Running the Services Locally

#### Backend API
```bash
cd User-Administration/backend/src/Nexorsys.Identity.API
dotnet run
```

#### Frontend Dashboard
```bash
cd User-Administration/frontend/nexorsys-identity-ui
npm install
npm run dev
```

#### NFC Kiosk Application
```bash
cd User-Administration/kiosk-nfc
dotnet run
```

---

## 🔒 Security & Architecture
This platform is built with healthcare and enterprise security in mind, featuring:
- **Zero-Trust Architecture**: Hardware-bound tokens, continuous validation.
- **Risk-Based Authentication**: Context-aware security policies.
- **Enterprise Deployment Ready**: Comprehensive MSI builders and deployment scripts for massive fleets.

---

*© 2026 Nexorsys. All rights reserved.*
