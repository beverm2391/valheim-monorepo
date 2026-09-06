# Working learnings

This is a short scratchpad for lessons found during live Valheim and Benheim
experimentation. Move settled behavior or guidance to its owning document, then
remove the absorbed bullet here.

- Test fixtures that depend on mod behavior should use a mod-owned helper that
  exercises the production initialization and validation path. Directly
  mutating private fields can create false positives, such as UI recognizing an
  Affinity that its gameplay runtime rejects.
- Before designing a new UI primitive, ask about and inspect Valheim's loaded
  native UI primitives for something reusable. Begin the experiment from the
  closest native donor.
