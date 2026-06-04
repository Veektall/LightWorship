# Security Policy

## Secrets

Do not commit API keys or provider credentials. Use local settings only:

- `data/settings.json` is ignored by Git.
- `data/settings.example.json` documents the expected shape with empty keys.

If a credential is accidentally committed, revoke it at the provider dashboard and rotate it before publishing a new release.

## Reporting Issues

For now, please open a GitHub issue with reproduction steps and the affected version. Do not include secrets, private media, church member information, or copyrighted licensed Bible/lyric content in public reports.
