# Nexorsys-HoPEN

Welcome to the **Nexorsys-HoPEN** unified workspace. This repository contains the complete enterprise suite for identity management, NFC-based kiosk authentication, and workstation management.

## 📖 About the Project

**Nexorsys-HoPEN** is an advanced authentication and fleet management platform engineered specifically to meet the stringent security and operational requirements of modern healthcare environments. The platform delivers seamless, frictionless access to medical workstations via NFC badges while enforcing zero-trust principles and robust audit trails behind the scenes. 

By unifying physical badge access, Windows credential provisioning, and centralized identity administration, the project ensures that healthcare professionals can authenticate quickly and securely, allowing them to focus on patient care rather than complex login procedures.

## 🏥 Context & Compliance

This solution is designed in direct alignment with major French national healthcare digitization and security programs:

### HoPEN (Hôpital Numérique Ouvert sur son Environnement)
The HoPEN program is a national initiative in France aimed at modernizing hospital information systems (HIS). It focuses on improving the quality of care, facilitating information sharing, and ensuring that hospitals are digitally equipped to interact securely with their surrounding healthcare ecosystem. **Nexorsys-HoPEN** directly supports these goals by providing the secure authentication foundation necessary for modern, interoperable digital health tools.

### Programme CaRE (Cybersécurité, Accélération et Résilience des Établissements)
The CaRE program is the French national action plan dedicated to strengthening the cybersecurity of healthcare institutions. It mandates strict controls on access, identity verification, and traceability to protect sensitive patient data against cyber threats. **Nexorsys** inherently enforces CaRE principles through its hardware-bound tokens, risk-based authentication, and comprehensive audit logging, ensuring that hospital networks remain resilient and compliant.

## 🏢 About NexorSys

**NexorSys** is an innovative technology provider specializing in highly secure identity and access management (IAM) solutions tailored for complex, high-stakes environments like healthcare. Our mission is to seamlessly bridge the gap between rigorous cybersecurity compliance and daily operational efficiency.

For more information about our products, services, and vision, please visit us at **[nexorsys.fr](https://nexorsys.fr)**.

---

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
