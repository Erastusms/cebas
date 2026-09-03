-- ==============================================================================
-- CEBAS Database Migration: 015_seed_realistic_replies.sql
-- Seeds authentic conversation reply threads between X accounts
-- and synchronizes posts.reply_count with authoritative post_replies counts.
-- ==============================================================================

-- 1. Marselino starting XI post (018f0200-0000-7000-8000-000000000001 by idextratime)
INSERT INTO post_replies (id, post_id, author_id, parent_reply_id, content, is_deleted, created_at)
VALUES
    ('018f0300-0000-7000-8000-000000000001'::uuid, '018f0200-0000-7000-8000-000000000001'::uuid, '018f0100-0000-7000-8000-000000000002'::uuid, NULL,
     'Kabar luar biasa! Jam terbang di Eropa bakal krusial banget buat persiapan Kualifikasi Piala Dunia. Semoga starter dan main full 90 menit! 🇮🇩⚽', FALSE, CURRENT_TIMESTAMP - interval '3 hours 40 minutes'),

    ('018f0300-0000-7000-8000-000000000002'::uuid, '018f0200-0000-7000-8000-000000000001'::uuid, '018f0100-0000-7000-8000-000000000003'::uuid, NULL,
     'Prediksi formasi nanti malam bakal main di posisi gelandang serang atau sayap kiri nih min?', FALSE, CURRENT_TIMESTAMP - interval '3 hours 20 minutes'),

    ('018f0300-0000-7000-8000-000000000003'::uuid, '018f0200-0000-7000-8000-000000000001'::uuid, '018f0100-0000-7000-8000-000000000001'::uuid, '018f0300-0000-7000-8000-000000000002'::uuid,
     'Kemungkinan besar jadi inverted winger kiri, menusuk ke tengah seperti skema pertandingan uji coba sebelumnya!', FALSE, CURRENT_TIMESTAMP - interval '3 hours 10 minutes'),

    ('018f0300-0000-7000-8000-000000000004'::uuid, '018f0200-0000-7000-8000-000000000001'::uuid, '018f0100-0000-7000-8000-000000000004'::uuid, NULL,
     'Secara taktikal, visinya dalam distribusi bola cepat bakal sangat dibutuhkan timnya untuk memecah low block pertahanan lawan.', FALSE, CURRENT_TIMESTAMP - interval '2 hours 50 minutes')
ON CONFLICT (id) DO NOTHING;

-- 2. Full Time match post (018f0200-0000-7000-8000-000000000002 by idextratime - Image 1 post)
INSERT INTO post_replies (id, post_id, author_id, parent_reply_id, content, is_deleted, created_at)
VALUES
    ('018f0300-0000-7000-8000-000000000005'::uuid, '018f0200-0000-7000-8000-000000000002'::uuid, '018f0100-0000-7000-8000-000000000002'::uuid, NULL,
     'Pertandingan gila! Pergantian taktik di menit ke-80 bener-bener mengubah dinamika lapangan. Tensi tinggi sampai peluit panjang! 🔥', FALSE, CURRENT_TIMESTAMP - interval '2 hours 45 minutes'),

    ('018f0300-0000-7000-8000-000000000006'::uuid, '018f0200-0000-7000-8000-000000000002'::uuid, '018f0100-0000-7000-8000-000000000003'::uuid, NULL,
     'Mentalitas juara berbicara di menit-menit akhir. Layak dapat 3 poin penuh lewat perjuangan ekstra! ⚽👏', FALSE, CURRENT_TIMESTAMP - interval '2 hours 30 minutes'),

    ('018f0300-0000-7000-8000-000000000007'::uuid, '018f0200-0000-7000-8000-000000000002'::uuid, '018f0100-0000-7000-8000-000000000001'::uuid, '018f0300-0000-7000-8000-000000000006'::uuid,
     'Betul banget! Daya juang pemain sampai detik terakhir patut diacungi dua jempol 👍👍', FALSE, CURRENT_TIMESTAMP - interval '2 hours 20 minutes'),

    ('018f0300-0000-7000-8000-000000000008'::uuid, '018f0200-0000-7000-8000-000000000002'::uuid, '018f0100-0000-7000-8000-000000000007'::uuid, NULL,
     'Dramatis banget! Gol penentu di injury time bener-bener bikin seisi tribun bergemuruh kencang.', FALSE, CURRENT_TIMESTAMP - interval '2 hours 5 minutes')
