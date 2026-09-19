#!/usr/bin/env python3
"""Drives a full battle against a published module over the HTTP API.

Three independent identities stand in for three Unity clients, so the script
exercises the same multiplayer path the game uses: join, speed-based turn order,
basic attacks, area skills, potions, equipment changes and scheduled enemy turns.

Usage: playtest.py [database-name] [--server http://127.0.0.1:3000]
"""

import argparse
import json
import sys
import time
import urllib.error
import urllib.request

TURN_WAIT_SECONDS = 25.0
POLL_SECONDS = 0.25


class Client:
    """One identity talking to one database."""

    def __init__(self, server, database, label):
        self.server = server.rstrip("/")
        self.database = database
        self.label = label
        payload = self._post("/v1/identity", token=None)
        self.identity = payload["identity"]
        self.token = payload["token"]

    def _post(self, path, body=None, token=..., content_type="application/json"):
        data = None
        headers = {}
        if body is not None:
            data = body.encode() if isinstance(body, str) else json.dumps(body).encode()
            headers["Content-Type"] = content_type
        used = self.token if token is ... else token
        if used:
            headers["Authorization"] = f"Bearer {used}"
        request = urllib.request.Request(
            self.server + path, data=data, headers=headers, method="POST"
        )
        with urllib.request.urlopen(request) as response:
            raw = response.read().decode()
        return json.loads(raw) if raw.strip() else None

    def call(self, reducer, *args):
        try:
            self._post(
                f"/v1/database/{self.database}/call/{reducer}",
                body=list(args),
            )
            return None
        except urllib.error.HTTPError as error:
            return error.read().decode().strip()

    def sql(self, query):
        result = self._post(
            f"/v1/database/{self.database}/sql",
            body=query,
            content_type="text/plain",
        )
        rows = []
        for statement in result or []:
            columns = statement["schema"]["elements"]
            names = [column["name"]["some"] for column in columns]
            variants = [variant_names(column["algebraic_type"]) for column in columns]
            for row in statement["rows"]:
                decoded = [
                    names_for[value[0]] if names_for else unwrap(value)
                    for value, names_for in zip(row, variants)
                ]
                rows.append(dict(zip(names, decoded)))
        return rows


def variant_names(algebraic_type):
    """Variant names for a unit-only sum type, else None."""
    sum_type = algebraic_type.get("Sum")
    if not sum_type:
        return None
    return [variant["name"]["some"] for variant in sum_type["variants"]]


def unwrap(value):
    """Identity and Timestamp arrive as single-element products."""
    if isinstance(value, list) and len(value) == 1 and isinstance(value[0], str):
        return value[0].removeprefix("0x")
    return value


def scalar(value):
    return value


def rows_by(rows, key):
    return {row[key]: row for row in rows}


def fail(message):
    print(f"FAIL: {message}")
    sys.exit(1)


