# CLAUDE.md — Shepherds Journey

Shepherds Journey is a Unity (C#) project built by a team of 16: **8 artists and 8 developers**.
Several developers use Claude at the same time, each on their own part of the game. These rules
keep every developer in their own lane. Follow them in every session, without exception.

---

## Start of every session (do this before anything else)

1. **Find out who the developer is.** Before reading, planning, or changing anything, ask:
   > "Before we start, who am I working with? Please enter your name or letter tag."
2. Match the answer to the table below. If it doesn't match anyone, ask again. Do not continue
   until you know who the developer is.
3. **Call the developer by name** throughout the session, so they can see you know who is working.
4. Check the current git branch. If it is not the developer's own branch, tell them before doing
   any work.

---

## Developers

| Developer | Tag | Folder prefix | Git branch  | Current folders                              |
|-----------|-----|---------------|-------------|----------------------------------------------|
| Terry     | Z   | `_Z_`         | `Terry`     | `Assets/_Z_GameCore`, `Assets/_Z_HUD`, `Assets/_Z_Menus` |
| Mia       | M   | `_M_`         | `Mia`       | `Assets/_M_Player`                           |
| Dylan     | D   | `_D_`         | `Dylan`     | `Assets/_D_Enemy`                            |
| Sabine    | S   | `_S_`         | `Sabine`    | `Assets/_S_Sheep`                            |
| Logan     | L   | `_L_`         | `Logan`     | `Assets/_L_Level`                            |
| JohnPaul  | P   | `_P_`         | `JohnPaul`  | `Assets/_P_Theater`                          |
| Johnathan | J   | `_J_`         | `Johnathan` | `Assets/_J_Puzzles`                          |
| Sam       | A   | `_A_`         | `Sam`       | `Assets/_A_Sound`                            |

The "Current folders" column is a snapshot. A developer owns **every** folder whose name starts
with their prefix (for example, any new `_Z_...` folder belongs to Terry).

---

## General rules

1. **Always follow best practices** — for C#, for Unity, and for git.
2. **Keep code well commented.** Everything should be easy to understand for a junior developer.
   Explain *why* the code does something, not only *what* it does.
   - Give every class and public method a `/// <summary>` comment.
   - Write comments in **plain text only**. Inside `<summary>`, use no other XML tags
     (no `<c>`, `<see cref>`, `<para>`, `<list>`, `<code>`, `<param>`, `<returns>`, and so on),
     and no escaped characters like `&lt;` / `&gt;`. Describe parameters and return values in
     the summary's own words.
   - Put long explanations, usage guides, and code examples in a normal `//` comment block
     above the class, not inside `<summary>`. There, code such as `Singleton<GameManager>`
     can be written as-is.
3. **One class per file.** The file name must match the class name (Unity requires this for
   `MonoBehaviour` and `ScriptableObject` classes). The only exception is small helper functions,
   which may share a file with the class they support.
4. **Plan first, then wait for confirmation.** When asked to do a task, ALWAYS give a plan first
   and get the developer's confirmation before starting any work. The plan should list:
   - what you will do, step by step;
   - which files you will create or change (all inside the developer's own folders);
   - any integration notes for other developers' code (see Developer Rule 2).

   If the plan changes while you work, stop and confirm the new plan before continuing.

---

## Developer rules

1. **Know who you are working with.** Before doing ANYTHING in a session, make sure you know who
   the developer is (see "Start of every session"). Call them by name while you work.

2. **Stay inside the developer's folders.**
   - Create, edit, move, and delete files **only** inside folders that start with the current
     developer's prefix (for example, `_Z_` for Terry).
   - **NEVER change another developer's work.** That includes their scripts, prefabs, scenes,
     ScriptableObjects, and Unity `.meta` files.
   - Do not change shared areas outside any developer folder (such as `ProjectSettings/`,
     `Packages/`, or unprefixed folders in `Assets/`) or any art assets. If a task needs a change
     there, say so in the plan and let the developer raise it with the team.
   - You **may read any file** in the project, to understand how systems connect.
   - When the work touches another developer's code, suggest integration best practices in the
     plan. This may include asking the developer to request a change from the folder's owner.
     Name who owns it, and describe exactly what change to ask for and why.

3. **Commit only to the developer's own branch.**
   - Each developer has a GitHub branch named after them (see the table).
   - Commit and push only to that branch.
   - **Never commit or push to `main` or `development`.** Never merge into them, and never
     force-push.
   - If you are on the wrong branch, stop and tell the developer before committing.

4. **Teach advanced techniques.** When a task uses advanced techniques or code (for example,
   events and delegates, coroutines or async code, generics, interfaces and inheritance, design
   patterns, ScriptableObject architecture, or performance tricks), walk the developer through
   every step of what is happening. They should understand it well enough to explain it to the
   other developers.

---

## Quick checklist for every task

- [ ] I know who the developer is, and I am calling them by name.
- [ ] I gave a plan and the developer confirmed it.
- [ ] Every file I touch is inside the developer's own `_X_` folders.
- [ ] The code is well commented (plain text, `<summary>` only) and uses one class per file.
- [ ] I explained any advanced techniques step by step.
- [ ] Commits go only to the developer's own branch, never `main` or `development`.
