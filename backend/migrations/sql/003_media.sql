-- ==============================================================================
-- CEBAS Database Migration: 003_media.sql
-- Creates media metadata table and adds avatar reference to users
-- ==============================================================================

-- 1. Create Media Metadata Table
CREATE TABLE IF NOT EXISTS media (
    id UUID PRIMARY KEY,
    owner_user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    original_file_name VARCHAR(255) NOT NULL,
    storage_key VARCHAR(500) NOT NULL,
    mime_type VARCHAR(100) NOT NULL,
    file_size BIGINT NOT NULL,
    status media_status_enum NOT NULL DEFAULT 'UPLOADING',
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ,
    confirmed_at TIMESTAMPTZ,
    deleted_at TIMESTAMPTZ
);

-- 2. Indexes for Media Access Patterns
CREATE INDEX IF NOT EXISTS idx_media_owner_user_id ON media (owner_user_id);
CREATE INDEX IF NOT EXISTS idx_media_status ON media (status);
CREATE INDEX IF NOT EXISTS idx_media_created_at ON media (created_at);

-- 3. Link Avatar Media Reference on Users Table
ALTER TABLE users ADD COLUMN IF NOT EXISTS avatar_media_id UUID REFERENCES media(id) ON DELETE SET NULL;
CREATE INDEX IF NOT EXISTS idx_users_avatar_media_id ON users (avatar_media_id);

-- 4. Implicit String Casts for PostgreSQL Media Enums
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_cast c 
        JOIN pg_type s ON c.castsource = s.oid 
        JOIN pg_type t ON c.casttarget = t.oid 
        WHERE s.typname = 'varchar' AND t.typname = 'media_status_enum'
    ) THEN
        CREATE CAST (varchar AS media_status_enum) WITH INOUT AS IMPLICIT;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_cast c 
        JOIN pg_type s ON c.castsource = s.oid 
        JOIN pg_type t ON c.casttarget = t.oid 
        WHERE s.typname = 'text' AND t.typname = 'media_status_enum'
    ) THEN
        CREATE CAST (text AS media_status_enum) WITH INOUT AS IMPLICIT;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_cast c 
        JOIN pg_type s ON c.castsource = s.oid 
        JOIN pg_type t ON c.casttarget = t.oid 
        WHERE s.typname = 'varchar' AND t.typname = 'media_type_enum'
    ) THEN
        CREATE CAST (varchar AS media_type_enum) WITH INOUT AS IMPLICIT;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_cast c 
        JOIN pg_type s ON c.castsource = s.oid 
        JOIN pg_type t ON c.casttarget = t.oid 
        WHERE s.typname = 'text' AND t.typname = 'media_type_enum'
    ) THEN
        CREATE CAST (text AS media_type_enum) WITH INOUT AS IMPLICIT;
    END IF;
END $$;