def wait_for_player_turn(admin, expected_ids, what):
    """Blocks until the active combatant is one of expected_ids."""
    deadline = time.time() + TURN_WAIT_SECONDS
    while time.time() < deadline:
        session = admin.sql("SELECT * FROM GameSession")[0]
        phase = scalar(session["phase"])
        if phase != "inBattle":
            return session, phase
        if int(session["active_entity_id"]) in expected_ids:
            return session, phase
        time.sleep(POLL_SECONDS)
    fail(f"timed out waiting for {what}")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("database", nargs="?", default="hophacks-merge")
    parser.add_argument("--server", default="http://127.0.0.1:3000")
    options = parser.parse_args()

    clients = [
        Client(options.server, options.database, f"client{i + 1}") for i in range(3)
    ]
    admin = clients[0]

    print("== reset ==")
    error = admin.call("reset_stage")
    if error:
        fail(f"reset_stage rejected: {error}")

    print("== three clients join (battle auto-starts on the third) ==")
    for client in clients:
        error = client.call("join_game")
        if error:
            fail(f"{client.label} join_game rejected: {error}")

    session = admin.sql("SELECT * FROM GameSession")[0]
    if scalar(session["phase"]) != "inBattle":
        fail(f"expected in_battle after 3 joins, got {scalar(session['phase'])}")
    print(f"   phase={scalar(session['phase'])} round={session['round']}")

    entities = admin.sql("SELECT * FROM Entity")
    by_id = rows_by(entities, "entity_id")
    players = sorted(
        (e for e in entities if scalar(e["faction"]) == "players"),
        key=lambda e: e["slot"],
    )
    enemies = sorted(
        (e for e in entities if scalar(e["faction"]) == "enemies"),
        key=lambda e: e["slot"],
    )
    if len(players) != 3:
        fail(f"expected 3 party members, got {len(players)}")
    if len(enemies) != 2:
        fail(f"expected 2 enemies, got {len(enemies)}")

    print("== rolled party (random class + random stats) ==")
    classes = set()
    for entity in players:
        classes.add(entity["class_name"])
        print(
            f"   {entity['name']:5} {entity['class_name']:8} "
            f"hp={entity['hp']}/{entity['max_hp']} mp={entity['mana']}/{entity['max_mana']} "
            f"STR={entity['strength']} DEX={entity['dexterity']} "
            f"INT={entity['intelligence']} SPD={entity['speed']} "
            f"ATK={entity['atk']} DEF={entity['defense']}"
        )
        if entity["strength"] < 1 or entity["speed"] < 1:
            fail(f"{entity['name']} has unrolled stats")
        if entity["atk"] < 1 or entity["defense"] < 1:
            fail(f"{entity['name']} gained no bonuses from starting gear")

    print("== equipment and bag ==")
    item_defs = rows_by(admin.sql("SELECT * FROM ItemDef"), "id")
    for client, entity in zip(clients, players):
        owned = client.sql("SELECT * FROM PlayerItem")
        mine = [i for i in owned if i["owner"] == client.identity]
        worn = {
            scalar(i["equipped_slot"]): item_defs[i["item_def_id"]]["name"]
            for i in mine
            if scalar(i["equipped_slot"]) != "bag"
        }
        bag = [
            f"{item_defs[i['item_def_id']]['name']} x{i['quantity']}"
            for i in mine
            if scalar(i["equipped_slot"]) == "bag"
        ]
        print(f"   {entity['name']:5} worn={worn}")
        print(f"         bag={bag}")
        for slot in ("weapon", "helmet", "chestplate", "leggings", "boots"):
            if slot not in worn:
                fail(f"{entity['name']} is missing the {slot} slot")
        if not bag:
            fail(f"{entity['name']} has an empty bag")

    print("== turn order (fastest first) ==")
    order = sorted(admin.sql("SELECT * FROM TurnOrder"), key=lambda r: r["idx"])
    speeds = [row["speed"] for row in order]
    for row in order:
        print(f"   {row['idx']}: {by_id[row['entity_id']]['name']:13} spd={row['speed']}")
    if speeds != sorted(speeds, reverse=True):
        fail(f"turn order is not sorted by speed: {speeds}")

    print("== equip from bag (swap in the spare chest piece) ==")
    swapper, swapper_entity = clients[0], players[0]
    spare = next(
        (
            item
            for item in swapper.sql("SELECT * FROM PlayerItem")
            if item["owner"] == swapper.identity
            and scalar(item["equipped_slot"]) == "bag"
            and scalar(item_defs[item["item_def_id"]]["kind"]) == "armor"
        ),
        None,
    )
    if spare is None:
        fail("no spare armour in the bag to equip")
    def stats_of(entity):
        row = admin.sql(f"SELECT * FROM Entity WHERE entity_id = {entity['entity_id']}")[0]
        return (row["defense"], row["max_hp"], row["max_mana"])

    original_chest = next(
        item
        for item in swapper.sql("SELECT * FROM PlayerItem")
        if item["owner"] == swapper.identity and scalar(item["equipped_slot"]) == "chestplate"
    )
    base = stats_of(swapper_entity)
    spare_name = item_defs[spare["item_def_id"]]["name"]

    error = swapper.call("equip_item", int(spare["id"]))
    if error:
        fail(f"equip_item rejected: {error}")
    swapped = stats_of(swapper_entity)
    print(f"   equipped {spare_name}: (def, maxhp, maxmp) {base} -> {swapped}")
    if swapped == base:
        fail("equipping a different chest piece changed nothing")

    # The displaced piece must land back in the bag, not vanish.
    displaced = next(
        item
        for item in swapper.sql("SELECT * FROM PlayerItem")
        if item["id"] == original_chest["id"]
    )
    if scalar(displaced["equipped_slot"]) != "bag":
        fail("the displaced chest piece did not return to the bag")

    error = swapper.call("unequip_item", int(spare["id"]))
    if error:
        fail(f"unequip_item rejected: {error}")
    bare = stats_of(swapper_entity)
    print(f"   unequipped {spare_name}: {swapped} -> {bare}")

    error = swapper.call("equip_item", int(original_chest["id"]))
    if error:
        fail(f"re-equip rejected: {error}")
    restored = stats_of(swapper_entity)
    print(f"   re-equipped the original: {bare} -> {restored}")
    if restored != base:
        fail(f"gear stats did not round-trip: {base} vs {restored}")

    wrong_weapon = next(
        (
            item
            for item in swapper.sql("SELECT * FROM PlayerItem")
            if item["owner"] == swapper.identity
            and scalar(item_defs[item["item_def_id"]]["kind"]) == "weapon"
            and item_defs[item["item_def_id"]]["weapon_type"]
            != item_defs[
                next(
                    i["item_def_id"]
                    for i in swapper.sql("SELECT * FROM PlayerItem")
                    if i["owner"] == swapper.identity
                    and scalar(i["equipped_slot"]) == "weapon"
                )
            ]["weapon_type"]
        ),
        None,
    )
    if wrong_weapon:
        error = swapper.call("equip_item", int(wrong_weapon["id"]))
        if not error:
            fail("a wrong-class weapon was accepted")

    print("== not-your-turn is rejected ==")
    active = int(admin.sql("SELECT * FROM GameSession")[0]["active_entity_id"])
    intruder = next(
        (
            (client, entity)
            for client, entity in zip(clients, players)
            if int(entity["entity_id"]) != active
        ),
        None,
    )
    if intruder:
        error = intruder[0].call("attack", int(enemies[0]["entity_id"]))
        if not error or "not your turn" not in error.lower():
            fail(f"expected a not-your-turn rejection, got {error!r}")
        print(f"   {intruder[1]['name']}: {error}")

    print("== play the battle ==")
    by_entity = {int(e["entity_id"]): (c, e) for c, e in zip(clients, players)}
    skills = rows_by(admin.sql("SELECT * FROM SkillDef"), "id")
    used = {"attack": 0, "aoe": 0, "single_skill": 0, "potion": 0, "focus": 0}
    turns = 0

    while turns < 60:
        session, phase = wait_for_player_turn(admin, set(by_entity), "a party turn")
        if phase != "inBattle":
            break

        client, entity = by_entity[int(session["active_entity_id"])]
        me = admin.sql(f"SELECT * FROM Entity WHERE entity_id = {entity['entity_id']}")[0]
        living = [
            e
            for e in admin.sql("SELECT * FROM Entity")
            if scalar(e["faction"]) == "enemies" and e["alive"]
        ]
        if not living:
            break
        target = int(sorted(living, key=lambda e: e["slot"])[0]["entity_id"])

        known = [
            skills[row["skill_def_id"]]
            for row in client.sql("SELECT * FROM EntitySkill")
            if row["entity_id"] == me["entity_id"]
        ]
        affordable = [s for s in known if s["mana_cost"] <= me["mana"]]
        aoe = [s for s in affordable if s["target_count"] > 1]
        single = sorted(
            (s for s in affordable if s["target_count"] == 1),
            key=lambda s: s["base_damage"],
            reverse=True,
        )
        mine = [i for i in client.sql("SELECT * FROM PlayerItem") if i["owner"] == client.identity]

        def potion(field):
            return next(
                (
                    i
                    for i in mine
                    if scalar(item_defs[i["item_def_id"]]["kind"]) == "consumable"
                    and item_defs[i["item_def_id"]][field] > 0
                ),
                None,
            )

        health, mana_potion = potion("heal_amount"), potion("mana_restore_amount")
        hurt = me["hp"] * 3 <= me["max_hp"]

        # Cover every action once, then play to win: heal when low, burn the
        # biggest affordable skill, spread damage with an area hit when two
        # enemies are still up, and fall back to the free swing.
        if used["attack"] == 0:
            action, error = "attack", client.call("attack", target)
        elif health and (hurt or used["potion"] == 0) and me["hp"] < me["max_hp"]:
            action, error = "potion", client.call("use_item", int(health["id"]))
        elif used["focus"] == 0 or (not affordable and mana_potion is None):
            action, error = "focus", client.call("focus")
        elif mana_potion and not affordable:
            action, error = "mana_potion", client.call("use_item", int(mana_potion["id"]))
        elif aoe and (len(living) > 1 or used["aoe"] == 0):
            action = "aoe"
            error = client.call("cast_skill", int(aoe[0]["id"]), target)
        elif single:
            action = "single_skill"
            error = client.call("cast_skill", int(single[0]["id"]), target)
        else:
            action, error = "attack", client.call("attack", target)

        if error:
            fail(f"{entity['name']} {action} rejected: {error}")
        used[action] = used.get(action, 0) + 1
        turns += 1

    print(f"   party turns taken: {turns}, actions used: {used}")
    for action in ("attack", "aoe", "single_skill", "potion", "focus"):
        if not used.get(action):
            fail(f"never exercised {action}")

    final = admin.sql("SELECT * FROM GameSession")[0]
    print(f"== final phase: {scalar(final['phase'])} round {final['round']} ==")
    if scalar(final["phase"]) != "victory":
        fail(
            f"a competently played 3v2 should be winnable, ended {scalar(final['phase'])} "
            f"on round {final['round']}"
        )

    log = sorted(admin.sql("SELECT * FROM BattleLog"), key=lambda r: r["id"])
    if len(log) < 20:
        fail(f"battle log only has {len(log)} lines")
    print(f"== battle log ({len(log)} lines, last 30) ==")
    for row in log[-30:]:
        print(f"   {row['message']}")

    if not any(scalar(row["kind"]) == "attack" and row["damage"] > 0 for row in log):
        fail("no damage-carrying attack rows for the client animations to replay")

    # One area cast has to produce several hits from the same actor on the same turn.
    multi = {}
    for row in log:
        if scalar(row["kind"]) == "attack":
            key = (row["round"], row["actor_entity_id"])
            multi.setdefault(key, set()).add(row["target_entity_id"])
    if not any(len(targets) > 1 for targets in multi.values()):
        fail("no multi-target hit landed, so the area attacks are not working")

    print("\nPASS")


if __name__ == "__main__":
    main()
