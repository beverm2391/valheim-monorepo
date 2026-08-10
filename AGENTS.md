# valheim-server agent instructions

## Agent behavior

- Read [`PROMPT.md`](PROMPT.md) before changing code, tests, scripts, or
  operations. It owns this repo's development, testing, and operation
  conventions.
- Treat the root and owning feature `PRODUCT.md` files as the product source of
  truth. Keep behavior, controls, player experience, acceptance meaning, and
  proof status in the document that owns them.
- When a player reports gameplay results, move confirmed behavior to **Current
  Behavior**, keep failed or unproven behavior in **In Development**, and
  delete behavior the player no longer wants.
- Manual test plans are task-scoped. Use a checklist for the current pass, but
  do not preserve a step-by-step test plan in a `PRODUCT.md`.
- Prefer one source of truth. Replace duplicated context with a pointer to the
  artifact, code, or command that owns the answer.
- Keep public docs generic. Do not commit secrets, passwords, tokens, private
  IPs, Steam IDs, world or character files, local save paths, or generated
  backup archives.
- Treat `server.env`, `r2.env`, downloaded backups, and Valheim world and
  character files as local-only operator data.
- Before a destructive server operation, download or verify a usable backup.
- Preserve vanilla-client compatibility unless Ben explicitly changes that
  product promise. Do not add custom persistent world objects or item data
  unless the product design requires them.
- Do not install, package, release, deploy, or change production state unless
  the task explicitly includes that action.

The nearest `PROMPT.md` owns path-local workflow details. This root file owns
agent behavior and points there so the two concerns do not drift back together.
