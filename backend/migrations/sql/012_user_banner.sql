-- Migration 012: Add banner_url and banner_media_id to users table

ALTER TABLE users ADD COLUMN IF NOT EXISTS banner_url VARCHAR(500);
ALTER TABLE users ADD COLUMN IF NOT EXISTS banner_media_id UUID REFERENCES media(id) ON DELETE SET NULL;