ON CONFLICT (id) DO NOTHING;

-- 3. BMKG weather post (018f0200-0000-7000-8000-000000000016 by detikcom)
INSERT INTO post_replies (id, post_id, author_id, parent_reply_id, content, is_deleted, created_at)
VALUES
    ('018f0300-0000-7000-8000-000000000009'::uuid, '018f0200-0000-7000-8000-000000000016'::uuid, '018f0100-0000-7000-8000-000000000016'::uuid, NULL,
     'Masyarakat diimbau selalu waspada potensi genangan di ruas jalan protokol serta sedia payung/jas hujan saat jam pulang kantor.', FALSE, CURRENT_TIMESTAMP - interval '5 hours'),

    ('018f0300-0000-7000-8000-000000000010'::uuid, '018f0200-0000-7000-8000-000000000016'::uuid, '018f0100-0000-7000-8000-00000000005e'::uuid, NULL,
     'Pantauan lapangan terkini: Jalur Rasuna Said dan Gatot Subroto mulai diguyur hujan intensitas sedang. Tetap berhati-hati bagi pengendara roda dua!', FALSE, CURRENT_TIMESTAMP - interval '4 hours 30 minutes')
ON CONFLICT (id) DO NOTHING;

-- 4. React 19 release post (018f0200-0000-7000-8000-00000000003f by reactjs)
INSERT INTO post_replies (id, post_id, author_id, parent_reply_id, content, is_deleted, created_at)
VALUES
    ('018f0300-0000-7000-8000-000000000011'::uuid, '018f0200-0000-7000-8000-00000000003f'::uuid, '018f0100-0000-7000-8000-000000000043'::uuid, NULL,
     'Huge milestone for the entire ecosystem! Next.js 15 App Router is fully optimized to harness React 19 Actions and Compiler out-of-the-box. 🚀', FALSE, CURRENT_TIMESTAMP - interval '8 hours'),

    ('018f0300-0000-7000-8000-000000000012'::uuid, '018f0200-0000-7000-8000-00000000003f'::uuid, '018f0100-0000-7000-8000-000000000042'::uuid, '018f0300-0000-7000-8000-000000000011'::uuid,
     'And deploying React 19 apps on Vercel delivers automatic Edge caching for Server Actions with zero configuration required.', FALSE, CURRENT_TIMESTAMP - interval '7 hours 30 minutes'),

    ('018f0300-0000-7000-8000-000000000013'::uuid, '018f0200-0000-7000-8000-00000000003f'::uuid, '018f0100-0000-7000-8000-00000000003f'::uuid, NULL,
     'Full end-to-end type safety with TypeScript 5.8 types is already published on @types/react. Happy coding!', FALSE, CURRENT_TIMESTAMP - interval '7 hours'),

    ('018f0300-0000-7000-8000-000000000014'::uuid, '018f0200-0000-7000-8000-00000000003f'::uuid, '018f0100-0000-7000-8000-000000000044'::uuid, NULL,
     'Tailwind CSS v4 pairs cleanly with the new compiler directives without extra Babel transforms needed.', FALSE, CURRENT_TIMESTAMP - interval '6 hours 45 minutes')
ON CONFLICT (id) DO NOTHING;

