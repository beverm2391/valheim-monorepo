# Installed Helper Commands

Use this when a `run_once` command calls an installed helper, or an action
depends on a separate fixture-preparation command. The technical contract is
in [SPEC.md](SPEC.md).

Keep the `operation_id` returned by `install_change` in subsequent commands'
structured `inputs`, along with its `change_id`. Inside `run_once`, resolve the
installed helper through its active registry identity:

```csharp
Type helper = ValheimDev.ValheimDevManagedChanges.ResolveType(
    input.changeId, input.installOperationId, input.helperType);
```

Invoke the helper's intended method on that returned type. Searching loaded
assemblies for a first or last matching type can select a cleaned or replaced
helper; cleanup does not unload its assembly.

When a shot or other action depends on preparation, pass the preparation run's
`operation_id` as the action's `requires_operation_id`. The prerequisite must
have succeeded in the same authorization and build; otherwise the action
records `prerequisite_failed` without compiling or executing. Preparation must
throw when it cannot establish its required conditions. A returned JSON
`ok: false` field is not interpreted as a failed operation. Inspect the ledger
failure and prepare again before issuing a dependent action.
