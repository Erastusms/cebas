-- ==============================================================================
-- CEBAS Database Migration: 001_extensions.sql
-- Enables foundational PostgreSQL extensions and domain ENUM types
-- ==============================================================================

-- 1. PostgreSQL Extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "citext";

-- 2. Enumeration Types
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'user_role_enum') THEN
        CREATE TYPE user_role_enum AS ENUM ('USER', 'MODERATOR', 'ADMIN');
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'media_type_enum') THEN
        CREATE TYPE media_type_enum AS ENUM ('IMAGE', 'VIDEO', 'AUDIO');
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'media_status_enum') THEN
        CREATE TYPE media_status_enum AS ENUM ('UPLOADING', 'READY', 'FAILED', 'DELETED');
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'notification_type_enum') THEN
        CREATE TYPE notification_type_enum AS ENUM (
            'POST_LIKED',
            'POST_REPLIED',
            'REPLY_LIKED',
            'USER_FOLLOWED',
            'USER_MENTIONED'
        );
    END IF;
END $$;
