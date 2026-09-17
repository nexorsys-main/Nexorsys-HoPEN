-- PINÈDE IDENTITY - Database Schema
-- Auteur : Ali HAMIDY
-- Projet : HOP'EN - Clinique La Pinède
-- PostgreSQL 14+ compatible

-- Enable UUID extension
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ----------------------------
-- Tables (order matters for FK resolution)
-- ----------------------------

-- Users table (AD imported users) - created WITHOUT the circular FK first
CREATE TABLE IF NOT EXISTS users (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    ad_guid VARCHAR(36) UNIQUE,
    sam_account_name VARCHAR(255) NOT NULL,
    distinguished_name VARCHAR(1000) NOT NULL DEFAULT '',
    display_name VARCHAR(255),
    first_name VARCHAR(100),
    last_name VARCHAR(100),
    email VARCHAR(255),
    department VARCHAR(255),
    title VARCHAR(255),
    manager_ad_guid VARCHAR(36),
    employee_id VARCHAR(100),
    badge_uid VARCHAR(100),
    is_active BOOLEAN DEFAULT true,
    is_local_profile BOOLEAN DEFAULT false,
    role VARCHAR(50) DEFAULT 'VIEWER',
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW()
);

-- User Pins for kiosk authentication
CREATE TABLE IF NOT EXISTS user_pins (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL,
    badge_uid VARCHAR(100) UNIQUE NOT NULL,
    pin_hash VARCHAR(60) NOT NULL,
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW(),
    created_by UUID,

    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    FOREIGN KEY (created_by) REFERENCES users(id)
);

-- Applications/Habilitations
CREATE TABLE IF NOT EXISTS applications (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(255) UNIQUE NOT NULL,
    description TEXT,
    client_id VARCHAR(100) UNIQUE NOT NULL,
    client_secret VARCHAR(100),
    redirect_uris TEXT[],
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP DEFAULT NOW()
);

-- User permissions for applications
CREATE TABLE IF NOT EXISTS user_permissions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL,
    application_id UUID NOT NULL,
    permission_level VARCHAR(50) DEFAULT 'read',
    granted_at TIMESTAMP DEFAULT NOW(),
    granted_by UUID,
    expires_at TIMESTAMP,

    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    FOREIGN KEY (application_id) REFERENCES applications(id) ON DELETE CASCADE,
    FOREIGN KEY (granted_by) REFERENCES users(id)
);

-- Audit Logs
CREATE TABLE IF NOT EXISTS audit_logs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID,
    action VARCHAR(255) NOT NULL,
    resource_type VARCHAR(100),
    resource_id UUID,
    old_values JSONB,
    new_values JSONB,
    ip_address INET,
    user_agent TEXT,
    created_at TIMESTAMP DEFAULT NOW(),

    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE SET NULL
);

-- Workflows/RH approvals
CREATE TABLE IF NOT EXISTS workflows (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    type VARCHAR(100) NOT NULL,
    user_id UUID NOT NULL,
    status VARCHAR(50) DEFAULT 'pending',
    assigned_to UUID,
    assigned_by UUID,
    comments TEXT,
    form_data JSONB,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW(),

    FOREIGN KEY (user_id) REFERENCES users(id),
    FOREIGN KEY (assigned_to) REFERENCES users(id),
    FOREIGN KEY (assigned_by) REFERENCES users(id)
);

-- Kiosk Sessions
CREATE TABLE IF NOT EXISTS kiosk_sessions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL,
    session_token VARCHAR(100) UNIQUE NOT NULL,
    badge_uid VARCHAR(100) NOT NULL,
    nfc_uid VARCHAR(100) NOT NULL,
    expires_at TIMESTAMP NOT NULL,
    created_at TIMESTAMP DEFAULT NOW(),

    FOREIGN KEY (user_id) REFERENCES users(id)
);

-- ----------------------------
-- Indexes for performance
-- ----------------------------
CREATE INDEX IF NOT EXISTS idx_users_ad_guid ON users(ad_guid);
CREATE INDEX IF NOT EXISTS idx_users_badge_uid ON users(badge_uid);
CREATE INDEX IF NOT EXISTS idx_users_email ON users(email);
CREATE INDEX IF NOT EXISTS idx_user_pins_badge_uid ON user_pins(badge_uid);
CREATE INDEX IF NOT EXISTS idx_audit_logs_created_at ON audit_logs(created_at);
CREATE INDEX IF NOT EXISTS idx_workflows_user_id ON workflows(user_id);
CREATE INDEX IF NOT EXISTS idx_workflows_assigned_to ON workflows(assigned_to);
CREATE INDEX IF NOT EXISTS idx_kiosk_sessions_token ON kiosk_sessions(session_token);

-- ----------------------------
-- Initial seed data
-- ----------------------------

-- Create default applications
INSERT INTO applications (id, name, description, client_id, client_secret, redirect_uris, is_active) VALUES
('a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11', 'NexorSys Identity UI', 'Identity administration client; configure a secret through secure provisioning.', 'nexorsys_identity_ui', NULL, ARRAY[]::text[], true),
('a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a12', 'NexorSys Kiosk', 'Kiosk client; authenticate using enrolled workstation certificate.', 'nexorsys_kiosk', NULL, ARRAY[]::text[], true)
ON CONFLICT (id) DO NOTHING;

-- Administrator and tenant provisioning are explicit, audited deployment operations.
