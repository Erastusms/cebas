-- ==============================================================================
-- CEBAS Database Migration: 017_reports_and_moderation.sql
-- Creates reports table, moderation_audit_logs table, and adds moderation/suspension
-- columns to users and posts.
-- ==============================================================================

-- 1. Extend users table with suspension state
ALTER TABLE users ADD COLUMN IF NOT EXISTS is_suspended BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE users ADD COLUMN IF NOT EXISTS suspended_at TIMESTAMPTZ;
ALTER TABLE users ADD COLUMN IF NOT EXISTS suspension_reason TEXT;

CREATE INDEX IF NOT EXISTS idx_users_is_suspended ON users (is_suspended) WHERE is_suspended = TRUE;

-- 2. Extend posts table with moderation hide state
ALTER TABLE posts ADD COLUMN IF NOT EXISTS is_hidden BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE posts ADD COLUMN IF NOT EXISTS hidden_at TIMESTAMPTZ;
ALTER TABLE posts ADD COLUMN IF NOT EXISTS hidden_reason TEXT;

CREATE INDEX IF NOT EXISTS idx_posts_is_hidden ON posts (is_hidden) WHERE is_hidden = TRUE;

-- 3. Create reports table
CREATE TABLE IF NOT EXISTS reports (
    id UUID PRIMARY KEY,
    reporter_user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    target_post_id UUID REFERENCES posts(id) ON DELETE SET NULL,
    target_user_id UUID REFERENCES users(id) ON DELETE SET NULL,
    category VARCHAR(50) NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'PENDING',
    reason TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ,
    resolved_at TIMESTAMPTZ,
    resolved_by_user_id UUID REFERENCES users(id) ON DELETE SET NULL,
    CONSTRAINT chk_reports_target_xor CHECK (
        (target_post_id IS NOT NULL AND target_user_id IS NULL) OR
        (target_post_id IS NULL AND target_user_id IS NOT NULL)
    ),
    CONSTRAINT chk_reports_category CHECK (
        category IN ('SPAM', 'HARASSMENT', 'HATE_SPEECH', 'INAPPROPRIATE_CONTENT')
    ),
    CONSTRAINT chk_reports_status CHECK (
        status IN ('PENDING', 'RESOLVED', 'DISMISSED')
    )
);

-- Operational indexes for moderation queue and reporter queries
CREATE INDEX IF NOT EXISTS idx_reports_status_created ON reports (status, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_reports_category_status ON reports (category, status);
CREATE INDEX IF NOT EXISTS idx_reports_reporter_created ON reports (reporter_user_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_reports_target_post ON reports (target_post_id) WHERE target_post_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_reports_target_user ON reports (target_user_id) WHERE target_user_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_reports_pending_target_post ON reports (target_post_id, reporter_user_id) WHERE status = 'PENDING' AND target_post_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_reports_pending_target_user ON reports (target_user_id, reporter_user_id) WHERE status = 'PENDING' AND target_user_id IS NOT NULL;

-- 4. Create moderation_audit_logs table
CREATE TABLE IF NOT EXISTS moderation_audit_logs (
    id UUID PRIMARY KEY,
    actor_user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    action VARCHAR(50) NOT NULL,
    target_type VARCHAR(50) NOT NULL,
    target_id UUID NOT NULL,
    reason TEXT,
    metadata JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_moderation_audit_logs_actor ON moderation_audit_logs (actor_user_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_moderation_audit_logs_target ON moderation_audit_logs (target_type, target_id);
CREATE INDEX IF NOT EXISTS idx_moderation_audit_logs_created ON moderation_audit_logs (created_at DESC);
