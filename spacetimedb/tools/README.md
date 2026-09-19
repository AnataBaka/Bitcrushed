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