-- 5. .NET 10 post (018f0200-0000-7000-8000-000000000040 by dotnet)
INSERT INTO post_replies (id, post_id, author_id, parent_reply_id, content, is_deleted, created_at)
VALUES
    ('018f0300-0000-7000-8000-000000000015'::uuid, '018f0200-0000-7000-8000-000000000040'::uuid, '018f0100-0000-7000-8000-000000000040'::uuid, NULL,
     'GitHub Actions runners now feature pre-installed .NET 10 SDKs for instantaneous CI/CD build starts across all repository workflows.', FALSE, CURRENT_TIMESTAMP - interval '10 hours'),

    ('018f0300-0000-7000-8000-000000000016'::uuid, '018f0200-0000-7000-8000-000000000040'::uuid, '018f0100-0000-7000-8000-000000000045'::uuid, NULL,
     'Official multi-arch Docker base images for .NET 10 (chiseled Ubuntu and Alpine) are available on Docker Hub! 🐳', FALSE, CURRENT_TIMESTAMP - interval '9 hours 30 minutes')
ON CONFLICT (id) DO NOTHING;

-- 6. Anime announcement post (018f0200-0000-7000-8000-00000000002a by animetv_jp)
INSERT INTO post_replies (id, post_id, author_id, parent_reply_id, content, is_deleted, created_at)
VALUES
    ('018f0300-0000-7000-8000-000000000017'::uuid, '018f0200-0000-7000-8000-00000000002a'::uuid, '018f0100-0000-7000-8000-00000000002a'::uuid, NULL,
     'We are thrilled to confirm worldwide streaming rights for this upcoming season! Stay tuned for the official subtitled trailer release! ✨🍿', FALSE, CURRENT_TIMESTAMP - interval '12 hours'),

    ('018f0300-0000-7000-8000-000000000018'::uuid, '018f0200-0000-7000-8000-00000000002a'::uuid, '018f0100-0000-7000-8000-00000000002b'::uuid, NULL,
     'Immediate spike in anticipation rankings! The animation quality in the key visual looks absolutely breathtaking. 📈🔥', FALSE, CURRENT_TIMESTAMP - interval '11 hours 30 minutes'),

    ('018f0300-0000-7000-8000-000000000019'::uuid, '018f0200-0000-7000-8000-00000000002a'::uuid, '018f0100-0000-7000-8000-00000000003c'::uuid, NULL,
     'Kabar gembira buat wibu Indonesia! Bakal tayang legal gratis di YouTube Muse Indonesia. Jangan lupa subscribe dan nyalakan loncengnya! 🇮🇩📺', FALSE, CURRENT_TIMESTAMP - interval '11 hours')
ON CONFLICT (id) DO NOTHING;

-- 7. Lembur martabak post (018f0200-0000-7000-8000-000000000053 by txtdrkaumbiasa)
INSERT INTO post_replies (id, post_id, author_id, parent_reply_id, content, is_deleted, created_at)
VALUES
    ('018f0300-0000-7000-8000-000000000020'::uuid, '018f0200-0000-7000-8000-000000000053'::uuid, '018f0100-0000-7000-8000-000000000057'::uuid, NULL,
     'Martabaknya martabak telor bebek apa martabak manis min? Kalo manis minimal red velvet cream cheese lah ya biar agak terobati lukanya wkwk', FALSE, CURRENT_TIMESTAMP - interval '14 hours'),

    ('018f0300-0000-7000-8000-000000000021'::uuid, '018f0200-0000-7000-8000-000000000053'::uuid, '018f0100-0000-7000-8000-000000000052'::uuid, NULL,
     'Relatable banget, mending martabak daripada cuma dapet stiker jempol atau ucapan ''terima kasih tim kerja kerasnya'' di grup WA jam 12 malem 😭', FALSE, CURRENT_TIMESTAMP - interval '13 hours 30 minutes')
ON CONFLICT (id) DO NOTHING;

-- Synchronize reply_count on all posts to perfectly match active rows in post_replies
UPDATE posts p
SET reply_count = (
    SELECT COUNT(*) FROM post_replies pr
    WHERE pr.post_id = p.id AND pr.is_deleted = FALSE
);
