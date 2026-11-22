# CLAUDE.md — Shepherds Journey

Shepherds Journey is a Unity (C#) project built by a team of 16: **8 artists and 8 developers**.
Several developers use Claude at the same time, each on their own part of the game. These rules
keep every developer in their own lane. Follow them in every session, without exception.

---

## Start of every session (do this before anything else)

1. **Verify the Git branch.** Check the current Git branch immediately. If the user is on
   `main` or `development`, stop and warn them before reading, planning, or changing anything.
   Do not proceed until they switch to a valid feature or personal branch.
2. **Ask for the root folder.** Before reading, planning, or changing anything, ask:
   > "Before we start, which root folder under `Assets/` are we working in? (Must start with an
   > underscore, e.g., `Assets/_Player`)"
   - Verify that the folder path starts with `Assets/_` (an underscore directly under `Assets`).
   - Do not continue until a valid root folder is confirmed.
3. **Default to Education Mode.** Start every session in **Education Mode** automatically. Do
   not ask which mode to run in. Switch to **Workhorse Mode** only if the user explicitly
   instructs you to do so.
   - If the user instructs you to run in **Workhorse Mode**, immediately ask which exact file(s)
     you are permitted to work in before making any plans or edits.

---

## Session modes

Every session runs in exactly one of two modes. The mode controls **whether Claude may edit
files**. It does not relax any other rule in this document: every General rule and Boundary
rule applies in both modes, including the plan-and-confirm rule, the root folder rules, the
branch rules, the comment rules, and the one-class-per-file rule.

### Education Mode (default)

- Claude **never creates, edits, moves, renames, or deletes files**, and never runs commands
  that do so (no file-writing tools, no shell redirects, no `git commit`, and so on).
- Claude **may read any file** in the project to understand how systems connect.
- Claude provides code in the chat, in full or as snippets, and explains what it does and why.
  Say which file each piece of code belongs in. That file must still be inside the active
  `Assets/_...` root folder, and the code must follow every coding rule in this document.
- The developer copies the code into their own files.
- Claude still gives a plan and waits for confirmation before providing the code.
- Claude still teaches advanced techniques step by step (Boundary Rule 4).

### Workhorse Mode (explicit opt-in only)

- Activated **only** when the user explicitly instructs Claude to run in Workhorse Mode.
- Requires the user to specify the **exact file(s)** to work in. Claude **may only create or
  edit those exact specified files** (which must also reside inside the active `Assets/_...`
  root folder). Claude will not touch, create, or modify any additional files.
- Claude still gives a plan and waits for confirmation before making any edit.
- Claude still explains advanced techniques step by step (Boundary Rule 4).
- Claude still commits only to the user's active non-protected branch, and only when asked or
  confirmed (Boundary Rule 3).

### Switching modes

- The session always starts in **Education Mode** without asking.
- The user can switch to **Workhorse Mode** (or back to Education Mode) at any time by explicitly
  asking. Claude never switches to Workhorse Mode on its own.
- Whenever switching into Workhorse Mode, Claude must ask the user to name the exact file(s) it
  is allowed to work in before proceeding.
- The mode and authorized file list last for the current session only.

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
   - which files you will create or change (all inside the confirmed `Assets/_...` root folder,
     and restricted strictly to the explicitly named files if in Workhorse Mode);
   - any integration notes for code outside the active root folder (see Boundary Rule 2);
   - the current mode, and what that means for how the work is delivered (code in chat for
     Education Mode, direct edits to specified files only for Workhorse Mode).

   If the plan changes while you work, stop and confirm the new plan before continuing.

---

## Boundary & Git rules

1. **Stay strictly inside the confirmed root folder.**
   - All work must stay inside the specific `Assets/_...` root folder confirmed at the start of
     the session (for example, `Assets/_Player`).
   - **NEVER work in any folder outside the confirmed root folder.** Do not create, edit, move,
     or delete scripts, prefabs, scenes, ScriptableObjects, or Unity `.meta` files outside that
     folder.
   - In **Workhorse Mode**, scope is restricted even further: only modify or create the **exact
     file(s)** explicitly named by the user within that root folder. Do not touch any other file.

2. **Protect shared areas and other folders.**
   - Do not change shared areas outside the confirmed root folder (such as `ProjectSettings/`,
     `Packages/`, or other folders in `Assets/`) or any art assets. If a task needs a change
     outside the active root folder, say so in the plan so the developer can coordinate with the
     appropriate team member.
   - You **may read any file** in the project to understand how systems connect.
   - When the work touches code outside the confirmed root folder, suggest integration best
     practices in the plan and describe exactly what external change is needed and why.

3. **Never work on `main` or `development`.**
   - Verify the active Git branch at the start of every session and before any commit.
   - **The user must NOT be on `main` or `development`.** If the current branch is `main` or
     `development`, refuse to proceed until the user checks out a different branch.
   - **Never commit or push to `main` or `development`.** Never merge into them, and never
     force-push.
   - Commit and push only to the user's active feature/personal branch, and only when explicitly
     asked or confirmed.

4. **Teach advanced techniques.** When a task uses advanced techniques or code (for example,
   events and delegates, coroutines or async code, generics, interfaces and inheritance, design
   patterns, ScriptableObject architecture, or performance tricks), walk the developer through
   every step of what is happening. They should understand it well enough to explain it to the
   other developers.

---

## Quick checklist for every task

- [ ] The current Git branch is NOT `main` or `development`.
- [ ] The user has specified the active root folder starting with `Assets/_...`, and all work stays inside it.
- [ ] Running in Education Mode by default (or Workhorse Mode only if explicitly instructed).
- [ ] If in Education Mode, no files have been edited; only code and explanations were provided.
- [ ] If in Workhorse Mode, the user specified the exact file(s) to work in, and no additional files were touched.
- [ ] A plan was given and confirmed before starting work.
- [ ] The code is well commented (plain text, `<summary>` only) and uses one class per file.
- [ ] Any advanced techniques were explained step by step.
- [ ] Any Git commits go only to the active non-protected branch, never `main` or `development`.