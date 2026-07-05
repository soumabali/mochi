---
name: mochi-v2
status: draft
type: desktop
server: 43.156.128.55
ports: []
urls: []
repository: https://github.com/dhar/mochi-v2
created: 2026-07-05
tags: []
---

# mochi-v2

> Status: `draft` | Tipe: `desktop` | Dibuat: 2026-07-05

## Ringkasan

Deskripsi singkat project ini: tujuan, ruang lingkup, dan target pengguna.

## Arsitektur

- **Platform:** web / desktop / mobile / backend / monorepo
- **Tech stack:** (isi setelah diputuskan)
- **Deployment target:** 43.156.128.55

## Entry Points

| Komponen | Path | Catatan |
|----------|------|---------|
| Web app | `02-application/packages/web/` | |
| Desktop app | `02-application/packages/desktop/` | |
| API/backend | `02-application/packages/api/` | |
| Tests | `02-application/tests/` | |
| Deploy scripts | `02-application/deploy/` | |

## Quick Commands

```bash
# Dev server
make dev

# Run tests
make test

# Deploy
make deploy
```

## Catatan

Lihat folder `00-meta/` untuk mapping port, URL, dan credential.
