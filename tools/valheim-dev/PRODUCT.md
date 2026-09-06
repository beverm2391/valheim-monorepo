# Valheim Dev

Valheim Dev lets Codex control Valheim while Ben plays in a disposable local
test world. Codex can inspect the game, give items, change values, create
objects, and install ongoing behavior while Ben sees the result. Ben should not need
to rebuild or relaunch the game between experiments.

The interface uses general code execution with a short reference for common
operations. Separate tools for each item, creature, UI element, or gameplay
action are unnecessary. Existing code and requirements must earn their place
by serving this workflow.

## Run Commands And Try Changes

Codex can run a trusted C# command for observation or action. Giving an item or
changing a value does not require an ID for installed code or a cleanup function.
A completed command may leave effects in the world or character. The tool does
not promise to reverse those effects.

Code that keeps running can be installed, replaced, or explicitly removed.
For example, an orbiting object needs a running component and a way to remove
that component. Replacement failures must report what remains installed and
whether the previous version was restored.

Status describes installed code and known errors. Registered code alone does
not prove that its effect still applies. A player can respawn,
an object can disappear, or Valheim can overwrite a value. Codex verifies the
effect through a fresh observation and Ben's feedback.

Cleanup is explicit. Turning Lab off closes control access without undoing
prior commands or automatically removing installed changes. Reopening Lab in
the same world lets Codex inspect and remove that installed code. Leaving the
world ends tracking for that world's installed code. Installed code must not
carry into another world.
This does not promise to undo saved items, creatures, damage, or other effects.

## Make The Tool Easy To Use

A short reference shows agents how to perform common operations and verify
results.

Normal responses stay concise. Agents can open the ledger for detailed evidence
about each operation.

Commands accept structured inputs and return structured results. Agents can
reuse the same code with different parameters. They can pass one command's
result into another without editing hard-coded source values or parsing prose.

Codex can keep disposable recipes on disk. Each recipe uses one folder. The
folder name is the recipe ID and the folder contains `code.cs`. An optional
`presets.json` contains an array of structured input sets. The array position is
the preset index. No preset is the default.

One `run_recipes` MCP tool accepts one or more recipes in order. For each
recipe, the agent supplies its ID. It may also select one preset index or
provide ad hoc structured inputs, but not both. The tool reads the current files
for each call, so an agent can use an edit without restarting the MCP server.
The source entrypoint selects a one-time command or managed change. A managed
recipe uses its recipe ID as the change ID, so selecting another preset replaces
the same experiment.

The tool uses the Lab's existing authorization, compilation, ledger, and
cleanup behavior. Its response identifies the outcome of every attempted recipe
and every managed change that remains active. It does not hide partial success
or failure.

Recipes are experimental scratch. Valheim Dev makes no compatibility or
preservation promise for them. Keeping or running a recipe does not promote it
into Benheim.

The existing ledger provides a compact history. Each run shows a short human
label, time, outcome, and duration. Agents can open a run to see its source,
result, and errors. The history needs no new database or dashboard.

[Benheim Developer Diagnostics](../../client-mods/benheim/src/DeveloperDiagnostics/PRODUCT.md)
owns shipped gameplay diagnostics. Valheim Dev can read that evidence and
record its own operations without creating another gameplay logging system.
Agents can read warnings and errors from the time of a run onward. Repeated
messages are collapsed into counts. When installed code keeps running, agents
can read errors logged after the entrypoint returns. A log entry after a run does not
prove that the run caused it. Logs prove only the observations they contain.
Ben judges visible behavior.

## Ben Controls The Lab

Ben creates and selects the disposable local character and world. He runs
`bh lab on` after entering the world. That enables repeated Codex operations
without separate approval for each command. Running `bh lab off`, leaving the
world, or quitting Valheim ends access to that Lab session.

Valheim Dev trusts Ben's local machine. It does not authenticate one local
process against another. A request prepared for an earlier Lab session cannot
run in the current Lab session.

Valheim Dev connects only to the enabled local single-player Lab. It does not
connect to the shared production world, an ordinary Benheim session, or a
dedicated server. It cannot enable Lab, manage saves, launch, quit, or restart
Valheim.

Runtime code must return control to the game loop. Valheim Dev cannot preempt
code that hangs Unity's main thread or guarantee that arbitrary effects can be
undone. Ben decides whether to restart the game or reset his disposable saves.

Experiment behavior enters normal Benheim source only when Ben explicitly
instructs a dev lead to implement it. That implementation uses the normal
Benheim build and shipping workflow.
