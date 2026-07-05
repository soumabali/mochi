# Message Bus (APPEND-ONLY — never edit or delete existing lines)

Workers append here; Hermes reads before every dispatch.
type: finding blocker question handoff
Rule: any change to a shared contract (API signature, schema, env var,
dependency version) MUST be logged here as `finding`.

| type | ref | task | message |
|------|-----|------|--------|