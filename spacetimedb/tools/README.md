# Module test tools

Two checks that drive a published module end to end. Both expect a running
server and a published database:

```bash
spacetime start                                   # in another shell
cd spacetimedb
spacetime publish --server local hophacks-merge -c -y
```

## `playtest.py`

Talks to the module over the HTTP API with three independent identities, so it
needs nothing but Python. Good for a quick check that the rules hold.

```bash
python3 spacetimedb/tools/playtest.py hophacks-merge
```

It resets the stage, joins three clients, and asserts that the party rolls random
classes and stats, starts with a full equipment set and a stocked bag, is ordered
by speed, rejects out-of-turn actions and wrong-class weapons, round-trips
equipment stats, lands area hits on several enemies at once, and can win.

## `clientcheck`

Connects three real clients over the WebSocket protocol using the published
`SpacetimeDB.ClientSDK` and the same generated bindings in
`Assets/module_bindings` that the Unity client compiles against. This covers what
the HTTP check cannot: subscriptions, BSATN decoding of every table, the index
filters the HUD reads through, and reducer argument encoding.

```bash
dotnet run --project spacetimedb/tools/clientcheck -- hophacks-merge http://127.0.0.1:3000
```

It plays the same battle as `playtest.py` and additionally asserts that battle log
rows arrive live carrying the actor, target and damage that the HUD's lunge and
hit animations are driven from.

## Compiling the client without a Unity licence

`tools/compile-client.sh` at the repository root compiles the client scripts with
Roslyn against the assemblies a Unity editor install ships. Useful on a machine
that has the editor unpacked but no licence, since the editor refuses to open a
project without one. Read the header of the script for the paths it expects. It
approximates a player build's define set, so it is a sanity check rather than a
replacement for an editor compile.
