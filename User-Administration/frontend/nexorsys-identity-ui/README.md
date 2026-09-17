# PINÈDE IDENTITY - Frontend

This is the frontend for the PINÈDE IDENTITY IAM platform, built with React, Vite, and Tailwind CSS.

## Project Structure

```
frontend/nexorsys-identity-ui/
├── src/
│   ├── App.jsx              # Main application component
│   ├── main.tsx             # Application entry point
│   ├── styles.css           # Global styles
│   ├── types/               # TypeScript definitions
│   ├── components/          # Reusable components
│   └── app/                 # Feature modules
│       ├── dashboard/
│       ├── users/
│       ├── workflows/
│       └── kiosk/
├── public/                  # Static assets
├── package.json
├── tailwind.config.js
├── tsconfig.json
└── vite.config.ts
```

## Prerequisites

- Node.js 18+
- npm or yarn

## Development

### Starting the Dev Server

```bash
cd frontend/nexorsys-identity-ui
npm install
npm run dev
```

The application will be available at: http://localhost:3000

### Type Checking

```bash
npm run typecheck
```

### Building for Production

```bash
npm run build
```

The production build will be in the `dist` directory.

## Technology Stack

- **React 19** - UI library
- **Vite** - Build tool and dev server
- **TypeScript** - Type safety
- **Tailwind CSS** - Utility-first CSS framework
- **React Router** - Client-side routing

## Features

- Dark/Light mode support (using Tailwind)
- Dashboard with KPI cards and activity charts
- User Directory with AD sync view
- Business Enrichment Form
- Habilitation Matrix
- Kiosk PIN Management
- Workflow Inbox (RH/Manager approvals)
- Audit Log Viewer

## Testing

```bash
npm test
```

## Docker

The frontend can be containerized using Docker. See the provided Dockerfile and nginx.conf.
